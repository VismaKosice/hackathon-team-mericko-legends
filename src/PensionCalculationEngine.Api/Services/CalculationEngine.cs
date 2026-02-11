using System.Diagnostics;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Mutations;

namespace PensionCalculationEngine.Api.Services;

public sealed class CalculationEngine
{
    private readonly MutationRegistry _mutationRegistry;
    private readonly JsonPatchGenerator? _patchGenerator;

    public CalculationEngine(MutationRegistry mutationRegistry, JsonPatchGenerator? patchGenerator = null)
    {
        _mutationRegistry = mutationRegistry;
        _patchGenerator = patchGenerator;
    }

    public async Task<CalculationResponse> ProcessCalculationRequestAsync(CalculationRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow; // Cache to avoid multiple calls
        var calculationId = Guid.NewGuid().ToString();
        var startTime = now;
        var stopwatch = Stopwatch.StartNew();

        // Pre-allocate with expected capacity for better performance
        var mutations = request.CalculationInstructions.Mutations;
        var mutationCount = mutations.Count;
        var allMessages = new List<CalculationMessage>(capacity: mutationCount * 2); // Assume avg 2 messages per mutation
        var processedMutations = new List<ProcessedMutation>(capacity: mutationCount);
        var currentSituation = new Situation(null);
        var outcome = StringPool.Success;

        // Avoid LINQ FirstOrDefault - direct array access
        var firstMutationDate = mutationCount > 0 ? mutations[0].ActualAt : DateOnly.FromDateTime(now);
        var initialSituation = new SituationSnapshot(
            null,
            null,
            firstMutationDate,
            new Situation(null)
        );

        string? lastSuccessfulMutationId = null;
        int lastSuccessfulMutationIndex = -1;
        DateOnly lastSuccessfulActualAt = firstMutationDate;

        // Use for loop instead of foreach to avoid enumerator allocation
        for (int mutationIndex = 0; mutationIndex < mutationCount; mutationIndex++)
        {
            var mutation = mutations[mutationIndex];
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
                outcome = StringPool.Failure;
                break;
            }

            // Store previous situation for patch generation (only if needed)
            var previousSituation = _patchGenerator != null ? currentSituation : null;
            var result = await mutationHandler.ExecuteAsync(currentSituation, mutation, cancellationToken);
            
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
                outcome = StringPool.Failure;
                // Don't update currentSituation for failed mutation, no patch needed
                processedMutations.Add(new ProcessedMutation(mutation, messageIndexes, null, null));
                break;
            }

            // Update situation for successful mutation
            currentSituation = result.UpdatedSituation;
            
            // Generate forward and backward patches if generator is available
            List<object>? forwardPatch = null;
            List<object>? backwardPatch = null;
            
            if (_patchGenerator != null && previousSituation != null)
            {
                var forwardOps = _patchGenerator.GeneratePatch(previousSituation, currentSituation);
                forwardPatch = forwardOps.Cast<object>().ToList();
                
                // Generate backward patch (reverse direction)
                var backwardOps = _patchGenerator.GeneratePatch(currentSituation, previousSituation);
                backwardPatch = backwardOps.Cast<object>().ToList();
            }
            
            processedMutations.Add(new ProcessedMutation(mutation, messageIndexes, forwardPatch, backwardPatch));
            
            lastSuccessfulMutationId = mutation.MutationId;
            lastSuccessfulMutationIndex = mutationIndex;
            lastSuccessfulActualAt = mutation.ActualAt;
        }

        stopwatch.Stop();
        var endTime = DateTime.UtcNow;

        // Create end situation - avoid LINQ
        var endSituation = new SituationSnapshot(
            lastSuccessfulMutationId ?? mutations[0].MutationId,
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
