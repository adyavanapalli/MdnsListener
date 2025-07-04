namespace MdnsListener.Models;

/// <summary>
/// Provides data for service-related events.
/// </summary>
public class ServiceEventArgs(ServiceRecord service) : EventArgs
{
    public ServiceRecord Service { get; } = service;
}
