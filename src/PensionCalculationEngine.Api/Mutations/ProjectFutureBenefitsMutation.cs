using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;

namespace PensionCalculationEngine.Api.Mutations;

public sealed class ProjectFutureBenefitsMutation : IMutation
{
    private readonly ISchemeRegistryService _schemeRegistry;

    public ProjectFutureBenefitsMutation(ISchemeRegistryService schemeRegistry)
    {
        _schemeRegistry = schemeRegistry;
    }

    public string MutationName => "project_future_benefits";

    public async Task<MutationResult> ExecuteAsync(Situation situation, CalculationMutation mutation, CancellationToken cancellationToken = default)
    {
        var messages = new List<CalculationMessage>(capacity: 3);
        
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
        var projectionStartDate = PropertyExtractor.GetDate(props, "projection_start_date");
        var projectionEndDate = PropertyExtractor.GetDate(props, "projection_end_date");
        var intervalMonths = PropertyExtractor.GetInt(props, "projection_interval_months");

        // Generate projection dates
        var projectionDates = new List<DateOnly>();
        var currentDate = projectionStartDate;
        while (currentDate <= projectionEndDate)
        {
            projectionDates.Add(currentDate);
            currentDate = currentDate.AddMonths(intervalMonths);
        }

        var policyCount = situation.Dossier.Policies.Count;
        var updatedPolicies = new List<Policy>(capacity: policyCount);

        // For each projection date, calculate pension using the same algorithm as calculate_retirement_benefit
        // but store results in projections array instead of attainable_pension
        var allProjections = new Dictionary<int, List<Projection>>(capacity: policyCount);
        for (int i = 0; i < policyCount; i++)
        {
            allProjections[i] = new List<Projection>(capacity: projectionDates.Count);
        }

        foreach (var projectionDate in projectionDates)
        {
            // Calculate years of service for each policy up to this projection date
            var policyYears = new Dictionary<int, decimal>(capacity: policyCount);
            var totalYears = 0m;

            for (int i = 0; i < policyCount; i++)
            {
                var policy = situation.Dossier.Policies[i];
                var years = CalculationCache.CalculateYearsOfService(policy.EmploymentStartDate, projectionDate);
                policyYears[i] = years;
                totalYears += years;
            }

            // Calculate weighted average salary
            var totalWeightedSalary = 0m;
            for (int i = 0; i < policyCount; i++)
            {
                var policy = situation.Dossier.Policies[i];
                var years = policyYears[i];
                var effectiveSalary = policy.Salary * policy.PartTimeFactor;
                totalWeightedSalary += effectiveSalary * years;
            }

            var weightedAvgSalary = totalYears > 0 ? totalWeightedSalary / totalYears : 0;

            // Fetch accrual rates for all unique scheme IDs (can be cached from first iteration)
            var uniqueSchemeIds = situation.Dossier.Policies.Select(p => p.SchemeId).Distinct().ToList();
            var accrualRates = await _schemeRegistry.GetAccrualRatesAsync(uniqueSchemeIds, cancellationToken);

            // Calculate total annual pension using scheme-specific accrual rates
            var annualPension = 0m;
            for (int i = 0; i < policyCount; i++)
            {
                var policy = situation.Dossier.Policies[i];
                var years = policyYears[i];
                var effectiveSalary = policy.Salary * policy.PartTimeFactor;
                var accrualRate = accrualRates.TryGetValue(policy.SchemeId, out var rate) ? rate : 0.02m;
                annualPension += effectiveSalary * years * accrualRate;
            }

            // Distribute to each policy proportionally
            for (int i = 0; i < policyCount; i++)
            {
                var years = policyYears[i];
                var policyPension = totalYears > 0 ? annualPension * (years / totalYears) : 0;
                allProjections[i].Add(new Projection(projectionDate, policyPension));
            }
        }

        // Add projections to policies
        for (int i = 0; i < policyCount; i++)
        {
            var policy = situation.Dossier.Policies[i];
            updatedPolicies.Add(policy with { Projections = allProjections[i] });
        }

        // Update dossier with projections, keep status as ACTIVE
        var updatedDossier = situation.Dossier with 
        { 
            Policies = updatedPolicies
        };

        var updatedSituation = new Situation(updatedDossier);
        return new MutationResult(updatedSituation, messages);
    }
}
