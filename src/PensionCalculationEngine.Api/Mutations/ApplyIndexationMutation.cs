using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;

namespace PensionCalculationEngine.Api.Mutations;

public sealed class ApplyIndexationMutation : IMutation
{
    public string MutationName => "apply_indexation";

    public MutationResult Execute(Situation situation, CalculationMutation mutation)
    {
        var messages = new List<CalculationMessage>();
        
        // Validation: Dossier does not exist
        if (situation.Dossier is null)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "DOSSIER_NOT_FOUND", "No dossier exists in the situation"));
            return new MutationResult(situation, messages);
        }

        // Validation: No policies exist
        if (situation.Dossier.Policies.Count == 0)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "NO_POLICIES", "Dossier has no policies"));
            return new MutationResult(situation, messages);
        }

        var props = mutation.MutationProperties;
        
        // Extract properties using optimized extractor
        var percentage = PropertyExtractor.GetDecimal(props, "percentage");
        var schemeIdFilter = PropertyExtractor.TryGetString(props, "scheme_id", out var sid) ? sid : null;
        var effectiveBeforeFilter = PropertyExtractor.TryGetDate(props, "effective_before", out var eb) ? eb : (DateOnly?)null;

        // Filter policies - optimized with direct indexing and single pass
        var policies = situation.Dossier.Policies;
        var policyCount = policies.Count;
        var matchingCount = 0;
        var hasNegativeSalary = false;
        var updatedPolicies = new List<Policy>(capacity: policyCount);

        for (int i = 0; i < policyCount; i++)
        {
            var policy = policies[i];
            var matches = true;
            
            if (schemeIdFilter is not null && policy.SchemeId != schemeIdFilter)
                matches = false;
            if (effectiveBeforeFilter is not null && policy.EmploymentStartDate >= effectiveBeforeFilter.Value)
                matches = false;

            if (matches)
            {
                matchingCount++;
                var newSalary = policy.Salary * (1 + percentage);
                if (newSalary < 0)
                {
                    newSalary = 0;
                    hasNegativeSalary = true;
                }
                updatedPolicies.Add(policy with { Salary = newSalary });
            }
            else
            {
                updatedPolicies.Add(policy);
            }
        }

        // Validation: No matching policies
        if (matchingCount == 0 && (schemeIdFilter is not null || effectiveBeforeFilter is not null))
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Warning, "NO_MATCHING_POLICIES", 
                "Filters were provided but no policies match the criteria"));
        }

        if (hasNegativeSalary)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Warning, "NEGATIVE_SALARY_CLAMPED", 
                "After applying the percentage, one or more salaries would be negative. Salary is clamped to 0."));
        }

        // Update dossier
        var updatedDossier = situation.Dossier with { Policies = updatedPolicies };
        var updatedSituation = new Situation(updatedDossier);

        return new MutationResult(updatedSituation, messages);
    }
}
