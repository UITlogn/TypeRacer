using Microsoft.Extensions.Configuration;
using TypeRacer.Server.Configuration;
using TypeRacer.Server.Data;
using TypeRacer.Server.Handlers;
using TypeRacer.Server.Logging;
using TypeRacer.Server.Network;
using TypeRacer.Server.Services;
using TypeRacer.Server.State;

namespace TypeRacer.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        LocalEnvironmentLoader.LoadOptionalDotEnv();

        // Load configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var port = int.TryParse(config["Server:Port"], out var p) ? p : 5000;

        // Initialize logging (I/O FileStream for rubric)
        using var logger = new FileLogger();
        logger.Info("=== TypeRacer Server Starting ===");

        IUserRepository userRepo;
        IRaceRepository raceRepo;
        IPassageRepository passageRepo;
        IChatRepository chatRepo;

        var dataProvider = (config["Data:Provider"] ?? "SqlServer").Trim();
        if (string.Equals(dataProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var store = new InMemoryDataStore();
            userRepo = new InMemoryUserRepository(store);
            raceRepo = new InMemoryRaceRepository(store);
            passageRepo = new InMemoryPassageRepository(store);
            chatRepo = new InMemoryChatRepository();
            logger.Info("Data provider: InMemory (local demo host)");
        }
        else if (string.Equals(dataProvider, "MongoDb", StringComparison.OrdinalIgnoreCase))
        {
            var mongoOptions = config.GetSection("MongoDb").Get<MongoDatabaseOptions>() ?? new MongoDatabaseOptions();
            var mongoManager = new MongoDatabaseManager(mongoOptions);
            var mongoOk = await mongoManager.TestConnectionAsync();
            if (!mongoOk)
            {
                logger.Error("MongoDB connection failed, startup aborted.");
                Environment.ExitCode = 1;
                return;
            }

            logger.Info("MongoDB connection successful");

            userRepo = new MongoUserRepository(mongoManager);
            raceRepo = new MongoRaceRepository(mongoManager);
            passageRepo = new MongoPassageRepository(mongoManager);
            chatRepo = new MongoChatRepository(mongoManager);
            logger.Info("Data provider: MongoDB");
        }
        else
        {
            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                logger.Error("ConnectionStrings:DefaultConnection is missing.");
                Environment.ExitCode = 1;
                return;
            }

            var dbManager = new DatabaseManager(connectionString);
            var dbOk = await dbManager.TestConnectionAsync();
            if (!dbOk)
            {
                logger.Error("SQL Server connection failed, startup aborted.");
                Environment.ExitCode = 1;
                return;
            }

            logger.Info("SQL Server connection successful");

            userRepo = new UserRepository(dbManager);
            raceRepo = new RaceRepository(dbManager);
            passageRepo = new SqlPassageRepository(dbManager);
            chatRepo = new SqlChatRepository(dbManager);
            logger.Info("Data provider: SQL Server");
        }

        // Initialize services
        var authService = new AuthService(userRepo);
        var mistakeMemory = new MistakeMemoryService();
        var aiCoachOptions = config.GetSection("AiCoach").Get<AiCoachOptions>() ?? new AiCoachOptions();
        var envApiKey = Environment.GetEnvironmentVariable("AICOACH__OPENAIAPIKEY")
            ?? Environment.GetEnvironmentVariable("AI_COACH_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(aiCoachOptions.OpenAiApiKey) &&
            string.IsNullOrWhiteSpace(aiCoachOptions.ApiKey))
        {
            aiCoachOptions.OpenAiApiKey = envApiKey;
        }
        var aiCoachService = new AiCoachService(aiCoachOptions, logger);
        logger.Info(
            $"AI Coach: enabled={aiCoachOptions.Enabled}, provider={aiCoachOptions.Provider}, model={aiCoachOptions.ResolveModel()}, key={(string.IsNullOrWhiteSpace(aiCoachOptions.ResolveApiKey()) ? "missing" : "configured")}");

        // Initialize state
        var serverState = new ServerState();

        // Initialize handlers
        var authHandler = new AuthHandler(authService, serverState, mistakeMemory, logger);
        var roomHandler = new RoomHandler(serverState, raceRepo, mistakeMemory, logger);
        var gameHandler = new GameHandler(serverState, passageRepo, raceRepo, mistakeMemory, logger);
        roomHandler.SetGameHandler(gameHandler);
        var chatHandler = new ChatHandler(serverState, chatRepo, logger);
        var statsHandler = new StatsHandler(userRepo, raceRepo, aiCoachService, mistakeMemory, logger);

        // Cancellation
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            logger.Info("Shutdown requested...");
        };

        // Start heartbeat service
        var heartbeatService = new HeartbeatService(serverState, logger);
        var heartbeatTask = Task.Run(() => heartbeatService.StartAsync(cts.Token));
        var communityQuickPlayService = new CommunityQuickPlayService(serverState, gameHandler, logger);
        var communityQuickPlayTask = Task.Run(() => communityQuickPlayService.StartAsync(cts.Token));

        // Start TCP server
        var tcpServer = new TcpGameServer(
            port, serverState,
            authHandler, roomHandler, gameHandler, chatHandler, statsHandler,
            mistakeMemory,
            logger);

        logger.Info($"Server listening on port {port}");
        logger.Info("Press Ctrl+C to stop");

        await tcpServer.StartAsync(cts.Token);
    }
}
