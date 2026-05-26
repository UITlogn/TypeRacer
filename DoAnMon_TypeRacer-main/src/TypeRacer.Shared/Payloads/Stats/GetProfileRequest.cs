namespace TypeRacer.Shared.Payloads.Stats;

public class GetProfileRequest
{
    public int? UserId { get; set; } // null = self
}
