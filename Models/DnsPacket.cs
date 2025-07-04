using System;
using System.Collections.Generic;
using System.Net;

namespace MdnsListener.Models;

/// <summary>
/// Represents a parsed DNS packet.
/// </summary>
public sealed record DnsPacket
{
    public ushort TransactionId { get; init; }
    public bool IsResponse { get; init; }
    public IReadOnlyList<ResourceRecord> AnswerRecords { get; init; } = [];
    public IReadOnlyList<ResourceRecord> AdditionalRecords { get; init; } = [];
}
