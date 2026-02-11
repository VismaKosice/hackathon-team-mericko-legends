using System.Text.Json;
using System.Text.Json.Serialization;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;

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
    options.SerializerOptions.DefaultBufferSize = 16384; // Larger buffer for better throughput
});

// Register services as singletons for better performance
builder.Services.AddSingleton<MutationRegistry>();

// Enable JSON Patch generation via environment variable or by default
var enableJsonPatch = Environment.GetEnvironmentVariable("ENABLE_JSON_PATCH")?.ToLower() != "false";
if (enableJsonPatch)
{
    builder.Services.AddSingleton<JsonPatchGenerator>();
}

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
app.MapPost("/calculation-requests", (CalculationRequest request, CalculationEngine calculationEngine) =>
{
    try
    {
        var response = calculationEngine.ProcessCalculationRequest(request);
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
[JsonSerializable(typeof(List<CalculationMessage>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization
)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
