using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;

namespace PensionCalculationEngine.Api.Mutations;

public interface IMutation
{
    string MutationName { get; }
    MutationResult Execute(Situation situation, CalculationMutation mutation);
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
