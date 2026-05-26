namespace TypeRacer.Shared.Models;

public class MatchHistoryEntryDto
{
    public int RaceId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int Position { get; set; }
    public int TotalPlayers { get; set; }
    public decimal Wpm { get; set; }
    public decimal Accuracy { get; set; }
    public int TimeTakenMs { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime PlayedAt { get; set; }
}
