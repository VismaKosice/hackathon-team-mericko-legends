using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;

namespace PensionCalculationEngine.Api.Mutations;

public sealed class AddPolicyMutation : IMutation
{
    public string MutationName => "add_policy";

    public Task<MutationResult> ExecuteAsync(Situation situation, CalculationMutation mutation, CancellationToken cancellationToken = default)
    {
        var messages = new List<CalculationMessage>(capacity: 2); // Pre-allocate for expected messages
        
        // Validation: Dossier does not exist
        if (situation.Dossier is null)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "DOSSIER_NOT_FOUND", "No dossier exists in the situation"));
            return Task.FromResult(new MutationResult(situation, messages));
        }

        var props = mutation.MutationProperties;
        
        // Extract properties using optimized extractor
        var schemeId = PropertyExtractor.GetString(props, "scheme_id");
        var employmentStartDate = PropertyExtractor.GetDate(props, "employment_start_date");
        var salary = PropertyExtractor.GetDecimal(props, "salary");
        var partTimeFactor = PropertyExtractor.GetDecimal(props, "part_time_factor");

        // Validation: Invalid salary
        if (salary < 0)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "INVALID_SALARY", "Salary cannot be negative"));
            return Task.FromResult(new MutationResult(situation, messages));
        }

        // Validation: Invalid part_time_factor
        if (partTimeFactor < 0 || partTimeFactor > 1)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "INVALID_PART_TIME_FACTOR", "Part-time factor must be between 0 and 1"));
            return Task.FromResult(new MutationResult(situation, messages));
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

        // Generate policy_id - use string interpolation which is optimized by compiler
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

        // Update dossier - use collection expression for efficiency
        var updatedPolicies = new List<Policy>(capacity: situation.Dossier.Policies.Count + 1);
        updatedPolicies.AddRange(situation.Dossier.Policies);
        updatedPolicies.Add(newPolicy);
        
        var updatedDossier = situation.Dossier with { Policies = updatedPolicies };
        var updatedSituation = new Situation(updatedDossier);

        return Task.FromResult(new MutationResult(updatedSituation, messages));
    }
}
