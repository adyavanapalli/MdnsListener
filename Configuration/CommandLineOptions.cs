using CommandLine;

namespace MdnsListener.Configuration;

/// <summary>
/// Command line options for the mDNS Listener application.
/// </summary>
public sealed class CommandLineOptions
{
    /// <summary>
    /// Gets or sets the specific service names to filter for.
    /// </summary>
    [Option("service-name", Separator = ' ', HelpText = "Specific service names to monitor (space-separated).")]
    public IEnumerable<string> ServiceNames { get; set; } = Enumerable.Empty<string>();

    /// <summary>
    /// Gets or sets the DNS record types to filter for.
    /// </summary>
    [Option("service-type", Separator = ' ', HelpText = "DNS record types to monitor, e.g., PTR, SRV (space-separated).")]
    public IEnumerable<string> ServiceTypes { get; set; } = Enumerable.Empty<string>();

    /// <summary>
    /// Gets or sets the domain patterns to match.
    /// </summary>
    [Option("domain-pattern", Separator = ' ', HelpText = "Domain patterns to match, supports wildcards (space-separated).")]
    public IEnumerable<string> DomainPatterns { get; set; } = Enumerable.Empty<string>();

    /// <summary>
    /// Gets or sets whether to include all services without filtering.
    /// </summary>
    [Option("all", Default = false, HelpText = "Monitor all services without filtering (overrides other filters).")]
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the logging level.
    /// </summary>
    [Option("log-level", Default = "Information", HelpText = "Set the logging level (Trace, Debug, Information, Warning, Error, Critical).")]
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets the configuration file path.
    /// </summary>
    [Option('c', "config", HelpText = "Path to custom configuration file.")]
    public string? ConfigFile { get; set; }

    /// <summary>
    /// Gets or sets whether to display help.
    /// </summary>
    [Option('h', "help", Default = false, HelpText = "Display help information.")]
    public bool Help { get; set; }

    /// <summary>
    /// Determines if any filters have been specified.
    /// </summary>
    public bool HasFilters()
    {
        return ServiceNames.Any() || ServiceTypes.Any() || DomainPatterns.Any();
    }
}