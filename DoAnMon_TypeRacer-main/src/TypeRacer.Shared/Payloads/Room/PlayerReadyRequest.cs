namespace TypeRacer.Shared.Payloads.Room;

public class PlayerReadyRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public bool IsReady { get; set; }
}
