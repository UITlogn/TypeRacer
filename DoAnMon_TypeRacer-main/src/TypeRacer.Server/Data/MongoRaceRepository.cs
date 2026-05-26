using MongoDB.Driver;
using TypeRacer.Shared.Models;

namespace TypeRacer.Server.Data;

public class MongoRaceRepository : IRaceRepository
{
    private readonly MongoDatabaseManager _mongo;
    private readonly IMongoCollection<MongoRoomDocument> _rooms;
    private readonly IMongoCollection<MongoRaceDocument> _races;
    private readonly IMongoCollection<MongoRaceResultDocument> _raceResults;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesInitialized;

    public MongoRaceRepository(MongoDatabaseManager mongo)
    {
        _mongo = mongo;
        _rooms = mongo.GetCollection<MongoRoomDocument>(mongo.Options.RoomsCollection);
        _races = mongo.GetCollection<MongoRaceDocument>(mongo.Options.RacesCollection);
        _raceResults = mongo.GetCollection<MongoRaceResultDocument>(mongo.Options.RaceResultsCollection);
    }

    public async Task<int> GetOrCreateRoomAsync(string roomCode, int hostId)
    {
        await EnsureIndexesAsync();

        var existing = await _rooms.Find(x => x.RoomCode == roomCode).FirstOrDefaultAsync();
        if (existing != null)
            return existing.Id;

        var id = await _mongo.GetNextSequenceAsync("rooms");
        var room = new MongoRoomDocument
        {
            Id = id,
            RoomCode = roomCode,
            HostUserId = hostId,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            await _rooms.InsertOneAsync(room);
            return id;
        }
        catch (MongoWriteException)
        {
            var concurrent = await _rooms.Find(x => x.RoomCode == roomCode).FirstOrDefaultAsync();
            if (concurrent != null)
                return concurrent.Id;

            throw;
        }
    }

    public async Task UpdateRoomHostAsync(string roomCode, int hostId)
    {
        await EnsureIndexesAsync();

        var update = Builders<MongoRoomDocument>.Update.Set(x => x.HostUserId, hostId);
        await _rooms.UpdateOneAsync(x => x.RoomCode == roomCode, update);
    }

    public async Task<int> CreateRaceAsync(int roomId, string roomCode, int passageId, DateTime startedAt)
    {
        await EnsureIndexesAsync();

        var id = await _mongo.GetNextSequenceAsync("races");
        var race = new MongoRaceDocument
        {
            Id = id,
            RoomId = roomId,
            RoomCode = roomCode,
            PassageId = passageId,
            StartedAt = startedAt,
        };

        await _races.InsertOneAsync(race);
        return id;
    }

    public async Task EndRaceAsync(int raceId, DateTime endedAt)
    {
        await EnsureIndexesAsync();

        var update = Builders<MongoRaceDocument>.Update.Set(x => x.EndedAt, endedAt);
        await _races.UpdateOneAsync(x => x.Id == raceId, update);
    }

    public async Task InsertResultAsync(int raceId, string roomCode, RaceResultDto result, int totalPlayers, DateTime playedAt)
    {
        await EnsureIndexesAsync();

        var doc = new MongoRaceResultDocument
        {
            RaceId = raceId,
            RoomCode = roomCode,
            UserId = result.UserId,
            Username = result.Username,
            Position = result.Position,
            TotalPlayers = totalPlayers,
            Wpm = (double)result.Wpm,
            Accuracy = (double)result.Accuracy,
            CharsCorrect = result.CharsCorrect,
            CharsWrong = result.CharsWrong,
            TimeTakenMs = result.TimeTakenMs,
            IsCompleted = result.IsCompleted,
            PlayedAt = playedAt,
        };

        await _raceResults.InsertOneAsync(doc);
    }

    public async Task<List<MatchHistoryRow>> GetMatchHistoryAsync(int userId, int limit)
    {
        await EnsureIndexesAsync();

        var docs = await _raceResults.Find(x => x.UserId == userId)
            .SortByDescending(x => x.PlayedAt)
            .Limit(limit)
            .ToListAsync();

        return docs.Select(x => new MatchHistoryRow
        {
            RaceId = x.RaceId,
            RoomCode = x.RoomCode,
            Position = x.Position,
            TotalPlayers = x.TotalPlayers,
            Wpm = (decimal)x.Wpm,
            Accuracy = (decimal)x.Accuracy,
            TimeTakenMs = x.TimeTakenMs,
            IsCompleted = x.IsCompleted,
            PlayedAt = x.PlayedAt,
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

            await _rooms.Indexes.CreateOneAsync(new CreateIndexModel<MongoRoomDocument>(
                Builders<MongoRoomDocument>.IndexKeys.Ascending(x => x.RoomCode),
                new CreateIndexOptions { Unique = true }));
            await _rooms.Indexes.CreateOneAsync(new CreateIndexModel<MongoRoomDocument>(
                Builders<MongoRoomDocument>.IndexKeys.Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true }));
            await _races.Indexes.CreateOneAsync(new CreateIndexModel<MongoRaceDocument>(
                Builders<MongoRaceDocument>.IndexKeys.Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true }));
            await _raceResults.Indexes.CreateOneAsync(new CreateIndexModel<MongoRaceResultDocument>(
                Builders<MongoRaceResultDocument>.IndexKeys
                    .Ascending(x => x.RaceId)
                    .Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true }));
            await _raceResults.Indexes.CreateOneAsync(new CreateIndexModel<MongoRaceResultDocument>(
                Builders<MongoRaceResultDocument>.IndexKeys
                    .Ascending(x => x.UserId)
                    .Descending(x => x.PlayedAt)));

            _indexesInitialized = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }
}
