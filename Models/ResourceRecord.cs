namespace MdnsListener.Models;

/// <summary>
/// Represents a DNS resource record within a packet.
/// </summary>
public sealed record ResourceRecord
{
    public string Name { get; init; } = string.Empty;
    public DnsRecordType Type { get; init; }
    public ushort Class { get; init; }
    public uint Ttl { get; init; }
    public ReadOnlyMemory<byte> Data { get; init; }
}
