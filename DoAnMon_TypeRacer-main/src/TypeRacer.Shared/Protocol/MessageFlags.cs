namespace TypeRacer.Shared.Protocol;

[Flags]
public enum MessageFlags : ushort
{
    None       = 0,
    Encrypted  = 1 << 0,   // AES-256-CBC encrypted payload
    Compressed = 1 << 1,   // Reserved for future use
}
