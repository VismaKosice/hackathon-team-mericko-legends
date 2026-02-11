using System.Text.Json;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;

namespace PensionCalculationEngine.Api.Mutations;

public sealed class ProjectFutureBenefitsMutation : IMutation
{
    private const decimal DefaultAccrualRate = 0.02m;

    public string MutationName => "project_future_benefits";

    public MutationResult Execute(Situation situation, CalculationMutation mutation)
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
        var projectionStartDate = GetDate(props, "projection_start_date");
        var projectionEndDate = GetDate(props, "projection_end_date");
        var intervalMonths = GetInt(props, "projection_interval_months");

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
                var years = CalculateYearsOfService(policy.EmploymentStartDate, projectionDate);
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

            // Calculate total annual pension
            var annualPension = weightedAvgSalary * totalYears * DefaultAccrualRate;

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

    private static decimal CalculateYearsOfService(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            return 0;

        var days = endDate.DayNumber - startDate.DayNumber;
        return Math.Max(0, days / 365.25m);
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

    private static int GetInt(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement) return jsonElement.GetInt32();
            if (value is int intValue) return intValue;
            if (value is long longValue) return (int)longValue;
            if (value is string str && int.TryParse(str, out var parsed)) return parsed;
        }
        return 0;
    }
}
