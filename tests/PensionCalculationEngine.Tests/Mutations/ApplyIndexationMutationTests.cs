using FluentAssertions;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Mutations;
using Xunit;

namespace PensionCalculationEngine.Tests.Mutations;

public class ApplyIndexationMutationTests
{
    private readonly ApplyIndexationMutation _mutation = new();

    [Fact]
    public async Task Execute_WithNoFilters_AppliesIndexationToAllPolicies()
    {
        // Arrange
        var dossier = new Dossier(
            "d2222222-2222-2222-2222-222222222222",
            "ACTIVE",
            null,
            new List<Person>
            {
                new("p3333333-3333-3333-3333-333333333333", "PARTICIPANT", "Jane Doe", new DateOnly(1960, 6, 15))
            },
            new List<Policy>
            {
                new("d2222222-2222-2222-2222-222222222222-1", "SCHEME-A", new DateOnly(2000, 1, 1), 50000, 1.0m)
            }
        );
        var situation = new Situation(dossier);
        var mutationData = new CalculationMutation(
            "c5555555-5555-5555-5555-555555555555",
            "apply_indexation",
            "DOSSIER",
            new DateOnly(2021, 1, 1),
            new Dictionary<string, object>
            {
                ["percentage"] = 0.03
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = await _mutation.ExecuteAsync(situation, mutationData);

        // Assert
        result.UpdatedSituation.Dossier!.Policies[0].Salary.Should().Be(51500); // 50000 * 1.03
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_WithSchemeIdFilter_AppliesOnlyToMatchingPolicies()
    {
        // Arrange
        var dossier = new Dossier(
            "d2222222-2222-2222-2222-222222222222",
            "ACTIVE",
            null,
            new List<Person>
            {
                new("p3333333-3333-3333-3333-333333333333", "PARTICIPANT", "Jane Doe", new DateOnly(1960, 6, 15))
            },
            new List<Policy>
            {
                new("d2222222-2222-2222-2222-222222222222-1", "SCHEME-A", new DateOnly(2000, 1, 1), 50000, 1.0m),
                new("d2222222-2222-2222-2222-222222222222-2", "SCHEME-B", new DateOnly(2010, 1, 1), 60000, 0.8m)
            }
        );
        var situation = new Situation(dossier);
        var mutationData = new CalculationMutation(
            "c5555555-5555-5555-5555-555555555555",
            "apply_indexation",
            "DOSSIER",
            new DateOnly(2021, 1, 1),
            new Dictionary<string, object>
            {
                ["percentage"] = 0.03,
                ["scheme_id"] = "SCHEME-A"
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = await _mutation.ExecuteAsync(situation, mutationData);

        // Assert
        result.UpdatedSituation.Dossier!.Policies[0].Salary.Should().Be(51500);
        result.UpdatedSituation.Dossier!.Policies[1].Salary.Should().Be(60000); // Unchanged
    }

    [Fact]
    public async Task Execute_WithEffectiveBeforeFilter_AppliesOnlyToPoliciesBeforeDate()
    {
        // Arrange
        var dossier = new Dossier(
            "d2222222-2222-2222-2222-222222222222",
            "ACTIVE",
            null,
            new List<Person>
            {
                new("p3333333-3333-3333-3333-333333333333", "PARTICIPANT", "Jane Doe", new DateOnly(1960, 6, 15))
            },
            new List<Policy>
            {
                new("d2222222-2222-2222-2222-222222222222-1", "SCHEME-A", new DateOnly(2000, 1, 1), 50000, 1.0m),
                new("d2222222-2222-2222-2222-222222222222-2", "SCHEME-A", new DateOnly(2015, 1, 1), 60000, 0.8m)
            }
        );
        var situation = new Situation(dossier);
        var mutationData = new CalculationMutation(
            "c5555555-5555-5555-5555-555555555555",
            "apply_indexation",
            "DOSSIER",
            new DateOnly(2021, 1, 1),
            new Dictionary<string, object>
            {
                ["percentage"] = 0.03,
                ["effective_before"] = "2010-01-01"
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = await _mutation.ExecuteAsync(situation, mutationData);

        // Assert
        result.UpdatedSituation.Dossier!.Policies[0].Salary.Should().Be(51500);
        result.UpdatedSituation.Dossier!.Policies[1].Salary.Should().Be(60000); // Unchanged
    }

    [Fact]
    public async Task Execute_WithNegativePercentage_ClampsSalaryToZero()
    {
        // Arrange
        var dossier = new Dossier(
            "d2222222-2222-2222-2222-222222222222",
            "ACTIVE",
            null,
            new List<Person>
            {
                new("p3333333-3333-3333-3333-333333333333", "PARTICIPANT", "Jane Doe", new DateOnly(1960, 6, 15))
            },
            new List<Policy>
            {
                new("d2222222-2222-2222-2222-222222222222-1", "SCHEME-A", new DateOnly(2000, 1, 1), 50000, 1.0m)
            }
        );
        var situation = new Situation(dossier);
        var mutationData = new CalculationMutation(
            "c5555555-5555-5555-5555-555555555555",
            "apply_indexation",
            "DOSSIER",
            new DateOnly(2021, 1, 1),
            new Dictionary<string, object>
            {
                ["percentage"] = -1.5 // Would make salary negative
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = await _mutation.ExecuteAsync(situation, mutationData);

        // Assert
        result.UpdatedSituation.Dossier!.Policies[0].Salary.Should().Be(0);
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Code.Should().Be("NEGATIVE_SALARY_CLAMPED");
        result.Messages[0].Level.Should().Be("WARNING");
    }
}
