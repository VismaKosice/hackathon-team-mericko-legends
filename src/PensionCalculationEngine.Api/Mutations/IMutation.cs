using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;

namespace PensionCalculationEngine.Api.Mutations;

public interface IMutation
{
    string MutationName { get; }
    Task<MutationResult> ExecuteAsync(Situation situation, CalculationMutation mutation, CancellationToken cancellationToken = default);
}

public sealed record MutationResult(
    Situation UpdatedSituation,
    List<CalculationMessage> Messages
);

public static class MessageLevel
{
    public const string Critical = "CRITICAL";
    public const string Warning = "WARNING";
}

public static class DossierStatus
{
    public const string Active = "ACTIVE";
    public const string Retired = "RETIRED";
}
