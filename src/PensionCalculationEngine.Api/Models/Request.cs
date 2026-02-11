using System.Text.Json.Serialization;

namespace PensionCalculationEngine.Api.Models;

public sealed record CalculationRequest(
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("calculation_instructions")] CalculationInstructions CalculationInstructions
);

public sealed record CalculationInstructions(
    [property: JsonPropertyName("mutations")] List<CalculationMutation> Mutations
);

public record CalculationMutation(
    [property: JsonPropertyName("mutation_id")] string MutationId,
    [property: JsonPropertyName("mutation_definition_name")] string MutationDefinitionName,
    [property: JsonPropertyName("mutation_type")] string MutationType,
    [property: JsonPropertyName("actual_at")] DateOnly ActualAt,
    [property: JsonPropertyName("mutation_properties")] Dictionary<string, object> MutationProperties,
    [property: JsonPropertyName("dossier_id")] string? DossierId = null
);

