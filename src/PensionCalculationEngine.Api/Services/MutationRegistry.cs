using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using PensionCalculationEngine.Api.Mutations;

namespace PensionCalculationEngine.Api.Services;

public sealed class MutationRegistry
{
    private readonly FrozenDictionary<string, IMutation> _mutations;

    public MutationRegistry(ISchemeRegistryService schemeRegistry)
    {
        // FrozenDictionary provides zero-allocation lookups
        _mutations = new Dictionary<string, IMutation>
        {
            ["create_dossier"] = new CreateDossierMutation(),
            ["add_policy"] = new AddPolicyMutation(),
            ["apply_indexation"] = new ApplyIndexationMutation(),
            ["calculate_retirement_benefit"] = new CalculateRetirementBenefitMutation(schemeRegistry),
            ["project_future_benefits"] = new ProjectFutureBenefitsMutation(schemeRegistry)
        }.ToFrozenDictionary(StringComparer.Ordinal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMutation? GetMutation(string mutationName)
    {
        return _mutations.GetValueOrDefault(mutationName);
    }
}
