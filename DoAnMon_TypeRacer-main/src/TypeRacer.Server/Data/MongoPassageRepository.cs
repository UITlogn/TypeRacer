using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TypeRacer.Server.Data;

public class MongoPassageRepository : IPassageRepository
{
    private readonly MongoDatabaseManager _mongo;
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly string _contentField;
    private readonly BsonDocument _filter;
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesInitialized;
    public MongoPassageRepository(MongoDatabaseManager mongo)
    {
        _mongo = mongo;
        _collection = mongo.GetCollection<BsonDocument>(mongo.Options.PassagesCollection);
        _contentField = string.IsNullOrWhiteSpace(mongo.Options.ContentField)
            ? "content"
            : mongo.Options.ContentField.Trim();
        _filter = string.IsNullOrWhiteSpace(mongo.Options.FilterJson)
            ? new BsonDocument()
            : BsonDocument.Parse(mongo.Options.FilterJson);
    }

    public async Task<PassageRow?> GetRandomAsync(string language = "any", IReadOnlyCollection<int>? excludePassageIds = null)
    {
        await EnsureIndexesAsync();

        if (await _collection.EstimatedDocumentCountAsync() == 0)
        {
            await SeedDefaultPassagesAsync();
        }

        var normalizedLanguage = NormalizeLanguage(language);
        var excludedIds = (excludePassageIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (normalizedLanguage == "vi")
        {
            var preferredFilter = BuildEffectiveFilter(normalizedLanguage, excludedIds);
            var preferredCandidates = await SampleDocumentsAsync(preferredFilter, 120);
            var preferredDocument = preferredCandidates.FirstOrDefault(d =>
                VietnameseTextHelper.ContainsVietnameseDiacritics(GetContent(d)));
            if (preferredDocument != null)
            {
                return await ToPassageRowAsync(preferredDocument, normalizedLanguage);
            }
        }

        var effectiveFilter = BuildEffectiveFilter(normalizedLanguage, excludedIds);
        var document = await SampleDocumentAsync(effectiveFilter);
        if (document == null)
            return null;

        return await ToPassageRowAsync(document, normalizedLanguage);
    }

    public async Task<PassageRow?> GetByIdAsync(int id)
    {
        await EnsureIndexesAsync();

        var document = await _collection.Find(Builders<BsonDocument>.Filter.Eq("id", id)).FirstOrDefaultAsync();
        if (document == null)
            return null;

        var content = GetContent(document);
        if (string.IsNullOrWhiteSpace(content))
            return null;

        return new PassageRow
        {
            Id = id,
            Content = content.Trim(),
            Language = GetLanguage(document),
        };
    }

    public async Task<int> GetOrCreateByContentAsync(string content, string language = "en")
    {
        await EnsureIndexesAsync();

        var normalizedContent = content?.Trim() ?? string.Empty;
        var normalizedLanguage = NormalizeLanguage(language);
        if (string.IsNullOrWhiteSpace(normalizedContent))
            return 0;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq(_contentField, normalizedContent),
            Builders<BsonDocument>.Filter.Eq("language", normalizedLanguage));

        var existing = await _collection.Find(filter).FirstOrDefaultAsync();
        if (existing != null)
            return GetId(existing);

        var id = await _mongo.GetNextSequenceAsync("passages");
        var doc = new BsonDocument
        {
            { "id", id },
            { _contentField, normalizedContent },
            { "language", normalizedLanguage },
        };

        try
        {
            await _collection.InsertOneAsync(doc);
            return id;
        }
        catch (MongoWriteException ex) when (ex.WriteError != null &&
                                           ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await _collection.Find(filter).FirstOrDefaultAsync();
            return existing != null ? GetId(existing) : id;
        }
    }

