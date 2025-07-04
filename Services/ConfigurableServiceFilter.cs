using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MdnsListener.Configuration;
using MdnsListener.Interfaces;

namespace MdnsListener.Services;

/// <summary>
/// A service filter that reads configuration from the Options pattern,
/// implementing the Strategy pattern for filtering logic.
/// </summary>
public sealed class ConfigurableServiceFilter : IServiceFilter
{
    private readonly ILogger<ConfigurableServiceFilter> _logger;
    private readonly ServiceFilterOptions _options;
    private readonly List<Regex> _compiledPatterns;

    public ConfigurableServiceFilter(
        ILogger<ConfigurableServiceFilter> logger,
        IOptions<MdnsListenerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value?.ServiceFilter ?? throw new ArgumentNullException(nameof(options));
        
        // Pre-compile regex patterns for better performance
        _compiledPatterns = CompilePatterns(_options.DomainPatterns);
        
        LogFilterSettings();
    }

    public bool ShouldInclude(string serviceName, string serviceType)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            _logger.LogWarning("Attempted to filter null or empty service name");
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceType))
        {
            _logger.LogWarning("Attempted to filter null or empty service type");
            return false;
        }

        if (_options.IncludeAll)
        {
            return true;
        }

        // Check exact service name matches
        if (_options.ServiceNames.Any(name => 
            serviceName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check service type matches
        if (_options.ServiceTypes.Any(type => 
            serviceType.Equals(type, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check pattern matches
        if (_compiledPatterns.Any(pattern => pattern.IsMatch(serviceName)))
        {
            return true;
        }

        return false;
    }

    private static List<Regex> CompilePatterns(List<string> patterns)
    {
        var compiledPatterns = new List<Regex>(patterns.Count);
        
        foreach (var pattern in patterns)
        {
            try
            {
                // Convert simple wildcards to regex
                var regexPattern = "^" + Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                
                compiledPatterns.Add(new Regex(
                    regexPattern, 
                    RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }
            catch (ArgumentException ex)
            {
                // Log but don't throw - skip invalid patterns
                throw new InvalidOperationException(
                    $"Invalid pattern '{pattern}' in configuration", ex);
            }
        }
        
        return compiledPatterns;
    }

    public void LogFilterSettings()
    {
        if (_options.IncludeAll)
        {
            _logger.LogInformation("Service Filter: ALL services will be processed (no filtering)");
        }
        else
        {
            _logger.LogInformation("Service Filter: Active filtering is enabled");
            
            if (_options.ServiceNames.Any())
            {
                _logger.LogInformation("  - Service Names: {Names}", 
                    string.Join(", ", _options.ServiceNames));
            }
            
            if (_options.ServiceTypes.Any())
            {
                _logger.LogInformation("  - Service Types: {Types}", 
                    string.Join(", ", _options.ServiceTypes));
            }
            
            if (_options.DomainPatterns.Any())
            {
                _logger.LogInformation("  - Domain Patterns: {Patterns}", 
                    string.Join(", ", _options.DomainPatterns));
            }
        }
    }
}