using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MdnsListener.Interfaces;
using MdnsListener.Models;

namespace MdnsListener.Services;

/// <summary>
/// A hosted service that subscribes to mDNS events and logs them,
/// demonstrating the Observer pattern.
/// </summary>
public sealed class MdnsEventLogger : IHostedService
{
    private readonly ILogger<MdnsEventLogger> _logger;
    private readonly IMdnsListener _listener;
    private readonly IServiceCache _cache;

    public MdnsEventLogger(
        ILogger<MdnsEventLogger> logger,
        IMdnsListener listener,
        IServiceCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting mDNS event logger");

        // Subscribe to listener events
        _listener.ServiceAdvertised += OnServiceAdvertised;
        _listener.ServiceRemoved += OnServiceRemoved;

        // Subscribe to cache expiration events if available
        if (_cache is ServiceCacheWithExpiration cacheWithExpiration)
        {
            cacheWithExpiration.ServiceExpired += OnServiceExpired;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping mDNS event logger");

        // Unsubscribe from events
        _listener.ServiceAdvertised -= OnServiceAdvertised;
        _listener.ServiceRemoved -= OnServiceRemoved;

        if (_cache is ServiceCacheWithExpiration cacheWithExpiration)
        {
            cacheWithExpiration.ServiceExpired -= OnServiceExpired;
        }

        // Log final statistics
        LogStatistics();

        return Task.CompletedTask;
    }

    private void OnServiceAdvertised(object? sender, ServiceEventArgs e)
    {
        _logger.LogInformation(
            "[SERVICE UP] {Service} | TTL: {Ttl}s | Source: {Source}",
            e.Service.Name,
            e.Service.Ttl,
            e.Service.Source);
    }

    private void OnServiceRemoved(object? sender, ServiceEventArgs e)
    {
        _logger.LogWarning(
            "[SERVICE DOWN] {Service} | Reason: {Reason} | Source: {Source}",
            e.Service.Name,
            e.Service.IsGoodbye ? "Goodbye packet" : "Manual removal",
            e.Service.Source);
    }

    private void OnServiceExpired(object? sender, ServiceEventArgs e)
    {
        _logger.LogWarning(
            "[SERVICE EXPIRED] {Service} | Age: {Age:F1}s | Last seen: {LastSeen}",
            e.Service.Name,
            (DateTime.UtcNow - e.Service.Timestamp).TotalSeconds,
            e.Service.Timestamp);
    }

    private void LogStatistics()
    {
        try
        {
            var services = _cache.GetAllServices();
            
            _logger.LogInformation(
                "Final statistics: {TotalServices} services in cache",
                services.Count);

            if (services.Any())
            {
                var servicesByType = services
                    .GroupBy(s => s.Value.Type)
                    .OrderByDescending(g => g.Count())
                    .Take(5);

                _logger.LogInformation("Top service types:");
                foreach (var group in servicesByType)
                {
                    _logger.LogInformation(
                        "  - {Type}: {Count} services",
                        group.Key,
                        group.Count());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging statistics");
        }
    }
}