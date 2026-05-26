namespace TypeRacer.Shared.Models;

public class PlayerProgressDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public double Progress { get; set; }
    public double Wpm { get; set; }
    public bool IsFinished { get; set; }
    public bool IsAiBot { get; set; }
}
