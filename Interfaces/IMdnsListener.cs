using System;
using System.Collections.Generic;
using System.Net;

using MdnsListener.Models;

namespace MdnsListener.Interfaces;

/// <summary>
/// Defines the contract for a service that listens for mDNS packets on the network.
/// </summary>
public interface IMdnsListener : IDisposable
{
    /// <summary>
    /// Fired when a new service is advertised or an existing one is updated.
    /// </summary>
    event EventHandler<ServiceEventArgs> ServiceAdvertised;

    /// <summary>
    /// Fired when a service is removed via a "goodbye" packet (TTL=0).
    /// </summary>
    event EventHandler<ServiceEventArgs> ServiceRemoved;

    /// <summary>
    /// Starts the listener and begins monitoring for mDNS packets.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the listener and releases network resources.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets a read-only snapshot of the currently cached services.
    /// </summary>
    IReadOnlyDictionary<string, ServiceRecord> GetCachedServices();
}
