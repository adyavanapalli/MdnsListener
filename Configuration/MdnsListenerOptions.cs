namespace MdnsListener.Configuration;

/// <summary>
/// Configuration options for the mDNS listener, following the Options pattern.
/// </summary>
public sealed class MdnsListenerOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "MdnsListener";

    /// <summary>
    /// Gets or sets the IPv4 multicast address for mDNS.
    /// </summary>
    public string MulticastAddressIPv4 { get; set; } = "224.0.0.251";

    /// <summary>
    /// Gets or sets the IPv6 multicast address for mDNS.
    /// </summary>
    public string MulticastAddressIPv6 { get; set; } = "FF02::FB";

    /// <summary>
    /// Gets or sets the port number for mDNS communication.
    /// </summary>
    public int Port { get; set; } = 5353;

    /// <summary>
    /// Gets or sets the timeout for processing individual packets.
    /// </summary>
    public TimeSpan PacketProcessingTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the interval for checking expired cache entries.
    /// </summary>
    public TimeSpan CacheExpirationCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the service filter configuration.
    /// </summary>
    public ServiceFilterOptions ServiceFilter { get; set; } = new();
}

/// <summary>
/// Configuration options for service filtering.
/// </summary>
public sealed class ServiceFilterOptions
{
    /// <summary>
    /// Gets or sets whether to include all services without filtering.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of specific service names to include.
    /// </summary>
    public List<string> ServiceNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of service types to include.
    /// </summary>
    public List<string> ServiceTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of domain patterns to match.
    /// </summary>
    public List<string> DomainPatterns { get; set; } = [];
}