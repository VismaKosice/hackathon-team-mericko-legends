using System.Text.Json.Serialization;
using PensionCalculationEngine.Api.Domain;

namespace PensionCalculationEngine.Api.Models;

public sealed record CalculationResponse(
    [property: JsonPropertyName("calculation_metadata")] CalculationMetadata CalculationMetadata,
    [property: JsonPropertyName("calculation_result")] CalculationResult CalculationResult
);

public sealed record CalculationMetadata(
    [property: JsonPropertyName("calculation_id")] string CalculationId,
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("calculation_started_at")] DateTime CalculationStartedAt,
    [property: JsonPropertyName("calculation_completed_at")] DateTime CalculationCompletedAt,
    [property: JsonPropertyName("calculation_duration_ms")] long CalculationDurationMs,
    [property: JsonPropertyName("calculation_outcome")] string CalculationOutcome
);

public sealed record CalculationResult(
    [property: JsonPropertyName("messages")] List<CalculationMessage> Messages,
    [property: JsonPropertyName("initial_situation")] SituationSnapshot InitialSituation,
    [property: JsonPropertyName("end_situation")] SituationSnapshot EndSituation,
    [property: JsonPropertyName("mutations")] List<ProcessedMutation> Mutations
);

public sealed record CalculationMessage(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message
);

public sealed record SituationSnapshot(
    [property: JsonPropertyName("mutation_id")] string? MutationId,
    [property: JsonPropertyName("mutation_index")] int? MutationIndex,
    [property: JsonPropertyName("actual_at")] DateOnly ActualAt,
    [property: JsonPropertyName("situation")] Situation Situation
);

public sealed record ProcessedMutation(
    [property: JsonPropertyName("mutation")] CalculationMutation Mutation,
    [property: JsonPropertyName("calculation_message_indexes")] List<int> CalculationMessageIndexes,
    [property: JsonPropertyName("forward_patch_to_situation_after_this_mutation")] List<object>? ForwardPatch = null
);

public sealed record ErrorResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")] string Message
);
