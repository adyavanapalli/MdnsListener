# mDNS Listener

<p align="center">
  <img src="assets/logo.png" alt="mDNS Listener Logo" width="350"/>
</p>

<p align="center">
A high-performance, production-ready multicast DNS (mDNS) listener built with .NET, designed to discover and monitor services on the local network using the mDNS protocol (RFC 6762).
</p>

## Features

- **Dual-Stack Support**: Simultaneous IPv4 and IPv6 multicast listening
- **Real-Time Service Discovery**: Automatic detection of services advertised via mDNS
- **TTL-Based Expiration**: Automatic removal of expired services based on DNS TTL values
- **Configurable Filtering**: Filter services by name, type, or pattern matching
- **Graceful Shutdown**: Proper resource cleanup and cancellation support
- **Security Hardened**: Input validation and protection against malformed packets
- **Event-Driven Architecture**: Subscribe to service advertisement and removal events
- **High Performance**: Zero-allocation parsing with efficient memory usage
- **Resilient**: Built-in retry logic and timeout handling for network operations

## Requirements

- .NET 10.0 or later
- Network access to multicast addresses (224.0.0.251 for IPv4, FF02::FB for IPv6)
- Administrator/root privileges may be required on some systems for binding to port 5353

## Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd MdnsListener
```

2. Build the project:
```bash
dotnet build
```

## Usage

### Basic Usage

Run the listener with default settings:
```bash
dotnet run
```

### Command Line Options

The application supports various command line options for filtering and configuration:

```bash
# Filter by specific service names
dotnet run -- --service-name "MyPrinter._ipp._tcp.local" "MyTV._airplay._tcp.local"

# Filter by DNS record types
dotnet run -- --service-type PTR SRV TXT

# Filter by domain patterns (supports wildcards)
dotnet run -- --domain-pattern "*.local" "*._tcp.local"

# Combine multiple filters
dotnet run -- --service-type PTR --domain-pattern "*._http._tcp.local"

# Monitor all services (no filtering)
dotnet run -- --all

# Set custom log level
dotnet run -- --log-level Debug
dotnet run -- --log-level Trace  # Most verbose

# Use custom configuration file
dotnet run -- --config /path/to/custom-config.json

# Display help
dotnet run -- --help
```

#### Available Options

- `--service-name <NAME1> <NAME2> ...` - Filter by specific service names (space-separated)
- `--service-type <TYPE1> <TYPE2> ...` - Filter by DNS record types like PTR, SRV, TXT (space-separated)
- `--domain-pattern <PATTERN1> <PATTERN2> ...` - Filter by domain patterns with wildcard support (space-separated)
- `--all` - Monitor all services without filtering (overrides other filters)
- `--log-level <LEVEL>` - Set logging level: Trace, Debug, Information, Warning, Error, Critical
- `-c, --config <PATH>` - Use custom configuration file instead of appsettings.json
- `-h, --help` - Display help information

**Note**: Command line filters override any filters configured in appsettings.json.

### Bash Completion

The application includes bash completion support. To enable it:

```bash
# For current session only
source completion/mdnslistener-completion.bash

# Or install permanently for current user
cp completion/mdnslistener-completion.bash ~/.local/share/bash-completion/completions/mdnslistener
```

See [completion/README.md](completion/README.md) for detailed installation instructions.

### Configuration

The application uses `appsettings.json` for configuration. Create or modify this file to customize behavior:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "MdnsListener": {
    "MulticastAddressIPv4": "224.0.0.251",
    "MulticastAddressIPv6": "FF02::FB",
    "Port": 5353,
    "PacketProcessingTimeout": "00:00:05",
    "CacheExpirationCheckInterval": "00:00:30",
    "ServiceFilter": {
      "IncludeAll": true,
      "ServiceNames": [],
      "ServiceTypes": [],
      "DomainPatterns": []
    }
  }
}
```

#### Configuration Options

