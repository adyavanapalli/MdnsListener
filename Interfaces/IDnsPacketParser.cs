using MdnsListener.Models;

namespace MdnsListener.Interfaces;

/// <summary>
/// Defines the contract for a service that parses raw byte arrays into DNS packets.
/// </summary>
public interface IDnsPacketParser
{
    /// <summary>
    /// Parses a byte buffer into a structured DNSPacket.
    /// </summary>
    /// <param name="buffer">The byte array received from the network.</param>
    /// <returns>A DNSPacket if parsing is successful; otherwise, null.</returns>
    DnsPacket? Parse(ReadOnlyMemory<byte> buffer);
}
