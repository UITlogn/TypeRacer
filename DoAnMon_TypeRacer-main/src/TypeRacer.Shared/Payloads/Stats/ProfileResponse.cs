using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Stats;

public class ProfileResponse
{
    public bool Success { get; set; }
    public UserDto? User { get; set; }
    public string? ErrorMessage { get; set; }
}