- **MulticastAddressIPv4**: IPv4 multicast address for mDNS (default: 224.0.0.251)
- **MulticastAddressIPv6**: IPv6 multicast address for mDNS (default: FF02::FB)
- **Port**: mDNS port (default: 5353)
- **PacketProcessingTimeout**: Maximum time to process a single packet
- **CacheExpirationCheckInterval**: How often to check for expired services
- **ServiceFilter**: Configure which services to track
  - **IncludeAll**: Process all services (true) or only filtered ones (false)
  - **ServiceNames**: Specific service names to include (e.g., ["MyPrinter._ipp._tcp.local"])
  - **ServiceTypes**: DNS record types to include (e.g., ["PTR", "SRV"])
  - **DomainPatterns**: Wildcard patterns for domain matching (e.g., ["*.local", "*._tcp.local"])

### Environment-Specific Configuration

Use environment-specific configuration files:
```bash
DOTNET_ENVIRONMENT=Development dotnet run
```

This will load `appsettings.Development.json` in addition to the base configuration.

## Architecture

The application follows SOLID principles and uses modern .NET patterns:

### Project Structure

```
MdnsListener/
├── Configuration/     # Configuration classes (Options pattern)
├── Interfaces/        # Interface contracts
├── Models/            # Domain models and data structures
├── Services/          # Service implementations
├── Program.cs         # Application entry point
└── appsettings.json   # Configuration file
```

### Key Components

- **MdnsListener**: Manages UDP clients and packet reception
- **PacketHandler**: Processes incoming mDNS packets
- **SecureDnsPacketParser**: Parses DNS packets with security validations
- **ServiceCacheWithExpiration**: Stores discovered services with TTL management
- **ConfigurableServiceFilter**: Filters services based on configuration
- **MdnsEventLogger**: Logs service discovery events

### Design Patterns

- **Dependency Injection**: All components use constructor injection
- **Options Pattern**: Configuration management via IOptions<T>
- **Observer Pattern**: Event-driven notifications for service changes
- **Repository Pattern**: Service cache for data storage
- **Hosted Service**: Background service lifecycle management

## Service Discovery Examples

The listener will automatically discover and log services like:

```
[SERVICE UP] MyPrinter._ipp._tcp.local | TTL: 120s | Source: 192.168.1.100:5353
[SERVICE UP] Living Room TV._airplay._tcp.local | TTL: 4500s | Source: 192.168.1.105:5353
[SERVICE DOWN] Old Device._http._tcp.local | Reason: Goodbye packet | Source: 192.168.1.50:5353
[SERVICE EXPIRED] Temp Service._workstation._tcp.local | Age: 125.3s | Last seen: 2024-01-10 15:30:45
```

## Security Considerations

- **Input Validation**: All network input is validated with bounds checking
- **Packet Size Limits**: Maximum packet sizes are enforced
- **Compression Attack Prevention**: Protection against DNS compression pointer loops
- **Resource Limits**: Maximum number of resource records per packet
- **Timeout Protection**: All packet processing has configurable timeouts

## Performance

- **Zero-Allocation Parsing**: Uses ref structs for efficient memory usage
- **Concurrent Processing**: Packets are processed asynchronously
- **Pre-Compiled Patterns**: Regex patterns are compiled once at startup
- **Efficient Caching**: Thread-safe concurrent dictionary for service storage

## Troubleshooting

### Common Issues

1. **Permission Denied**: On some systems, binding to port 5353 requires elevated privileges
   - Solution: Run with `sudo` on Linux/macOS or as Administrator on Windows

2. **No Services Discovered**: Firewall may be blocking multicast traffic
   - Solution: Allow UDP port 5353 and multicast addresses in firewall rules

3. **IPv6 Not Working**: IPv6 may be disabled on the system
   - Solution: The application will continue with IPv4 only (check logs)

### Debug Logging

Enable debug logging for detailed information:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "MdnsListener": "Trace"
    }
  }
}
```

## Contributing

Contributions are welcome! Please ensure:

1. Code follows C# coding conventions
2. All public APIs have XML documentation
3. New features include appropriate logging
4. Changes maintain backward compatibility
5. Security considerations are addressed

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## References

- [RFC 6762 - Multicast DNS](https://datatracker.ietf.org/doc/html/rfc6762)
- [RFC 1035 - Domain Names - Implementation and Specification](https://datatracker.ietf.org/doc/html/rfc1035)
- [Zero Configuration Networking](http://www.zeroconf.org/)
