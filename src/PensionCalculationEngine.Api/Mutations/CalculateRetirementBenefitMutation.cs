using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;

namespace PensionCalculationEngine.Api.Mutations;

public sealed class CalculateRetirementBenefitMutation : IMutation
{
    private const decimal DefaultAccrualRate = 0.02m;

    public string MutationName => "calculate_retirement_benefit";

    public MutationResult Execute(Situation situation, CalculationMutation mutation)
    {
        var messages = new List<CalculationMessage>(capacity: 3); // Pre-allocate for expected messages
        
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
        var retirementDate = PropertyExtractor.GetDate(props, "retirement_date");

        // Get participant birth date - use indexer for better performance
        var participant = situation.Dossier.Persons[0]; // First person is participant
        var age = CalculationCache.CalculateAge(participant.BirthDate, retirementDate);

        // Calculate total years of service
        var policyCount = situation.Dossier.Policies.Count;
        var policyYears = new Dictionary<Policy, decimal>(capacity: policyCount);
        var totalYears = 0m;

        for (int i = 0; i < policyCount; i++)
        {
            var policy = situation.Dossier.Policies[i];
            var years = CalculationCache.CalculateYearsOfService(policy.EmploymentStartDate, retirementDate);
            
            if (retirementDate < policy.EmploymentStartDate)
            {
                messages.Add(new CalculationMessage(0, MessageLevel.Warning, "RETIREMENT_BEFORE_EMPLOYMENT", 
                    $"Retirement date is before employment start date for policy {policy.PolicyId}"));
                years = 0;
            }

            policyYears[policy] = years;
            totalYears += years;
        }

        // Validation: Not eligible
        if (age < 65 && totalYears < 40)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "NOT_ELIGIBLE", 
                "Participant is under 65 years old and has less than 40 years of service"));
            return new MutationResult(situation, messages);
        }

        // Calculate weighted average salary
        var totalWeightedSalary = 0m;
        foreach (var (policy, years) in policyYears)
        {
            var effectiveSalary = policy.Salary * policy.PartTimeFactor;
            totalWeightedSalary += effectiveSalary * years;
        }

        var weightedAvgSalary = totalYears > 0 ? totalWeightedSalary / totalYears : 0;

        // Calculate annual pension
        var annualPension = weightedAvgSalary * totalYears * DefaultAccrualRate;

        // Distribute to policies
        var updatedPolicies = new List<Policy>(capacity: policyCount);
        for (int i = 0; i < policyCount; i++)
        {
            var policy = situation.Dossier.Policies[i];
            var years = policyYears[policy];
            var policyPension = totalYears > 0 ? annualPension * (years / totalYears) : 0;
            updatedPolicies.Add(policy with { AttainablePension = policyPension });
        }

        // Update dossier status
        var updatedDossier = situation.Dossier with 
        { 
            Status = DossierStatus.Retired,
            RetirementDate = retirementDate,
            Policies = updatedPolicies
        };

        var updatedSituation = new Situation(updatedDossier);
        return new MutationResult(updatedSituation, messages);
    }
}
