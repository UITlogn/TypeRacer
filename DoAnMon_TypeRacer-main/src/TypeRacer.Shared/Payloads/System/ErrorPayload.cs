using TypeRacer.Shared.Enums;

namespace TypeRacer.Shared.Payloads.System;

public class ErrorPayload
{
    public ErrorCode Code { get; set; }
    public string Message { get; set; } = string.Empty;
}
