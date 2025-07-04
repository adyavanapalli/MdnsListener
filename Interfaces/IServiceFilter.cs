namespace MdnsListener.Interfaces;

/// <summary>
/// Defines the contract for filtering mDNS service records.
/// </summary>
public interface IServiceFilter
{
    /// <summary>
    /// Determines whether a service record should be processed based on filter rules.
    /// </summary>
    /// <param name="serviceName">The name of the service (e.g., "MyDevice._http._tcp.local").</param>
    /// <param name="serviceType">The DNS record type (e.g., "A", "PTR", "SRV").</param>
    /// <returns>True if the service should be included; otherwise, false.</returns>
    bool ShouldInclude(string serviceName, string serviceType);

    /// <summary>
    /// Writes the current filter configuration to the log.
    /// </summary>
    void LogFilterSettings();
}
