﻿using MassTransit.Logging;
using MassTransit.Monitoring;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics.Metrics;

namespace APIGateway.MluviiWebhook
{
    public static class OpenTelemetryCustom
    {
        public static void AddConsoleOpenTelemetry(this IServiceCollection services, IConfigurationSection configSection, WebApplicationBuilder appBuilder)
        {
            var openTelemetryOptions = configSection.Get<OpenTelemetryOptions>();
            if (openTelemetryOptions == null || string.IsNullOrEmpty(openTelemetryOptions?.UrlGrpc))
            {
                return;
            }

            // Note: Switch between Zipkin/OTLP/Console by setting UseTracingExporter in appsettings.json.
            var tracingExporter = appBuilder.Configuration.GetValue("UseTracingExporter", defaultValue: "console")!.ToLowerInvariant();

            // Note: Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.
            var metricsExporter = appBuilder.Configuration.GetValue("UseMetricsExporter", defaultValue: "console")!.ToLowerInvariant();

            // Note: Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
            var logExporter = appBuilder.Configuration.GetValue("UseLogExporter", defaultValue: "console")!.ToLowerInvariant();

            // Note: Switch between Explicit/Exponential by setting HistogramAggregation in appsettings.json
            var histogramAggregation = appBuilder.Configuration.GetValue("HistogramAggregation", defaultValue: "explicit")!.ToLowerInvariant();

            // Build a resource configuration action to set service information.
            Action<ResourceBuilder> configureResource = r => r.AddService(
                serviceName: appBuilder.Configuration.GetValue("ServiceName", defaultValue: "otel-test")!,
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                serviceInstanceId: Environment.MachineName);

            // Configure OpenTelemetry tracing & metrics with auto-start using the
            // AddOpenTelemetry extension from OpenTelemetry.Extensions.Hosting.
            appBuilder.Services.AddOpenTelemetry()
                .ConfigureResource(configureResource)
                .WithTracing(builder =>
                {
                    // Tracing

                    // Ensure the TracerProvider subscribes to any custom ActivitySources.
                    builder
                        .AddSource(DiagnosticHeaders.DefaultListenerName)
                        .SetSampler(new AlwaysOnSampler())
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation();

                    // Use IConfiguration binding for AspNetCore instrumentation options.
                    appBuilder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(appBuilder.Configuration.GetSection("AspNetCoreInstrumentation"));

                    switch (tracingExporter)
                    {
                        case "otlp":
                            builder.AddOtlpExporter(otlpOptions =>
                            {
                                // Use IConfiguration directly for Otlp exporter endpoint option.
                                otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
                                otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                            });
                            break;

                        default:
                            builder.AddConsoleExporter();
                            break;
                    }
                })
                .WithMetrics(builder =>
                {
                    // Metrics

                    // Ensure the MeterProvider subscribes to any custom Meters.
                    builder
#if EXPOSE_EXPERIMENTAL_FEATURES
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
#endif
                        .AddMeter(InstrumentationOptions.MeterName)
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation();

                    switch (histogramAggregation)
                    {
                        case "exponential":
                            builder.AddView(instrument =>
                            {
                                return instrument.GetType().GetGenericTypeDefinition() == typeof(Histogram<>)
                                    ? new Base2ExponentialBucketHistogramConfiguration()
                                    : null;
                            });
                            break;
                        default:
                            // Explicit bounds histogram is the default.
                            // No additional configuration necessary.
                            break;
                    }

                    switch (metricsExporter)
                    {
                        case "otlp":
                            builder.AddOtlpExporter(otlpOptions =>
                            {
                                // Use IConfiguration directly for Otlp exporter endpoint option.
                                otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
                                otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                            });
                            break;
                        default:
                            builder.AddConsoleExporter();
                            break;
                    }
                });

            // Clear default logging providers used by WebApplication host.
            appBuilder.Logging.ClearProviders();
            // Configure OpenTelemetry Logging.
            appBuilder.Logging.AddOpenTelemetry(options =>
            {
                // Note: See appsettings.json Logging:OpenTelemetry section for configuration.

                var resourceBuilder = ResourceBuilder.CreateDefault();
                configureResource(resourceBuilder);
                options.SetResourceBuilder(resourceBuilder);
                switch (logExporter)
                {
                    case "otlp":
                        options.AddOtlpExporter(otlpOptions =>
                        {
                            // Use IConfiguration directly for Otlp exporter endpoint option.
                            otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
                            otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                        });
                        break;
                    default:
                        options.AddConsoleExporter();
                        break;
                }
            });
        }
    }
}
