using MdnsListener.Models;

namespace MdnsListener.Interfaces;

/// <summary>
/// Defines the contract for a cache that stores discovered mDNS service records.
/// </summary>
public interface IServiceCache
{
    /// <summary>
    /// Adds or updates a service record in the cache.
    /// </summary>
    /// <param name="record">The service record to cache.</param>
    /// <returns>True if the service was new, false if it was an update.</returns>
    bool AddOrUpdate(ServiceRecord record);

    /// <summary>
    /// Removes a service record from the cache.
    /// </summary>
    /// <param name="serviceName">The unique name of the service to remove.</param>
    /// <returns>The removed service record, if it existed.</returns>
    ServiceRecord? Remove(string serviceName);

    /// <summary>
    /// Gets a read-only snapshot of all services currently in the cache.
    /// </summary>
    IReadOnlyDictionary<string, ServiceRecord> GetAllServices();

    /// <summary>
    /// Gets the total count of services in the cache.
    /// </summary>
    int Count { get; }
}
