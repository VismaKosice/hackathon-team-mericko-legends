using System.Text.Json;
using System.Text.Json.Serialization;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON options for optimal performance
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.TypeInfoResolver = AppJsonSerializerContext.Default;
});

// Register services as singletons for better performance
builder.Services.AddSingleton<MutationRegistry>();
builder.Services.AddSingleton<CalculationEngine>();

var app = builder.Build();

// Health check endpoints
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", timestamp = DateTime.UtcNow }))
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
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
