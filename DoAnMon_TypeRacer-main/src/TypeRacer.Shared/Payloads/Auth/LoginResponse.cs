using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Auth;

public class LoginResponse
{
    public bool Success { get; set; }
    public string? SessionToken { get; set; }
    public UserDto? User { get; set; }
    public string? ErrorMessage { get; set; }
}
