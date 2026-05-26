using MongoDB.Driver;

namespace TypeRacer.Server.Data;

public class MongoChatRepository : IChatRepository
{
    private readonly MongoDatabaseManager _mongo;
    private readonly IMongoCollection<MongoChatMessageDocument> _messages;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesInitialized;

    public MongoChatRepository(MongoDatabaseManager mongo)
    {
        _mongo = mongo;
        _messages = mongo.GetCollection<MongoChatMessageDocument>(mongo.Options.ChatMessagesCollection);
    }

    public async Task SaveAsync(string roomCode, int userId, string username, string content, DateTime sentAt)
    {
        await EnsureIndexesAsync();

        var id = await _mongo.GetNextSequenceAsync("chat_messages");
        var doc = new MongoChatMessageDocument
        {
            Id = id,
            RoomCode = roomCode,
            UserId = userId,
            Username = username,
            Content = content,
            SentAt = sentAt,
        };

        await _messages.InsertOneAsync(doc);
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

            await _messages.Indexes.CreateOneAsync(new CreateIndexModel<MongoChatMessageDocument>(
                Builders<MongoChatMessageDocument>.IndexKeys.Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true }));
            await _messages.Indexes.CreateOneAsync(new CreateIndexModel<MongoChatMessageDocument>(
                Builders<MongoChatMessageDocument>.IndexKeys
                    .Ascending(x => x.RoomCode)
                    .Descending(x => x.SentAt)));

            _indexesInitialized = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }
}
