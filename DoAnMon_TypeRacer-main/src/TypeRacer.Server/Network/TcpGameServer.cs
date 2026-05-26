using System.Net;
using System.Net.Sockets;
using TypeRacer.Server.Handlers;
using TypeRacer.Server.Logging;
using TypeRacer.Server.Services;
using TypeRacer.Server.State;
using TypeRacer.Shared;

namespace TypeRacer.Server.Network;

/// <summary>
/// Main TCP listener that accepts client connections and spawns handler tasks.
/// </summary>
public class TcpGameServer
{
    private readonly int _port;
    private readonly ServerState _serverState;
    private readonly IMessageHandler _authHandler;
    private readonly IMessageHandler _roomHandler;
    private readonly GameHandler _gameHandler;
    private readonly IMessageHandler _chatHandler;
    private readonly IMessageHandler _statsHandler;
    private readonly MistakeMemoryService _mistakeMemory;
    private readonly FileLogger _logger;
    private TcpListener? _listener;

    public TcpGameServer(
        int port,
        ServerState serverState,
        IMessageHandler authHandler,
        IMessageHandler roomHandler,
        GameHandler gameHandler,
        IMessageHandler chatHandler,
        IMessageHandler statsHandler,
        MistakeMemoryService mistakeMemory,
        FileLogger logger)
    {
        _port = port;
        _serverState = serverState;
        _authHandler = authHandler;
        _roomHandler = roomHandler;
        _gameHandler = gameHandler;
        _chatHandler = chatHandler;
        _statsHandler = statsHandler;
        _mistakeMemory = mistakeMemory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _logger.Info($"TypeRacer Server started on port {_port}");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleNewClient(tcpClient, ct), ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Server shutting down...");
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleNewClient(TcpClient tcpClient, CancellationToken ct)
    {
        var client = new ConnectedClient
        {
            TcpClient = tcpClient,
            Stream = tcpClient.GetStream(),
        };

        var handler = new ClientHandler(
            client,
            _authHandler,
            _roomHandler,
            _gameHandler,
            _chatHandler,
            _statsHandler,
            _serverState,
            _mistakeMemory,
            _logger);

        await handler.HandleAsync(ct);
    }
}
