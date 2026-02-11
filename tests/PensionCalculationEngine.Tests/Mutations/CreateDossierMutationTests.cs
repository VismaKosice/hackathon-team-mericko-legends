using FluentAssertions;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Mutations;
using Xunit;

namespace PensionCalculationEngine.Tests.Mutations;

public class CreateDossierMutationTests
{
    private readonly CreateDossierMutation _mutation = new();

    [Fact]
    public void Execute_WithValidData_CreatesDossier()
    {
        // Arrange
        var situation = new Situation(null);
        var mutationData = new DossierCreationMutation(
            "a1111111-1111-1111-1111-111111111111",
            "create_dossier",
            "DOSSIER_CREATION",
            new DateOnly(2020, 1, 1),
            new Dictionary<string, object>
            {
                ["dossier_id"] = "d2222222-2222-2222-2222-222222222222",
                ["person_id"] = "p3333333-3333-3333-3333-333333333333",
                ["name"] = "Jane Doe",
                ["birth_date"] = "1960-06-15"
            }
        );

        // Act
        var result = _mutation.Execute(situation, mutationData);

        // Assert
        result.UpdatedSituation.Dossier.Should().NotBeNull();
        result.UpdatedSituation.Dossier!.DossierId.Should().Be("d2222222-2222-2222-2222-222222222222");
        result.UpdatedSituation.Dossier.Status.Should().Be("ACTIVE");
        result.UpdatedSituation.Dossier.Persons.Should().HaveCount(1);
        result.UpdatedSituation.Dossier.Persons[0].Name.Should().Be("Jane Doe");
        result.UpdatedSituation.Dossier.Policies.Should().BeEmpty();
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Execute_WithExistingDossier_ReturnsCriticalError()
    {
        // Arrange
        var existingDossier = new Dossier(
            "existing-id",
            "ACTIVE",
            null,
            new List<Person>(),
            new List<Policy>()
        );
        var situation = new Situation(existingDossier);
        var mutationData = new DossierCreationMutation(
            "a1111111-1111-1111-1111-111111111111",
            "create_dossier",
            "DOSSIER_CREATION",
            new DateOnly(2020, 1, 1),
            new Dictionary<string, object>
            {
                ["dossier_id"] = "d2222222-2222-2222-2222-222222222222",
                ["person_id"] = "p3333333-3333-3333-3333-333333333333",
                ["name"] = "Jane Doe",
                ["birth_date"] = "1960-06-15"
            }
        );

        // Act
        var result = _mutation.Execute(situation, mutationData);

        // Assert
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Level.Should().Be("CRITICAL");
        result.Messages[0].Code.Should().Be("DOSSIER_ALREADY_EXISTS");
    }

    [Fact]
    public void Execute_WithEmptyName_ReturnsCriticalError()
    {
        // Arrange
        var situation = new Situation(null);
        var mutationData = new DossierCreationMutation(
            "a1111111-1111-1111-1111-111111111111",
            "create_dossier",
            "DOSSIER_CREATION",
            new DateOnly(2020, 1, 1),
            new Dictionary<string, object>
            {
                ["dossier_id"] = "d2222222-2222-2222-2222-222222222222",
                ["person_id"] = "p3333333-3333-3333-3333-333333333333",
                ["name"] = "",
                ["birth_date"] = "1960-06-15"
            }
        );

        // Act
        var result = _mutation.Execute(situation, mutationData);

        // Assert
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Level.Should().Be("CRITICAL");
        result.Messages[0].Code.Should().Be("INVALID_NAME");
    }
}
