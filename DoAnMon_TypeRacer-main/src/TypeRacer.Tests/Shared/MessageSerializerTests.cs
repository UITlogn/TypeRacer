using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using TypeRacer.Shared;
using TypeRacer.Shared.Payloads.Ai;
using TypeRacer.Shared.Crypto;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Tests.Shared;

public class MessageSerializerTests
{
    [Fact]
    public async Task WriteAsync_WritesBigEndianHeaderAndPlainJsonBody()
    {
        var payload = new { room_code = "ABC123", current_position = 42 };
        var message = NetworkMessage.Create(MessageType.TYPING_UPDATE, payload);
        await using var stream = new MemoryStream();

        await MessageSerializer.WriteAsync(stream, message);

        var bytes = stream.ToArray();
        var bodyLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(0, 4));
        var type = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4, 2));
        var flags = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(6, 2));
        var body = bytes.AsSpan(Constants.HeaderSize, bodyLength).ToArray();

        Assert.Equal(message.Body.Length, bodyLength);
        Assert.Equal((ushort)MessageType.TYPING_UPDATE, type);
        Assert.Equal((ushort)MessageFlags.None, flags);
        Assert.Equal(message.Body, body);
        Assert.Contains("\"room_code\":\"ABC123\"", Encoding.UTF8.GetString(body));
    }

    [Fact]
    public async Task WriteAsync_EncryptsBodyWhenFlagIsSet()
    {
        var payload = new { username = "alice", password = "secret" };
        var message = NetworkMessage.Create(MessageType.LOGIN_REQUEST, payload, MessageFlags.Encrypted);
        await using var stream = new MemoryStream();

        await MessageSerializer.WriteAsync(stream, message);

        var bytes = stream.ToArray();
        var bodyLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(0, 4));
        var type = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4, 2));
        var flags = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(6, 2));
        var cipherBody = bytes.AsSpan(Constants.HeaderSize, bodyLength).ToArray();
        var plainBody = AesEncryption.Decrypt(cipherBody);

        Assert.Equal((ushort)MessageType.LOGIN_REQUEST, type);
        Assert.Equal((ushort)MessageFlags.Encrypted, flags);
        Assert.NotEqual(message.Body, cipherBody);
        Assert.Equal(message.Body, plainBody);
        Assert.DoesNotContain("secret", Encoding.UTF8.GetString(cipherBody));
    }

    [Fact]
    public void NetworkMessage_UsesSnakeCaseJsonPayloads()
    {
        var message = NetworkMessage.Create(MessageType.CREATE_ROOM, new
        {
            RaceDurationSeconds = 180,
            EnableAiMode = true,
        });

        var json = Encoding.UTF8.GetString(message.Body);
        var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("race_duration_seconds", out var duration));
        Assert.True(document.RootElement.TryGetProperty("enable_ai_mode", out var aiMode));
        Assert.Equal(180, duration.GetInt32());
        Assert.True(aiMode.GetBoolean());
    }

    [Fact]
    public void AiCoachResponse_SerializesNewFingerprintFields()
    {
        var response = new AiCoachResponse
        {
            Success = true,
            MistakeFingerprint = new List<string> { "Typing DNA: weakspot nh" },
            AdaptiveRaceStrategy = new List<string> { "Opening 0-20s: keep steady" },
            PersonalizationScore = 88.5m,
            TrainingPackSignature = "ABCDEF1234567890",
            ProblemKeyStoryTitle = "TypeAI problem-key story",
            ProblemKeyStoryKeys = new List<string> { "nh", "th" },
            ProblemKeyStoryPassage = "The night shift chef thinks through thin threads before the thunder starts.",
        };

        var message = NetworkMessage.Create(MessageType.AI_COACH_RESPONSE, response);
        var json = Encoding.UTF8.GetString(message.Body);
        var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("mistake_fingerprint", out var fingerprint));
        Assert.True(document.RootElement.TryGetProperty("adaptive_race_strategy", out var strategy));
        Assert.True(document.RootElement.TryGetProperty("personalization_score", out var score));
        Assert.True(document.RootElement.TryGetProperty("training_pack_signature", out var signature));
        Assert.True(document.RootElement.TryGetProperty("problem_key_story_title", out var storyTitle));
        Assert.True(document.RootElement.TryGetProperty("problem_key_story_keys", out var storyKeys));
        Assert.True(document.RootElement.TryGetProperty("problem_key_story_passage", out var storyPassage));
        Assert.Equal("Typing DNA: weakspot nh", fingerprint[0].GetString());
        Assert.Equal("Opening 0-20s: keep steady", strategy[0].GetString());
        Assert.Equal(88.5m, score.GetDecimal());
        Assert.Equal("ABCDEF1234567890", signature.GetString());
        Assert.Equal("TypeAI problem-key story", storyTitle.GetString());
        Assert.Equal("nh", storyKeys[0].GetString());
        Assert.Contains("thin", storyPassage.GetString());
    }
}
