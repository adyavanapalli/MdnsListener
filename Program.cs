using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MdnsListener.Configuration;
using MdnsListener.Interfaces;
using MdnsListener.Services;

namespace MdnsListener;

/// <summary>
/// Entry point for the mDNS listener application.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Parse command line arguments
        var parser = new Parser(with => with.HelpWriter = Console.Out);
        var parserResult = parser.ParseArguments<CommandLineOptions>(args);

        return await parserResult.MapResult(
            async options => await RunWithOptionsAsync(options, args),
            _ => Task.FromResult(1) // Return error code on parse failure
        );
    }

    private static async Task<int> RunWithOptionsAsync(CommandLineOptions options, string[] args)
    {
        // Handle help request
        if (options.Help)
        {
            return 0;
        }

        var builder = Host.CreateApplicationBuilder(args);

        // Configure application settings with command line overrides
        ConfigureAppConfiguration(builder.Configuration, options);

        // Configure logging with command line log level
        ConfigureLogging(builder, options);

        // Configure services using dependency injection
        ConfigureServices(builder.Services, builder.Configuration, options);

        // Build and run the host
        using var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("MdnsListener.Program");

        try
        {
            logger.LogInformation("Starting mDNS Listener application. Press Ctrl+C to exit.");
            
            // Log active filters if any
            if (options.HasFilters() && !options.IncludeAll)
            {
                logger.LogInformation("Active filters:");
                if (options.ServiceNames.Any())
                    logger.LogInformation("  Service names: {Names}", string.Join(", ", options.ServiceNames));
                if (options.ServiceTypes.Any())
                    logger.LogInformation("  Service types: {Types}", string.Join(", ", options.ServiceTypes));
                if (options.DomainPatterns.Any())
                    logger.LogInformation("  Domain patterns: {Patterns}", string.Join(", ", options.DomainPatterns));
            }

            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Application terminated unexpectedly");
            return 1;
        }
    }

    private static void ConfigureAppConfiguration(IConfigurationBuilder configuration, CommandLineOptions options)
    {
        // Set base path to the directory where the executable is located
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();
        configuration.SetBasePath(assemblyDirectory);

        // Load custom config file if specified
        if (!string.IsNullOrEmpty(options.ConfigFile))
        {
            configuration.AddJsonFile(options.ConfigFile, optional: false, reloadOnChange: true);
        }
        else
        {
            // Make appsettings.json optional with default values
            configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", 
                    optional: true, reloadOnChange: true);
        }

        configuration.AddEnvironmentVariables();

        // Add command line options as configuration
        var commandLineConfig = new Dictionary<string, string?>();
        
        // Override filter settings if specified on command line
        if (options.HasFilters() || options.IncludeAll)
        {
            commandLineConfig[$"{MdnsListenerOptions.SectionName}:ServiceFilter:IncludeAll"] = options.IncludeAll.ToString();
            
            if (options.ServiceNames.Any())
            {
                var names = options.ServiceNames.ToList();
                for (int i = 0; i < names.Count; i++)
                {
                    commandLineConfig[$"{MdnsListenerOptions.SectionName}:ServiceFilter:ServiceNames:{i}"] = names[i];
                }
            }
            
            if (options.ServiceTypes.Any())
            {
                var types = options.ServiceTypes.ToList();
                for (int i = 0; i < types.Count; i++)
                {
                    commandLineConfig[$"{MdnsListenerOptions.SectionName}:ServiceFilter:ServiceTypes:{i}"] = types[i];
                }
            }
            
            if (options.DomainPatterns.Any())
            {
                var patterns = options.DomainPatterns.ToList();
                for (int i = 0; i < patterns.Count; i++)
                {
                    commandLineConfig[$"{MdnsListenerOptions.SectionName}:ServiceFilter:DomainPatterns:{i}"] = patterns[i];
                }
            }
        }

        configuration.AddInMemoryCollection(commandLineConfig);
    }

    private static void ConfigureLogging(HostApplicationBuilder builder, CommandLineOptions options)
    {
        // Clear default logging configuration
        builder.Logging.ClearProviders();
        
        // Add console and debug providers
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();

        // If log level is specified on command line, override all configuration
        if (Enum.TryParse<LogLevel>(options.LogLevel, true, out var logLevel))
        {
            builder.Logging.SetMinimumLevel(logLevel);
            
            // Also override specific category levels
            builder.Logging.AddFilter("MdnsListener", logLevel);
            builder.Logging.AddFilter("Microsoft", logLevel);
            builder.Logging.AddFilter("System", logLevel);
        }
        else
        {
            // Otherwise use configuration from appsettings.json
            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, CommandLineOptions options)
    {
        // Configure options
        services.Configure<MdnsListenerOptions>(
            configuration.GetSection(MdnsListenerOptions.SectionName));

        // Register command line options
        services.AddSingleton(options);

        // Register core services as singletons
        services.AddSingleton<IDnsPacketParser, SecureDnsPacketParser>();
        services.AddSingleton<IServiceFilter, ConfigurableServiceFilter>();
        
        // Register the cache as both IServiceCache and IHostedService
        services.AddSingleton<ServiceCacheWithExpiration>();
        services.AddSingleton<IServiceCache>(provider => 
            provider.GetRequiredService<ServiceCacheWithExpiration>());
        services.AddHostedService(provider => 
            provider.GetRequiredService<ServiceCacheWithExpiration>());

        // Register packet handler
        services.AddSingleton<IPacketHandler, PacketHandler>();

        // Register the listener as both IMdnsListener and IHostedService
        services.AddSingleton<Services.MdnsListener>();
        services.AddSingleton<IMdnsListener>(provider => 
            provider.GetRequiredService<Services.MdnsListener>());
        services.AddHostedService(provider => 
            provider.GetRequiredService<Services.MdnsListener>());

        // Register event handlers
        services.AddSingleton<IHostedService, MdnsEventLogger>();
    }
}