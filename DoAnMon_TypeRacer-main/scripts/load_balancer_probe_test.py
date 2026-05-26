#!/usr/bin/env python3
import argparse
import json
import os
import shutil
import socket
import subprocess
import sys
import tempfile
import threading
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "TypeRacer.LoadBalancer" / "TypeRacer.LoadBalancer.csproj"


def pick_dotnet() -> str:
    explicit = os.environ.get("DOTNET_BIN")
    if explicit:
        return explicit

    local = Path("/root/.dotnet/dotnet")
    if local.exists():
        return str(local)

    found = shutil.which("dotnet")
    if found:
        return found

    raise RuntimeError("dotnet not found")


def free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return int(sock.getsockname()[1])


def wait_port(port: int, timeout: float) -> None:
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            with socket.create_connection(("127.0.0.1", port), timeout=0.2):
                return
        except OSError:
            time.sleep(0.05)
    raise TimeoutError(f"port {port} did not open")


class EchoBackend:
    def __init__(self, name: str, port: int):
        self.name = name
        self.port = port
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None
        self.accepted = 0

    def start(self) -> None:
        self._thread = threading.Thread(target=self._serve, name=f"backend-{self.name}", daemon=True)
        self._thread.start()
        wait_port(self.port, 3.0)

    def stop(self) -> None:
        self._stop.set()
        try:
            with socket.create_connection(("127.0.0.1", self.port), timeout=0.2):
                pass
        except OSError:
            pass
        if self._thread:
            self._thread.join(timeout=1.0)

    def _serve(self) -> None:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
            server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            server.bind(("127.0.0.1", self.port))
            server.listen()
            server.settimeout(0.2)

            while not self._stop.is_set():
                try:
                    conn, _addr = server.accept()
                except socket.timeout:
                    continue

                self.accepted += 1
                threading.Thread(target=self._handle, args=(conn,), daemon=True).start()

    def _handle(self, conn: socket.socket) -> None:
        with conn:
            conn.settimeout(1.0)
            try:
                data = conn.recv(4096)
            except OSError:
                return

            if not data:
                return

            conn.sendall(f"{self.name}:".encode("utf-8") + data)


class LoadBalancerProcess:
    def __init__(self, publish_dir: Path, lb_port: int, backend_ports: list[int], strategy: str, health_ms: int):
        self.publish_dir = publish_dir
        self.lb_port = lb_port
        self.backend_ports = backend_ports
        self.strategy = strategy
        self.health_ms = health_ms
        self.lines: list[str] = []
        self.proc: subprocess.Popen[str] | None = None

    def start(self) -> None:
        settings = {
            "LoadBalancer": {
                "Port": self.lb_port,
                "Strategy": self.strategy,
                "HealthCheckIntervalMs": self.health_ms,
                "Backends": [{"Host": "127.0.0.1", "Port": port} for port in self.backend_ports],
            }
        }
        (self.publish_dir / "appsettings.json").write_text(
            json.dumps(settings, indent=2),
            encoding="utf-8",
        )

        dll = self.publish_dir / "TypeRacer.LoadBalancer.dll"
        cmd = [pick_dotnet(), str(dll)]

        self.proc = subprocess.Popen(
            cmd,
            cwd=self.publish_dir,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
        )
        threading.Thread(target=self._read_output, daemon=True).start()
        if not self.wait_for_line("[LoadBalancer] Đang lắng nghe", 5.0):
            raise TimeoutError(f"load balancer did not start on port {self.lb_port}; output={self.lines[-10:]}")

    def stop(self) -> None:
        if self.proc is None:
            return

        if self.proc.poll() is None:
            self.proc.terminate()
            try:
                self.proc.wait(timeout=2.0)
            except subprocess.TimeoutExpired:
                self.proc.kill()
                self.proc.wait(timeout=2.0)

    def wait_for_line(self, needle: str, timeout: float) -> bool:
        deadline = time.time() + timeout
        while time.time() < deadline:
            if any(needle in line for line in self.lines):
                return True
            time.sleep(0.05)
        return False

    def _read_output(self) -> None:
        if self.proc is None or self.proc.stdout is None:
            return

        for line in self.proc.stdout:
            self.lines.append(line.rstrip())


def publish_load_balancer(publish_dir: Path) -> None:
    dotnet = pick_dotnet()
    result = subprocess.run(
        [dotnet, "publish", str(PROJECT), "-c", "Release", "-o", str(publish_dir)],
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )
    if result.returncode != 0:
        print(result.stdout)
        raise RuntimeError("dotnet publish for LoadBalancer failed")


def probe(lb_port: int, payload: str, timeout: float = 3.0) -> str:
    with socket.create_connection(("127.0.0.1", lb_port), timeout=timeout) as sock:
        sock.settimeout(timeout)
        sock.sendall(payload.encode("utf-8"))
        data = sock.recv(4096)
        return data.decode("utf-8", errors="replace")


def run_round_robin(publish_dir: Path) -> None:
    backend_a = EchoBackend("backend-a", free_port())
    backend_b = EchoBackend("backend-b", free_port())
    backend_a.start()
    backend_b.start()
    lb = LoadBalancerProcess(
        publish_dir,
        free_port(),
        [backend_a.port, backend_b.port],
        "RoundRobin",
        health_ms=250,
    )
    try:
        lb.start()
        responses = [probe(lb.lb_port, f"rr-{idx}") for idx in range(4)]
        owners = [response.split(":", 1)[0] for response in responses]
        if owners != ["backend-a", "backend-b", "backend-a", "backend-b"]:
            raise AssertionError(f"round-robin order invalid: responses={responses}")
        print(f"ROUND_ROBIN ok: {responses}")
    finally:
        lb.stop()
        backend_a.stop()
        backend_b.stop()


def run_health_failover(publish_dir: Path) -> None:
    backend_a = EchoBackend("backend-a", free_port())
    backend_a.start()
    offline_port = free_port()
    lb = LoadBalancerProcess(
        publish_dir,
        free_port(),
        [backend_a.port, offline_port],
        "RoundRobin",
        health_ms=200,
    )
    try:
        lb.start()
        if not lb.wait_for_line(f"127.0.0.1:{offline_port} - OFFLINE", 4.0):
            raise AssertionError("health checker did not mark offline backend")

        responses = [probe(lb.lb_port, f"failover-{idx}") for idx in range(3)]
        if any(not response.startswith("backend-a:") for response in responses):
            raise AssertionError(f"offline backend was selected: responses={responses}")
        print(f"HEALTH_FAILOVER ok: {responses}")
    finally:
        lb.stop()
        backend_a.stop()


def main() -> int:
    parser = argparse.ArgumentParser(description="TypeRacer LoadBalancer round-robin + health failover probe")
    parser.add_argument("--publish-dir", default="")
    args = parser.parse_args()

    if args.publish_dir:
        publish_dir = Path(args.publish_dir).resolve()
        publish_dir.mkdir(parents=True, exist_ok=True)
        publish_load_balancer(publish_dir)
        run_round_robin(publish_dir)
        run_health_failover(publish_dir)
    else:
        with tempfile.TemporaryDirectory(prefix="typeracer-lb-") as tmp:
            publish_dir = Path(tmp) / "publish"
            publish_dir.mkdir(parents=True, exist_ok=True)
            publish_load_balancer(publish_dir)
            run_round_robin(publish_dir)
            run_health_failover(publish_dir)

    print("LOAD BALANCER PROBE TEST PASSED")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"LOAD BALANCER PROBE TEST FAILED: {exc}", file=sys.stderr)
        sys.exit(1)
