using MongoDB.Bson;
using MongoDB.Driver;

namespace TypeRacer.Server.Data;

public class MongoUserRepository : IUserRepository
{
    private readonly MongoDatabaseManager _mongo;
    private readonly IMongoCollection<MongoUserDocument> _users;
    private readonly IMongoCollection<MongoRaceResultDocument> _raceResults;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesInitialized;

    public MongoUserRepository(MongoDatabaseManager mongo)
    {
        _mongo = mongo;
        _users = mongo.GetCollection<MongoUserDocument>(mongo.Options.UsersCollection);
        _raceResults = mongo.GetCollection<MongoRaceResultDocument>(mongo.Options.RaceResultsCollection);
    }

    public async Task<UserRow?> GetByUsernameAsync(string username)
    {
        await EnsureIndexesAsync();

        var user = await _users.Find(x => x.Username == username).FirstOrDefaultAsync();
        return user == null
            ? null
            : new UserRow
            {
                Id = user.Id,
                Username = user.Username,
                PasswordHash = user.PasswordHash,
            };
    }

    public async Task<UserRow?> GetByIdAsync(int id)
    {
        await EnsureIndexesAsync();

        var user = await _users.Find(x => x.Id == id).FirstOrDefaultAsync();
        return user == null
            ? null
            : new UserRow
            {
                Id = user.Id,
                Username = user.Username,
                PasswordHash = user.PasswordHash,
            };
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        await EnsureIndexesAsync();
        return await _users.Find(x => x.Username == username).AnyAsync();
    }

    public async Task<int> CreateAsync(string username, string passwordHash)
    {
        await EnsureIndexesAsync();

        var id = await _mongo.GetNextSequenceAsync("users");
        var user = new MongoUserDocument
        {
            Id = id,
            Username = username,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            await _users.InsertOneAsync(user);
            return id;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateUsernameException(username, ex);
        }
    }

    public async Task UpdatePasswordHashAsync(int userId, string passwordHash)
    {
        await EnsureIndexesAsync();
        var update = Builders<MongoUserDocument>.Update.Set(x => x.PasswordHash, passwordHash);
        await _users.UpdateOneAsync(x => x.Id == userId, update);
    }

    public async Task<List<LeaderboardRow>> GetLeaderboardAsync(int top, string sortBy)
    {
        await EnsureIndexesAsync();

        var sortStage = new BsonDocument("$sort", GetLeaderboardSort(sortBy));
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("is_completed", true)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument { { "user_id", "$user_id" }, { "username", "$username" } } },
                { "total_races", new BsonDocument("$sum", 1) },
                { "total_wins", new BsonDocument("$sum",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$position", 1 }),
                        1,
                        0,
                    })) },
                { "avg_wpm", new BsonDocument("$avg", "$wpm") },
                { "best_wpm", new BsonDocument("$max", "$wpm") },
            }),
            sortStage,
            new BsonDocument("$limit", top),
        };

        var docs = await _raceResults.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return docs.Select(doc => new LeaderboardRow
        {
            UserId = doc["_id"]["user_id"].ToInt32(),
            Username = doc["_id"]["username"].AsString,
            TotalRaces = doc["total_races"].ToInt32(),
            TotalWins = doc["total_wins"].ToInt32(),
            AvgWpm = GetDecimal(doc, "avg_wpm"),
            BestWpm = GetDecimal(doc, "best_wpm"),
        }).ToList();
    }

    private async Task EnsureIndexesAsync()
    {
        if (_indexesInitialized)
            return;

        await _indexLock.WaitAsync();
        try
        {
            if (_indexesInitialized)
                return;

            await _users.Indexes.CreateOneAsync(new CreateIndexModel<MongoUserDocument>(
                Builders<MongoUserDocument>.IndexKeys.Ascending(x => x.Username),
                new CreateIndexOptions { Unique = true }));
            await _users.Indexes.CreateOneAsync(new CreateIndexModel<MongoUserDocument>(
                Builders<MongoUserDocument>.IndexKeys.Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true }));
            await _raceResults.Indexes.CreateOneAsync(new CreateIndexModel<MongoRaceResultDocument>(
                Builders<MongoRaceResultDocument>.IndexKeys
                    .Ascending(x => x.IsCompleted)
                    .Ascending(x => x.Position)
                    .Ascending(x => x.UserId)));

            _indexesInitialized = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static BsonDocument GetLeaderboardSort(string? sortBy)
    {
        return string.Equals(sortBy, "total_wins", StringComparison.OrdinalIgnoreCase)
            ? new BsonDocument
            {
                { "total_wins", -1 },
                { "avg_wpm", -1 },
                { "best_wpm", -1 },
                { "_id.username", 1 },
            }
            : new BsonDocument
            {
                { "avg_wpm", -1 },
                { "best_wpm", -1 },
                { "total_wins", -1 },
                { "_id.username", 1 },
            };
    }

    private static decimal GetDecimal(BsonDocument doc, string fieldName)
    {
        if (!doc.TryGetValue(fieldName, out var value))
            return 0m;

        return value.BsonType switch
        {
            BsonType.Decimal128 => MongoDB.Bson.Decimal128.ToDecimal(value.AsDecimal128),
            BsonType.Double => (decimal)value.AsDouble,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            _ => 0m,
        };
    }
}
