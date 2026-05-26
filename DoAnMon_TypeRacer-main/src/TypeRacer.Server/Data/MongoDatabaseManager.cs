using System.Security.Authentication;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace TypeRacer.Server.Data;

public class MongoDatabaseManager
{
    private readonly IMongoCollection<CounterDocument> _counters;

    public MongoDatabaseOptions Options { get; }
    public IMongoDatabase Database { get; }

    public MongoDatabaseManager(MongoDatabaseOptions options)
    {
        Options = options;

        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
        settings.SslSettings = new SslSettings
        {
            EnabledSslProtocols = SslProtocols.Tls12,
            CheckCertificateRevocation = false,
        };

        var client = new MongoClient(settings);
        Database = client.GetDatabase(options.Database);
        _counters = Database.GetCollection<CounterDocument>(options.CountersCollection);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await Database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        return Database.GetCollection<T>(collectionName);
    }

    public async Task<int> GetNextSequenceAsync(string name)
    {
        var filter = Builders<CounterDocument>.Filter.Eq(x => x.Id, name);
        var update = Builders<CounterDocument>.Update.Inc(x => x.Sequence, 1);
        var options = new FindOneAndUpdateOptions<CounterDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };

        var counter = await _counters.FindOneAndUpdateAsync(filter, update, options);
        return counter.Sequence;
    }
}

public class MongoDatabaseOptions
{
    private string _passagesCollection = "passages";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "TypeRacer";
    public string UsersCollection { get; set; } = "users";
    public string PassagesCollection
    {
        get => _passagesCollection;
        set => _passagesCollection = string.IsNullOrWhiteSpace(value) ? "passages" : value;
    }

    // Compatibility alias for older Passage Mongo config.
    public string Collection
    {
        get => _passagesCollection;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                _passagesCollection = value;
        }
    }

    public string ContentField { get; set; } = "content";
    public string RoomsCollection { get; set; } = "rooms";
    public string RacesCollection { get; set; } = "races";
    public string RaceResultsCollection { get; set; } = "race_results";
    public string ChatMessagesCollection { get; set; } = "chat_messages";
    public string CountersCollection { get; set; } = "counters";
    public string FilterJson { get; set; } = "{}";
}

public class CounterDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("seq")]
    public int Sequence { get; set; }
}
