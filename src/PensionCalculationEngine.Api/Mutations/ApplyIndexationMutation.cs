using System.Text.Json;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;

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
        
        // Extract properties
        var percentage = GetDecimal(props, "percentage");
        var schemeIdFilter = props.TryGetValue("scheme_id", out var sid) ? GetString(props, "scheme_id") : null;
        var effectiveBeforeFilter = props.TryGetValue("effective_before", out var eb) ? GetDate(props, "effective_before") : (DateOnly?)null;

        // Filter policies
        var matchingPolicies = situation.Dossier.Policies.Where(p =>
        {
            if (schemeIdFilter is not null && p.SchemeId != schemeIdFilter)
                return false;
            if (effectiveBeforeFilter is not null && p.EmploymentStartDate >= effectiveBeforeFilter.Value)
                return false;
            return true;
        }).ToList();

        // Validation: No matching policies
        if (matchingPolicies.Count == 0 && (schemeIdFilter is not null || effectiveBeforeFilter is not null))
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Warning, "NO_MATCHING_POLICIES", 
                "Filters were provided but no policies match the criteria"));
        }

        // Apply indexation
        var updatedPolicies = new List<Policy>(situation.Dossier.Policies.Count);
        var hasNegativeSalary = false;

        foreach (var policy in situation.Dossier.Policies)
        {
            if (matchingPolicies.Contains(policy))
            {
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

    private static string GetString(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement) return jsonElement.GetString() ?? string.Empty;
            return value?.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static DateOnly GetDate(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement) return DateOnly.Parse(jsonElement.GetString()!);
            if (value is string str) return DateOnly.Parse(str);
            if (value is DateOnly date) return date;
        }
        return DateOnly.MinValue;
    }

    private static decimal GetDecimal(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement) return jsonElement.GetDecimal();
            if (value is decimal d) return d;
            if (value is double dbl) return (decimal)dbl;
            if (value is int i) return i;
            if (value is long l) return l;
        }
        return 0;
    }
}
