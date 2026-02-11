using System.Text.Json;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;

namespace PensionCalculationEngine.Api.Mutations;

public sealed class AddPolicyMutation : IMutation
{
    public string MutationName => "add_policy";

    public MutationResult Execute(Situation situation, CalculationMutation mutation)
    {
        var messages = new List<CalculationMessage>(capacity: 2); // Pre-allocate for expected messages
        
        // Validation: Dossier does not exist
        if (situation.Dossier is null)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "DOSSIER_NOT_FOUND", "No dossier exists in the situation"));
            return new MutationResult(situation, messages);
        }

        var props = mutation.MutationProperties;
        
        // Extract properties
        var schemeId = GetString(props, "scheme_id");
        var employmentStartDate = GetDate(props, "employment_start_date");
        var salary = GetDecimal(props, "salary");
        var partTimeFactor = GetDecimal(props, "part_time_factor");

        // Validation: Invalid salary
        if (salary < 0)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "INVALID_SALARY", "Salary cannot be negative"));
            return new MutationResult(situation, messages);
        }

        // Validation: Invalid part_time_factor
        if (partTimeFactor < 0 || partTimeFactor > 1)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "INVALID_PART_TIME_FACTOR", "Part-time factor must be between 0 and 1"));
            return new MutationResult(situation, messages);
        }

        // Check for duplicate policy (same scheme_id and employment_start_date)
        // Use for loop for better performance
        var duplicate = false;
        var policies = situation.Dossier.Policies;
        for (int i = 0; i < policies.Count; i++)
        {
            if (policies[i].SchemeId == schemeId && policies[i].EmploymentStartDate == employmentStartDate)
            {
                duplicate = true;
                break;
            }
        }
        
        if (duplicate)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Warning, "DUPLICATE_POLICY", 
                "A policy with the same scheme_id and employment_start_date already exists"));
        }

        // Generate policy_id
        var sequenceNumber = situation.Dossier.Policies.Count + 1;
        var policyId = $"{situation.Dossier.DossierId}-{sequenceNumber}";

        // Create new policy
        var newPolicy = new Policy(
            policyId,
            schemeId,
            employmentStartDate,
            salary,
            partTimeFactor
        );

        // Update dossier - pre-allocate capacity
        var updatedPolicies = new List<Policy>(capacity: situation.Dossier.Policies.Count + 1);
        updatedPolicies.AddRange(situation.Dossier.Policies);
        updatedPolicies.Add(newPolicy);
        
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
