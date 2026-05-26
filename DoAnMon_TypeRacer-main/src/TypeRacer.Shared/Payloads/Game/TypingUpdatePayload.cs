namespace TypeRacer.Shared.Payloads.Game;

public class TypingUpdatePayload
{
    public string RoomCode { get; set; } = string.Empty;
    public int CurrentPosition { get; set; }
    public int CorrectChars { get; set; }
    public int WrongChars { get; set; }
    public string TypedText { get; set; } = string.Empty;
    public long Timestamp { get; set; }
}
