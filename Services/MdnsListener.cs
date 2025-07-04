using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MdnsListener.Configuration;
using MdnsListener.Interfaces;
using MdnsListener.Models;

namespace MdnsListener.Services;

/// <summary>
/// An mDNS listener that implements proper resource management,
/// follows SOLID principles, and uses dependency injection.
/// </summary>
public sealed class MdnsListener : IMdnsListener, IHostedService
{
    private readonly ILogger<MdnsListener> _logger;
    private readonly IPacketHandler _packetHandler;
    private readonly IServiceCache _cache;
    private readonly MdnsListenerOptions _options;
    
    private readonly List<UdpClient> _udpClients = [];
    private readonly SemaphoreSlim _startStopSemaphore = new(1, 1);
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event EventHandler<ServiceEventArgs>? ServiceAdvertised
    {
        add
        {
            if (_packetHandler is PacketHandler handler)
                handler.ServiceAdvertised += value;
        }
        remove
        {
            if (_packetHandler is PacketHandler handler)
                handler.ServiceAdvertised -= value;
        }
    }

    public event EventHandler<ServiceEventArgs>? ServiceRemoved
    {
        add
        {
            if (_packetHandler is PacketHandler handler)
                handler.ServiceRemoved += value;
        }
        remove
        {
            if (_packetHandler is PacketHandler handler)
                handler.ServiceRemoved -= value;
        }
    }

    public MdnsListener(
        ILogger<MdnsListener> logger,
        IPacketHandler packetHandler,
        IServiceCache cache,
        IOptions<MdnsListenerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _packetHandler = packetHandler ?? throw new ArgumentNullException(nameof(packetHandler));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _startStopSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cts is not null && !_cts.IsCancellationRequested)
            {
                _logger.LogWarning("Listener is already running");
                return;
            }

            _logger.LogInformation("Starting mDNS Listener...");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            InitializeUdpClients();

            if (_udpClients.Count == 0)
            {
                throw new InvalidOperationException(
                    "No network interfaces available to start mDNS listener. Ensure networking is enabled.");
            }

            var listenTasks = _udpClients
                .Select(client => Task.Run(() => ListenForPacketsAsync(client, _cts.Token), _cts.Token))
                .ToList();

            _logger.LogInformation("Listener started on {Count} socket(s)", _udpClients.Count);
        }
        finally
        {
            _startStopSemaphore.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _startStopSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cts is null)
            {
                return;
            }

            _logger.LogInformation("Stopping mDNS listener...");

            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }

            // Give tasks a chance to complete gracefully
            await Task.Delay(100, cancellationToken);

            DisposeUdpClients();

            _cts.Dispose();
            _cts = null;

            _logger.LogInformation("Listener stopped. Statistics: {CachedCount} services cached", _cache.Count);
        }
        finally
        {
            _startStopSemaphore.Release();
        }
    }

    public void Start()
    {
        StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Stop()
    {
        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public IReadOnlyDictionary<string, ServiceRecord> GetCachedServices()
    {
        return _cache.GetAllServices();
    }

    private void InitializeUdpClients()
    {
        InitializeUdpClient(_options.MulticastAddressIPv4, AddressFamily.InterNetwork);
        InitializeUdpClient(_options.MulticastAddressIPv6, AddressFamily.InterNetworkV6);
    }

    private void InitializeUdpClient(string address, AddressFamily family)
    {
        try
        {
            var udpClient = new UdpClient(family);
            ConfigureUdpClient(udpClient, address, family);
            _udpClients.Add(udpClient);
            
            _logger.LogDebug("Successfully initialized UDP client for {Family}", family);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
        {
            _logger.LogWarning("{Family} is not available on this system", family);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize UDP client for {Family}", family);
        }
    }

    private void ConfigureUdpClient(UdpClient udpClient, string address, AddressFamily family)
    {
        // Configure socket options for mDNS
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        
        // Platform-specific socket options
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            }
            catch { /* Ignore if not supported */ }
        }

        var localEndpoint = new IPEndPoint(
            family == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 
            _options.Port);
        
        udpClient.Client.Bind(localEndpoint);

        var multicastAddress = IPAddress.Parse(address);
        udpClient.JoinMulticastGroup(multicastAddress);

        // Set TTL for multicast packets
        udpClient.Client.SetSocketOption(
            family == AddressFamily.InterNetwork ? SocketOptionLevel.IP : SocketOptionLevel.IPv6,
            SocketOptionName.MulticastTimeToLive,
            255);
    }

    private async Task ListenForPacketsAsync(UdpClient client, CancellationToken cancellationToken)
    {
        var endpointName = client.Client.LocalEndPoint?.ToString() ?? "Unknown";
        _logger.LogInformation("Listening for packets on {Endpoint}", endpointName);

        var buffer = new byte[65535]; // Maximum UDP packet size
        var retryCount = 0;
        const int maxRetries = 5;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cancellationToken);
                
                _logger.LogTrace("Received {ByteCount} bytes from {RemoteEndpoint}", 
                    result.Buffer.Length, result.RemoteEndPoint);
                
                // Reset retry count on successful receive
                retryCount = 0;

                // Process packet asynchronously without blocking the receive loop
                _ = _packetHandler.ProcessPacketAsync(
                    result.Buffer, 
                    result.RemoteEndPoint, 
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (ObjectDisposedException)
            {
                // Socket was disposed, exit gracefully
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
            {
                // Socket was interrupted, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogError(ex, "Error receiving packet on {Endpoint} (Retry {Retry}/{MaxRetries})", 
                    endpointName, retryCount, maxRetries);

                if (retryCount >= maxRetries)
                {
                    _logger.LogError("Maximum retries exceeded for {Endpoint}, stopping listener", endpointName);
                    break;
                }

                // Exponential backoff
                var delay = Math.Min(100 * Math.Pow(2, retryCount), 5000);
                await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
            }
        }

        _logger.LogInformation("Packet listening loop stopped for {Endpoint}", endpointName);
    }

    private void DisposeUdpClients()
    {
        foreach (var client in _udpClients)
        {
            try
            {
                client.Close();
                client.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing UDP client");
            }
        }
        _udpClients.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _startStopSemaphore.Dispose();
        _disposed = true;
    }
}