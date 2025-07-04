using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MdnsListener.Configuration;
using MdnsListener.Interfaces;
using MdnsListener.Models;

namespace MdnsListener.Services;

/// <summary>
/// Handles the processing of mDNS packets, implementing separation of concerns
/// and the Single Responsibility Principle.
/// </summary>
public sealed class PacketHandler : IPacketHandler
{
    private readonly ILogger<PacketHandler> _logger;
    private readonly IDnsPacketParser _parser;
    private readonly IServiceCache _cache;
    private readonly IServiceFilter _filter;
    private readonly MdnsListenerOptions _options;
    
    public event EventHandler<ServiceEventArgs>? ServiceAdvertised;
    public event EventHandler<ServiceEventArgs>? ServiceRemoved;

    public PacketHandler(
        ILogger<PacketHandler> logger,
        IDnsPacketParser parser,
        IServiceCache cache,
        IServiceFilter filter,
        IOptions<MdnsListenerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task ProcessPacketAsync(ReadOnlyMemory<byte> buffer, IPEndPoint source, CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0)
        {
            _logger.LogWarning("Received empty packet from {Source}", source);
            return;
        }

        if (buffer.Length > 65535) // Maximum UDP packet size
        {
            _logger.LogWarning("Received oversized packet ({Size} bytes) from {Source}", buffer.Length, source);
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.PacketProcessingTimeout);

            await Task.Run(() => ProcessPacketCore(buffer, source), timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Packet processing timed out for packet from {Source}", source);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing packet from {Source}", source);
        }
    }

    private void ProcessPacketCore(ReadOnlyMemory<byte> buffer, IPEndPoint source)
    {
        var packet = _parser.Parse(buffer);
        if (packet is null)
        {
            _logger.LogWarning("Failed to parse DNS packet from {Source}", source);
            return;
        }

        var allRecords = packet.AnswerRecords.Concat(packet.AdditionalRecords);

        foreach (var record in allRecords)
        {
            ProcessRecord(record, source);
        }
    }

    private void ProcessRecord(ResourceRecord record, IPEndPoint source)
    {
        var recordTypeName = GetRecordTypeName(record.Type);

        if (!_filter.ShouldInclude(record.Name, recordTypeName))
        {
            _logger.LogTrace("Filtered out record: {Name} ({Type})", record.Name, recordTypeName);
            return;
        }

        var serviceRecord = CreateServiceRecord(record, recordTypeName, source);

        if (serviceRecord.IsGoodbye)
        {
            ProcessGoodbyeRecord(serviceRecord);
        }
        else
        {
            ProcessAdvertisementRecord(serviceRecord);
        }
    }

    private static string GetRecordTypeName(DnsRecordType type)
    {
        return Enum.IsDefined(typeof(DnsRecordType), type) 
            ? type.ToString() 
            : $"TYPE{(ushort)type}";
    }

    private static ServiceRecord CreateServiceRecord(ResourceRecord record, string recordTypeName, IPEndPoint source)
    {
        return new ServiceRecord
        {
            Name = record.Name,
            Type = recordTypeName,
            Data = record.Data,
            Ttl = record.Ttl,
            Source = source,
            Timestamp = DateTime.UtcNow,
            IsGoodbye = record.Ttl == 0
        };
    }

    private void ProcessGoodbyeRecord(ServiceRecord serviceRecord)
    {
        var removed = _cache.Remove(serviceRecord.Name);
        if (removed != null)
        {
            _logger.LogInformation("Service removed via goodbye packet: {Name}", serviceRecord.Name);
            ServiceRemoved?.Invoke(this, new ServiceEventArgs(serviceRecord));
        }
        else
        {
            _logger.LogDebug("Received goodbye for non-cached service: {Name}", serviceRecord.Name);
        }
    }

    private void ProcessAdvertisementRecord(ServiceRecord serviceRecord)
    {
        bool isNew = _cache.AddOrUpdate(serviceRecord);
        if (isNew)
        {
            _logger.LogInformation("New service advertised: {Name}", serviceRecord.Name);
        }
        else
        {
            _logger.LogDebug("Service updated: {Name}", serviceRecord.Name);
        }
        ServiceAdvertised?.Invoke(this, new ServiceEventArgs(serviceRecord));
    }
}