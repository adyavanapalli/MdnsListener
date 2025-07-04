using System.Net;

namespace MdnsListener.Interfaces;

/// <summary>
/// Defines the contract for handling mDNS packets.
/// </summary>
public interface IPacketHandler
{
    /// <summary>
    /// Processes a received mDNS packet asynchronously.
    /// </summary>
    /// <param name="buffer">The raw packet data.</param>
    /// <param name="source">The source endpoint of the packet.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessPacketAsync(ReadOnlyMemory<byte> buffer, IPEndPoint source, CancellationToken cancellationToken = default);
}