using System.Net;

namespace MdnsListener.Models;

/// <summary>
/// Represents a discovered mDNS service.
/// </summary>
public sealed record ServiceRecord
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public ReadOnlyMemory<byte> Data { get; init; }
    public uint Ttl { get; init; }
    public IPEndPoint Source { get; init; } = new(IPAddress.None, 0);
    public DateTime Timestamp { get; init; }
    public bool IsGoodbye { get; init; }

    public override string ToString()
    {
        var status = IsGoodbye ? "GOODBYE" : "ACTIVE";
        var age = DateTime.UtcNow - Timestamp;
        return $"{Name} ({Type}) [{status}] TTL:{Ttl} Age:{(int)age.TotalSeconds}s from {Source}";
    }
}
