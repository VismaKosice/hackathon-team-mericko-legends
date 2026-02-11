using FluentAssertions;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Mutations;
using PensionCalculationEngine.Api.Services;
using Xunit;

namespace PensionCalculationEngine.Tests.Mutations;

public class CalculateRetirementBenefitMutationTests
{
    private readonly CalculateRetirementBenefitMutation _mutation;

    public CalculateRetirementBenefitMutationTests()
    {
        var schemeRegistry = new SchemeRegistryService(null);
        _mutation = new CalculateRetirementBenefitMutation(schemeRegistry);
    }

    [Fact]
    public async Task Execute_WithEligibleAge_CalculatesCorrectPension()
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
            "d6666666-6666-6666-6666-666666666666",
            "calculate_retirement_benefit",
            "DOSSIER",
            new DateOnly(2025, 1, 1),
            new Dictionary<string, object>
            {
                ["retirement_date"] = "2025-01-01"
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = await _mutation.ExecuteAsync(situation, mutationData);

        // Assert
        result.UpdatedSituation.Dossier!.Status.Should().Be("RETIRED");
        result.UpdatedSituation.Dossier.RetirementDate.Should().Be(new DateOnly(2025, 1, 1));
        result.UpdatedSituation.Dossier.Policies[0].AttainablePension.Should().BeGreaterThan(0);
        result.UpdatedSituation.Dossier.Policies[1].AttainablePension.Should().BeGreaterThan(0);
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_WithInsufficientAge_ReturnsCriticalError()
    {
        // Arrange
        var dossier = new Dossier(
            "d2222222-2222-2222-2222-222222222222",
            "ACTIVE",
            null,
            new List<Person>
            {
                new("p3333333-3333-3333-3333-333333333333", "PARTICIPANT", "Jane Doe", new DateOnly(1990, 6, 15))
            },
            new List<Policy>
            {
                new("d2222222-2222-2222-2222-222222222222-1", "SCHEME-A", new DateOnly(2010, 1, 1), 50000, 1.0m)
            }
        );
        var situation = new Situation(dossier);
        var mutationData = new CalculationMutation(
            "d6666666-6666-6666-6666-666666666666",
            "calculate_retirement_benefit",
            "DOSSIER",
            new DateOnly(2025, 1, 1),
            new Dictionary<string, object>
            {
                ["retirement_date"] = "2025-01-01"
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = await _mutation.ExecuteAsync(situation, mutationData);

        // Assert
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Level.Should().Be("CRITICAL");
        result.Messages[0].Code.Should().Be("NOT_ELIGIBLE");
    }

    [Fact]
    public async Task Execute_WithRetirementBeforeEmployment_ReturnsWarning()
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
                new("d2222222-2222-2222-2222-222222222222-1", "SCHEME-A", new DateOnly(2030, 1, 1), 50000, 1.0m)
            }
        );
        var situation = new Situation(dossier);
        var mutationData = new CalculationMutation(
            "d6666666-6666-6666-6666-666666666666",
            "calculate_retirement_benefit",
            "DOSSIER",
            new DateOnly(2025, 1, 1),
            new Dictionary<string, object>
            {
                ["retirement_date"] = "2025-01-01"
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = await _mutation.ExecuteAsync(situation, mutationData);

        // Assert
        result.Messages.Should().Contain(m => m.Code == "RETIREMENT_BEFORE_EMPLOYMENT" && m.Level == "WARNING");
    }
}
