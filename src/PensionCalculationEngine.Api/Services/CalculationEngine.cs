using System.Diagnostics;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Mutations;

namespace PensionCalculationEngine.Api.Services;

public sealed class CalculationEngine
{
    private readonly MutationRegistry _mutationRegistry;
    private readonly JsonPatchGenerator? _patchGenerator;
    private readonly bool _generatePatches;

    public CalculationEngine(MutationRegistry mutationRegistry, JsonPatchGenerator? patchGenerator = null)
    {
        _mutationRegistry = mutationRegistry;
        _patchGenerator = patchGenerator;
        // Only generate patches if explicitly enabled via environment variable or if generator provided
        _generatePatches = Environment.GetEnvironmentVariable("ENABLE_JSON_PATCH")?.ToLower() == "true" || patchGenerator != null;
    }

    public CalculationResponse ProcessCalculationRequest(CalculationRequest request)
    {
        var calculationId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        // Pre-allocate with expected capacity for better performance
        var mutationCount = request.CalculationInstructions.Mutations.Count;
        var allMessages = new List<CalculationMessage>(capacity: mutationCount * 2); // Assume avg 2 messages per mutation
        var processedMutations = new List<ProcessedMutation>(capacity: mutationCount);
        var currentSituation = new Situation(null);
        var outcome = "SUCCESS";

        var firstMutationDate = request.CalculationInstructions.Mutations.FirstOrDefault()?.ActualAt ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var initialSituation = new SituationSnapshot(
            null,
            null,
            firstMutationDate,
            new Situation(null)
        );

        string? lastSuccessfulMutationId = null;
        int lastSuccessfulMutationIndex = -1;
        DateOnly lastSuccessfulActualAt = firstMutationDate;
        var mutationIndex = 0;

        foreach (var mutation in request.CalculationInstructions.Mutations)
        {
            var mutationHandler = _mutationRegistry.GetMutation(mutation.MutationDefinitionName);
            if (mutationHandler is null)
            {
                var errorMsg = new CalculationMessage(
                    allMessages.Count,
                    MessageLevel.Critical,
                    "UNKNOWN_MUTATION",
                    $"Unknown mutation: {mutation.MutationDefinitionName}"
                );
                allMessages.Add(errorMsg);
                processedMutations.Add(new ProcessedMutation(mutation, [allMessages.Count - 1], null));
                outcome = "FAILURE";
                break;
            }

            // Store previous situation for patch generation (only if needed)
            var previousSituation = _generatePatches ? currentSituation : null;
            var result = mutationHandler.Execute(currentSituation, mutation);
            
            // Assign message IDs and track indexes
            var messageIndexes = new List<int>(capacity: result.Messages.Count);
            foreach (var msg in result.Messages)
            {
                var messageId = allMessages.Count;
                allMessages.Add(msg with { Id = messageId });
                messageIndexes.Add(messageId);
            }

            // Check for CRITICAL messages - use for loop for better performance
            var hasCritical = false;
            for (int i = 0; i < result.Messages.Count; i++)
            {
                if (result.Messages[i].Level == MessageLevel.Critical)
                {
                    hasCritical = true;
                    break;
                }
            }
            
            if (hasCritical)
            {
                outcome = "FAILURE";
                // Don't update currentSituation for failed mutation, no patch needed
                processedMutations.Add(new ProcessedMutation(mutation, messageIndexes, null));
                break;
            }

            // Update situation for successful mutation
            currentSituation = result.UpdatedSituation;
            
            // Generate forward patch only if enabled
            List<object>? patchOperations = null;
            if (_generatePatches && _patchGenerator != null && previousSituation != null)
            {
                var forwardPatch = _patchGenerator.GeneratePatch(previousSituation, currentSituation);
                patchOperations = forwardPatch.Cast<object>().ToList();
            }
            
            processedMutations.Add(new ProcessedMutation(mutation, messageIndexes, patchOperations));
            
            lastSuccessfulMutationId = mutation.MutationId;
            lastSuccessfulMutationIndex = mutationIndex;
            lastSuccessfulActualAt = mutation.ActualAt;
            mutationIndex++;
        }

        stopwatch.Stop();
        var endTime = DateTime.UtcNow;

        // Create end situation
        var endSituation = new SituationSnapshot(
            lastSuccessfulMutationId ?? request.CalculationInstructions.Mutations.First().MutationId,
            lastSuccessfulMutationIndex >= 0 ? lastSuccessfulMutationIndex : 0,
            lastSuccessfulActualAt,
            currentSituation
        );

        var metadata = new CalculationMetadata(
            calculationId,
            request.TenantId,
            startTime,
            endTime,
            stopwatch.ElapsedMilliseconds,
            outcome
        );

        var calculationResult = new CalculationResult(
            allMessages,
            initialSituation,
            endSituation,
            processedMutations
        );

        return new CalculationResponse(metadata, calculationResult);
    }
}