    private async Task SeedDefaultPassagesAsync()
    {
        await _seedLock.WaitAsync();
        try
        {
            if (await _collection.EstimatedDocumentCountAsync() > 0)
                return;

            var seedFile = FindSeedFile();
            if (seedFile == null)
                return;

            var sql = await File.ReadAllTextAsync(seedFile);
            var matches = Regex.Matches(sql, @"N'((?:''|[^'])*)'", RegexOptions.Singleline);
            var docs = new List<BsonDocument>();

            foreach (Match match in matches)
            {
                var content = match.Groups[1].Value.Replace("''", "'").Trim();
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                docs.Add(new BsonDocument
                {
                    { "id", await _mongo.GetNextSequenceAsync("passages") },
                    { _contentField, content },
                    { "source", "database/003_seed_passages.sql" },
                    { "language", "en" },
                });
            }

            if (docs.Count > 0)
            {
                await _collection.InsertManyAsync(docs);
            }
        }
        finally
        {
            _seedLock.Release();
        }
    }

    private static string? FindSeedFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "database", "003_seed_passages.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "database", "003_seed_passages.sql"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private string? GetContent(BsonDocument document)
    {
        if (document.TryGetValue(_contentField, out var configuredValue) && configuredValue.IsString)
            return configuredValue.AsString;

        if (_contentField != "content" && document.TryGetValue("content", out var fallbackValue) && fallbackValue.IsString)
            return fallbackValue.AsString;

        return null;
    }

    private static int GetId(BsonDocument document)
    {
        if (!document.TryGetValue("id", out var idValue))
            return 0;

        return idValue.BsonType switch
        {
            BsonType.Int32 => idValue.AsInt32,
            BsonType.Int64 => (int)idValue.AsInt64,
            BsonType.Double => (int)idValue.AsDouble,
            _ => 0,
        };
    }

    private BsonDocument BuildEffectiveFilter(string normalizedLanguage, IReadOnlyCollection<int> excludedIds)
    {
        var filter = _filter.DeepClone().AsBsonDocument;

        if (normalizedLanguage != "any")
        {
            filter["language"] = normalizedLanguage;
        }

        if (excludedIds.Count > 0)
        {
            filter["id"] = new BsonDocument("$nin", new BsonArray(excludedIds));
        }

        return filter;
    }

    private async Task<BsonDocument?> SampleDocumentAsync(BsonDocument effectiveFilter)
    {
        if (await _collection.CountDocumentsAsync(effectiveFilter) == 0)
            return null;

        var pipeline = new List<BsonDocument>();
        if (effectiveFilter.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$match", effectiveFilter));
        }

        pipeline.Add(new BsonDocument("$sample", new BsonDocument("size", 1)));
        return await _collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
    }

    private async Task<List<BsonDocument>> SampleDocumentsAsync(BsonDocument effectiveFilter, int size)
    {
        if (size <= 0)
            return new List<BsonDocument>();

        if (await _collection.CountDocumentsAsync(effectiveFilter) == 0)
            return new List<BsonDocument>();

        var pipeline = new List<BsonDocument>();
        if (effectiveFilter.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$match", effectiveFilter));
        }

        pipeline.Add(new BsonDocument("$sample", new BsonDocument("size", size)));
        return await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
    }

    private async Task<PassageRow?> ToPassageRowAsync(BsonDocument document, string fallbackLanguage)
    {
        var content = GetContent(document);
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var id = GetId(document);
        if (id <= 0)
        {
            id = await _mongo.GetNextSequenceAsync("passages");
            var filter = Builders<BsonDocument>.Filter.Eq("_id", document["_id"]);
            var update = Builders<BsonDocument>.Update.Set("id", id);
            await _collection.UpdateOneAsync(filter, update);
        }

        return new PassageRow
        {
            Id = id,
            Content = content.Trim(),
            Language = GetLanguage(document, fallbackLanguage),
        };
    }

    private static string GetLanguage(BsonDocument document, string fallback = "en")
    {
        if (document.TryGetValue("language", out var value) && value.IsString)
        {
            return NormalizeLanguage(value.AsString);
        }

        return NormalizeLanguage(fallback);
    }

    private static string NormalizeLanguage(string? rawLanguage)
    {
        var code = (rawLanguage ?? "en").Trim().ToLowerInvariant();
        return code switch
        {
            "vi" => "vi",
            "en" => "en",
            "any" => "any",
            _ => "en",
        };
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

            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("id"),
                new CreateIndexOptions { Unique = true, Sparse = true }));

            _indexesInitialized = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }
}
