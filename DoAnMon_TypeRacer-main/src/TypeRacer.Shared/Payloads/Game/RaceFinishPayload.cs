namespace TypeRacer.Shared.Payloads.Game;

public class RaceFinishPayload
{
    public string RoomCode { get; set; } = string.Empty;
    public int CorrectChars { get; set; }
    public int WrongChars { get; set; }
    public int TimeTakenMs { get; set; }
    public string TypedText { get; set; } = string.Empty;
    public bool IsTimeout { get; set; }
    public int BackspaceCount { get; set; }
    public bool IsDisqualified { get; set; }
}
