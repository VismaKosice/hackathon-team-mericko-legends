using System.Text.Json;
using System.Text.Json.Serialization;

namespace PensionCalculationEngine.Tests.TestData;

public class TestCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("request")]
    public JsonElement Request { get; set; }

    [JsonPropertyName("expected")]
    public ExpectedResult Expected { get; set; } = new();

    public override string ToString() => $"{Id}: {Name}";
}

public class ExpectedResult
{
    [JsonPropertyName("http_status")]
    public int HttpStatus { get; set; }

    [JsonPropertyName("calculation_outcome")]
    public string CalculationOutcome { get; set; } = string.Empty;

    [JsonPropertyName("message_count")]
    public int MessageCount { get; set; }

    [JsonPropertyName("messages")]
    public List<TestMessage> Messages { get; set; } = new();

    [JsonPropertyName("mutations_processed_count")]
    public int MutationsProcessedCount { get; set; }

    [JsonPropertyName("end_situation_mutation_id")]
    public string? EndSituationMutationId { get; set; }

    [JsonPropertyName("end_situation_mutation_index")]
    public int EndSituationMutationIndex { get; set; }

    [JsonPropertyName("end_situation_actual_at")]
    public string? EndSituationActualAt { get; set; }

    [JsonPropertyName("end_situation")]
    public JsonElement? EndSituation { get; set; }
}

public class TestMessage
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    public override string ToString() => $"{Level}/{Code}";
}


