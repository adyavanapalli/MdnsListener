using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MdnsListener.Configuration;
using MdnsListener.Models;
using MdnsListener.Services;
using System.Net;

namespace MdnsListener.Tests;

public class ServiceCacheWithExpirationTests
{
    private readonly Mock<ILogger<ServiceCacheWithExpiration>> _loggerMock;
    private readonly ServiceCacheWithExpiration _cache;
    private readonly MdnsListenerOptions _options;

    public ServiceCacheWithExpirationTests()
    {
        _loggerMock = new Mock<ILogger<ServiceCacheWithExpiration>>();
        _options = new MdnsListenerOptions
        {
            CacheExpirationCheckInterval = TimeSpan.FromMilliseconds(100)
        };
        var optionsMock = new Mock<IOptions<MdnsListenerOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);
        
        _cache = new ServiceCacheWithExpiration(_loggerMock.Object, optionsMock.Object);
    }

    [Fact]
    public void AddOrUpdate_WithNewService_ReturnsTrue()
    {
        // Arrange
        var service = CreateServiceRecord("test.local", 120);

        // Act
        var result = _cache.AddOrUpdate(service);

        // Assert
        Assert.True(result);
        Assert.Equal(1, _cache.Count);
    }

    [Fact]
    public void AddOrUpdate_WithExistingService_ReturnsFalse()
    {
        // Arrange
        var service1 = CreateServiceRecord("test.local", 120);
        var service2 = CreateServiceRecord("test.local", 240);

        // Act
        _cache.AddOrUpdate(service1);
        var result = _cache.AddOrUpdate(service2);

        // Assert
        Assert.False(result);
        Assert.Equal(1, _cache.Count);
    }

    [Fact]
    public void Remove_ExistingService_ReturnsService()
    {
        // Arrange
        var service = CreateServiceRecord("test.local", 120);
        _cache.AddOrUpdate(service);

        // Act
        var removed = _cache.Remove("test.local");

        // Assert
        Assert.NotNull(removed);
        Assert.Equal("test.local", removed.Name);
        Assert.Equal(0, _cache.Count);
    }

    [Fact]
    public void Remove_NonExistentService_ReturnsNull()
    {
        // Act
        var removed = _cache.Remove("nonexistent.local");

        // Assert
        Assert.Null(removed);
    }

    [Fact]
    public void GetAllServices_ExcludesExpiredServices()
    {
        // Arrange
        var activeService = CreateServiceRecord("active.local", 120);
        var expiredService = CreateServiceRecord("expired.local", 1, DateTime.UtcNow.AddSeconds(-2));
        
        _cache.AddOrUpdate(activeService);
        _cache.AddOrUpdate(expiredService);

        // Act
        var services = _cache.GetAllServices();

        // Assert
        Assert.Single(services);
        Assert.Equal("active.local", services.First().Key);
    }

    [Fact]
    public async Task StartAsync_EnablesExpirationTimer()
    {
        // Arrange
        var expiredService = CreateServiceRecord("expired.local", 1, DateTime.UtcNow.AddSeconds(-2));
        _cache.AddOrUpdate(expiredService);

        ServiceEventArgs? expiredEventArgs = null;
        _cache.ServiceExpired += (sender, args) => expiredEventArgs = args;

        // Act
        await _cache.StartAsync(CancellationToken.None);
        
        // Wait for timer to fire with retry logic
        var maxWaitTime = TimeSpan.FromSeconds(2);
        var checkInterval = TimeSpan.FromMilliseconds(50);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (expiredEventArgs == null && stopwatch.Elapsed < maxWaitTime)
        {
            await Task.Delay(checkInterval);
        }

        // Assert
        Assert.NotNull(expiredEventArgs);
        Assert.Equal("expired.local", expiredEventArgs.Service.Name);
        Assert.Equal(0, _cache.Count);

        // Cleanup
        await _cache.StopAsync(CancellationToken.None);
    }

    private static ServiceRecord CreateServiceRecord(string name, uint ttl, DateTime? timestamp = null)
    {
        return new ServiceRecord
        {
            Name = name,
            Type = "PTR",
            Data = ReadOnlyMemory<byte>.Empty,
            Ttl = ttl,
            Source = new IPEndPoint(IPAddress.Loopback, 5353),
            Timestamp = timestamp ?? DateTime.UtcNow,
            IsGoodbye = false
        };
    }
}