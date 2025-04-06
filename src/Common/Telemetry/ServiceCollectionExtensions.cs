using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;
using OpenTelemetry;
using System.Diagnostics;

namespace Common.Telemetry;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = Assembly.GetCallingAssembly().GetName().Name!;
        var telemetryConfig = configuration.GetSection("Telemetry");
        
        var tracesEndpoint = new Uri(telemetryConfig["TracesEndpoint"] ?? "http://localhost:4318/v1/traces");
        var metricsEndpoint = new Uri(telemetryConfig["MetricsEndpoint"] ?? "http://localhost:4318/v1/metrics");
        
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .SetSampler(new AlwaysOnSampler())
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation(options => 
                {
                    options.RecordException = true;
                    options.Filter = (httpContext) => true; 
                })
                .AddSource("MassTransit")
                .AddSource("Microsoft.AspNetCore.Hosting")
                .AddSource("Microsoft.AspNetCore.Server.Kestrel")
                .AddSource(serviceName)
                .AddOtlpExporter(e => {
                    e.Endpoint = tracesEndpoint;
                    e.Protocol = OtlpExportProtocol.HttpProtobuf;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("MassTransit")
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Routing")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddMeter("System.Runtime")
                .AddMeter("System.Net.Http")
                .AddMeter("System.Net.NameResolution")
                .AddOtlpExporter(e => {
                    e.Endpoint = metricsEndpoint;
                    e.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));
        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });
        return services;
    }
}
