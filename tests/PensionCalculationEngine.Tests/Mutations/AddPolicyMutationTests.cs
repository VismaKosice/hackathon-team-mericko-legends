using FluentAssertions;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Mutations;
using Xunit;

namespace PensionCalculationEngine.Tests.Mutations;

public class AddPolicyMutationTests
{
    private readonly AddPolicyMutation _mutation = new();

    [Fact]
    public void Execute_WithValidData_AddsPolicy()
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
            new List<Policy>()
        );
        var situation = new Situation(dossier);
        var mutationData = new CalculationMutation(
            "b4444444-4444-4444-4444-444444444444",
            "add_policy",
            "DOSSIER",
            new DateOnly(2020, 1, 1),
            new Dictionary<string, object>
            {
                ["scheme_id"] = "SCHEME-A",
                ["employment_start_date"] = "2000-01-01",
                ["salary"] = 50000,
                ["part_time_factor"] = 1.0
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = _mutation.Execute(situation, mutationData);

        // Assert
        result.UpdatedSituation.Dossier!.Policies.Should().HaveCount(1);
        result.UpdatedSituation.Dossier.Policies[0].PolicyId.Should().Be("d2222222-2222-2222-2222-222222222222-1");
        result.UpdatedSituation.Dossier.Policies[0].SchemeId.Should().Be("SCHEME-A");
        result.UpdatedSituation.Dossier.Policies[0].Salary.Should().Be(50000);
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Execute_WithoutDossier_ReturnsCriticalError()
    {
        // Arrange
        var situation = new Situation(null);
        var mutationData = new CalculationMutation(
            "b4444444-4444-4444-4444-444444444444",
            "add_policy",
            "DOSSIER",
            new DateOnly(2020, 1, 1),
            new Dictionary<string, object>
            {
                ["scheme_id"] = "SCHEME-A",
                ["employment_start_date"] = "2000-01-01",
                ["salary"] = 50000,
                ["part_time_factor"] = 1.0
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = _mutation.Execute(situation, mutationData);

        // Assert
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Level.Should().Be("CRITICAL");
        result.Messages[0].Code.Should().Be("DOSSIER_NOT_FOUND");
    }

    [Fact]
    public void Execute_WithMultiplePolicies_GeneratesSequentialPolicyIds()
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
            "b5555555-5555-5555-5555-555555555555",
            "add_policy",
            "DOSSIER",
            new DateOnly(2020, 1, 1),
            new Dictionary<string, object>
            {
                ["scheme_id"] = "SCHEME-B",
                ["employment_start_date"] = "2010-01-01",
                ["salary"] = 60000,
                ["part_time_factor"] = 0.8
            },
            "d2222222-2222-2222-2222-222222222222"
        );

        // Act
        var result = _mutation.Execute(situation, mutationData);

        // Assert
        result.UpdatedSituation.Dossier!.Policies.Should().HaveCount(2);
        result.UpdatedSituation.Dossier.Policies[1].PolicyId.Should().Be("d2222222-2222-2222-2222-222222222222-2");
    }
}
