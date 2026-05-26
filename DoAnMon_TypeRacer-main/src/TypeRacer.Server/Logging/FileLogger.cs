namespace TypeRacer.Server.Logging;

public sealed class FileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public FileLogger(string logDirectory = "logs")
    {
        Directory.CreateDirectory(logDirectory);
        var fileName = $"server_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        var path = Path.Combine(logDirectory, fileName);
        var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fileStream) { AutoFlush = true };
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);
    public void Error(string message, Exception ex) => Write("ERROR", $"{message} | {ex}");

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        lock (_lock)
        {
            _writer.WriteLine(line);
        }
        Console.WriteLine(line);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
