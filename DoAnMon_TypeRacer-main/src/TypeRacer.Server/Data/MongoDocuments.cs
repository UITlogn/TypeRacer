using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TypeRacer.Server.Data;

public class MongoUserDocument
{
    [BsonId]
    public ObjectId ObjectId { get; set; }

    [BsonElement("id")]
    public int Id { get; set; }

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class MongoPassageDocument
{
    [BsonId]
    public ObjectId ObjectId { get; set; }

    [BsonElement("id")]
    public int Id { get; set; }

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("source")]
    public string Source { get; set; } = "seed";

    [BsonElement("language")]
    public string Language { get; set; } = "en";
}

public class MongoRoomDocument
{
    [BsonId]
    public ObjectId ObjectId { get; set; }

    [BsonElement("id")]
    public int Id { get; set; }

    [BsonElement("room_code")]
    public string RoomCode { get; set; } = string.Empty;

    [BsonElement("host_user_id")]
    public int HostUserId { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MongoRaceDocument
{
    [BsonId]
    public ObjectId ObjectId { get; set; }

    [BsonElement("id")]
    public int Id { get; set; }

    [BsonElement("room_id")]
    public int RoomId { get; set; }

    [BsonElement("room_code")]
    public string RoomCode { get; set; } = string.Empty;

    [BsonElement("passage_id")]
    public int PassageId { get; set; }

    [BsonElement("started_at")]
    public DateTime StartedAt { get; set; }

    [BsonElement("ended_at")]
    [BsonIgnoreIfNull]
    public DateTime? EndedAt { get; set; }
}

public class MongoRaceResultDocument
{
    [BsonId]
    public ObjectId ObjectId { get; set; }

    [BsonElement("race_id")]
    public int RaceId { get; set; }

    [BsonElement("room_code")]
    public string RoomCode { get; set; } = string.Empty;

    [BsonElement("user_id")]
    public int UserId { get; set; }

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("position")]
    public int Position { get; set; }

    [BsonElement("total_players")]
    public int TotalPlayers { get; set; }

    [BsonElement("wpm")]
    public double Wpm { get; set; }

    [BsonElement("accuracy")]
    public double Accuracy { get; set; }

    [BsonElement("chars_correct")]
    public int CharsCorrect { get; set; }

    [BsonElement("chars_wrong")]
    public int CharsWrong { get; set; }

    [BsonElement("time_taken_ms")]
    public int TimeTakenMs { get; set; }

    [BsonElement("is_completed")]
    public bool IsCompleted { get; set; }

    [BsonElement("played_at")]
    public DateTime PlayedAt { get; set; }
}

public class MongoChatMessageDocument
{
    [BsonId]
    public ObjectId ObjectId { get; set; }

    [BsonElement("id")]
    public int Id { get; set; }

    [BsonElement("room_code")]
    public string RoomCode { get; set; } = string.Empty;

    [BsonElement("user_id")]
    public int UserId { get; set; }

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("sent_at")]
    public DateTime SentAt { get; set; }
}
