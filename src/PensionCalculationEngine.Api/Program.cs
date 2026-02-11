using System.Text.Json;
using System.Text.Json.Serialization;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;
using PensionCalculationEngine.Api.Domain;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure Kestrel for high throughput
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 1000;
    options.Limits.MaxConcurrentUpgradedConnections = 1000;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    options.AddServerHeader = false;
    options.Limits.MinRequestBodyDataRate = null; // Remove rate limiting for benchmarks
    options.Limits.MinResponseDataRate = null;
});

// Add response compression for bandwidth efficiency (disabled for latency benchmarks)
// Uncomment if network bandwidth is the bottleneck
// builder.Services.AddResponseCompression(options =>
// {
//     options.EnableForHttps = true;
//     options.Providers.Add<GzipCompressionProvider>();
//     options.MimeTypes = ["application/json"];
// });

// builder.Services.Configure<GzipCompressionProviderOptions>(options =>
// {
//     options.Level = CompressionLevel.Fastest; // Prioritize speed over size
// });

// Configure JSON options for optimal performance with source generation
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    options.SerializerOptions.WriteIndented = false;
    options.SerializerOptions.DefaultBufferSize = 32768; // Increased buffer for better throughput
    options.SerializerOptions.PropertyNameCaseInsensitive = false; // Strict matching for performance
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict; // Faster number handling
});

// Configure HTTP client for scheme registry with optimized settings
builder.Services.AddHttpClient("SchemeRegistry", client =>
{
    client.Timeout = TimeSpan.FromSeconds(2); // 2 second timeout as per requirements
})
.ConfigureHttpClient(client =>
{
    // Optimize for performance
    client.DefaultRequestVersion = new Version(2, 0); // Use HTTP/2 if available
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5), // Connection pooling
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 10, // Allow multiple concurrent connections
    EnableMultipleHttp2Connections = true
});

// Register services as singletons for better performance
builder.Services.AddSingleton<ISchemeRegistryService>(sp =>
{
    var httpClientFactory = sp.GetService<IHttpClientFactory>();
    return new SchemeRegistryService(httpClientFactory);
});

builder.Services.AddSingleton<MutationRegistry>();

// Enable JSON Patch generation (bonus feature worth 11 points)
builder.Services.AddSingleton<JsonPatchGenerator>();

builder.Services.AddSingleton<CalculationEngine>();

var app = builder.Build();

// Enable response compression  (disabled for latency benchmarks)
// app.UseResponseCompression();

// Health check endpoints
app.MapGet("/health", () => Results.Text("healthy"))
    .WithName("HealthCheck")
    .ExcludeFromDescription();

app.MapGet("/health/ready", () => Results.Text("ready"))
    .WithName("ReadinessCheck")
    .ExcludeFromDescription();

// Main calculation endpoint
app.MapPost("/calculation-requests", async (CalculationRequest request, CalculationEngine calculationEngine, CancellationToken cancellationToken) =>
{
    try
    {
        var response = await calculationEngine.ProcessCalculationRequestAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new ErrorResponse(400, "Invalid JSON format"));
    }
    catch (Exception ex)
    {
        return Results.Json(
            new ErrorResponse(500, $"Internal server error: {ex.Message}"),
            statusCode: 500
        );
    }
})
.WithName("CalculateRequest");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");

// Source generation context for AOT and performance
[JsonSerializable(typeof(CalculationRequest))]
[JsonSerializable(typeof(CalculationResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(CalculationInstructions))]
[JsonSerializable(typeof(CalculationMutation))]
[JsonSerializable(typeof(CalculationMetadata))]
[JsonSerializable(typeof(CalculationResult))]
[JsonSerializable(typeof(CalculationMessage))]
[JsonSerializable(typeof(SituationSnapshot))]
[JsonSerializable(typeof(ProcessedMutation))]
[JsonSerializable(typeof(Situation))]
[JsonSerializable(typeof(Dossier))]
[JsonSerializable(typeof(Person))]
[JsonSerializable(typeof(Policy))]
[JsonSerializable(typeof(Projection))]
[JsonSerializable(typeof(List<CalculationMessage>))]
[JsonSerializable(typeof(List<Person>))]
[JsonSerializable(typeof(List<Policy>))]
[JsonSerializable(typeof(List<Projection>))]
[JsonSerializable(typeof(List<ProcessedMutation>))]
[JsonSerializable(typeof(List<CalculationMutation>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<object>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Serialization
)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
