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
});

// Configure JSON options for optimal performance
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    // Keep reflection for complex types
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

// Health check endpoints
app.MapGet("/health", () => Results.Text("healthy"))
    .WithName("HealthCheck");

app.MapGet("/health/ready", () => Results.Text("ready"))
    .WithName("ReadinessCheck");

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
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false
)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
