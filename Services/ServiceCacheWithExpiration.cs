using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MdnsListener.Configuration;
using MdnsListener.Interfaces;
using MdnsListener.Models;

namespace MdnsListener.Services;

/// <summary>
/// An enhanced service cache that implements TTL-based expiration,
/// following the Single Responsibility Principle.
/// </summary>
public sealed class ServiceCacheWithExpiration : IServiceCache, IHostedService, IDisposable
{
    private readonly ILogger<ServiceCacheWithExpiration> _logger;
    private readonly MdnsListenerOptions _options;
    private readonly ConcurrentDictionary<string, ServiceRecord> _cache = new();
    private Timer? _expirationTimer;

    public event EventHandler<ServiceEventArgs>? ServiceExpired;

    public ServiceCacheWithExpiration(
        ILogger<ServiceCacheWithExpiration> logger,
        IOptions<MdnsListenerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public int Count => _cache.Count;

    public bool AddOrUpdate(ServiceRecord record)
    {
        if (record == null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        bool isNew = false;
        _cache.AddOrUpdate(record.Name,
            addValueFactory: _ =>
            {
                isNew = true;
                return record;
            },
            updateValueFactory: (_, existing) =>
            {
                // Preserve the original timestamp if TTL is being extended
                return record with { Timestamp = existing.Timestamp };
            });

        _logger.LogTrace("Service '{Name}' {Action} in cache with TTL {Ttl}s", 
            record.Name, isNew ? "added" : "updated", record.Ttl);
        
        return isNew;
    }

    public ServiceRecord? Remove(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
        }

        if (_cache.TryRemove(serviceName, out var removedRecord))
        {
            _logger.LogTrace("Service '{Name}' removed from cache", serviceName);
            return removedRecord;
        }
        
        return null;
    }

    public IReadOnlyDictionary<string, ServiceRecord> GetAllServices()
    {
        // Return only non-expired services
        var currentTime = DateTime.UtcNow;
        return _cache
            .Where(kvp => !IsExpired(kvp.Value, currentTime))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting service cache expiration timer");
        _expirationTimer = new Timer(
            CheckExpiredServices,
            null,
            _options.CacheExpirationCheckInterval,
            _options.CacheExpirationCheckInterval);
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping service cache expiration timer");
        _expirationTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private void CheckExpiredServices(object? state)
    {
        try
        {
            var currentTime = DateTime.UtcNow;
            var expiredServices = _cache
                .Where(kvp => IsExpired(kvp.Value, currentTime))
                .ToList();

            foreach (var kvp in expiredServices)
            {
                if (_cache.TryRemove(kvp.Key, out var removedService))
                {
                    _logger.LogInformation("Service '{Name}' expired and removed from cache", kvp.Key);
                    ServiceExpired?.Invoke(this, new ServiceEventArgs(removedService));
                }
            }

            if (expiredServices.Count > 0)
            {
                _logger.LogInformation("Removed {Count} expired services from cache", expiredServices.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for expired services");
        }
    }

    private static bool IsExpired(ServiceRecord record, DateTime currentTime)
    {
        if (record.Ttl == 0) // Goodbye records don't expire
        {
            return false;
        }

        var expirationTime = record.Timestamp.AddSeconds(record.Ttl);
        return currentTime > expirationTime;
    }

    public void Dispose()
    {
        _expirationTimer?.Dispose();
        _cache.Clear();
    }
}