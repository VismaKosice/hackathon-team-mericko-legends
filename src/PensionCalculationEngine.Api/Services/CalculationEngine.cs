using System.Diagnostics;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Mutations;

namespace PensionCalculationEngine.Api.Services;

public sealed class CalculationEngine
{
    private readonly MutationRegistry _mutationRegistry;

    public CalculationEngine(MutationRegistry mutationRegistry)
    {
        _mutationRegistry = mutationRegistry;
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
                processedMutations.Add(new ProcessedMutation(mutation, [allMessages.Count - 1]));
                outcome = "FAILURE";
                break;
            }

            var result = mutationHandler.Execute(currentSituation, mutation);
            
            // Assign message IDs and track indexes
            var messageIndexes = new List<int>(capacity: result.Messages.Count);
            foreach (var msg in result.Messages)
            {
                var messageId = allMessages.Count;
                allMessages.Add(msg with { Id = messageId });
                messageIndexes.Add(messageId);
            }

            processedMutations.Add(new ProcessedMutation(mutation, messageIndexes));

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
                // Don't update currentSituation for failed mutation
                break;
            }

            // Update situation for successful mutation
            currentSituation = result.UpdatedSituation;
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
