using FluentAssertions;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;
using Xunit;

namespace PensionCalculationEngine.Tests.Integration;

public class CalculationEngineIntegrationTests
{
    private readonly CalculationEngine _engine;

    public CalculationEngineIntegrationTests()
    {
        var schemeRegistry = new SchemeRegistryService(null);
        var registry = new MutationRegistry(schemeRegistry);
        var patchGenerator = new JsonPatchGenerator();
        _engine = new CalculationEngine(registry, patchGenerator);
    }

    [Fact]
    public async Task ProcessCalculationRequest_FullScenario_ReturnsCorrectResponse()
    {
        // Arrange
        var request = new CalculationRequest(
            "tenant-001",
            new CalculationInstructions(
                new List<CalculationMutation>
                {
                    new CalculationMutation(
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
                    ),
                    new CalculationMutation(
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
                    ),
                    new CalculationMutation(
                        "c5555555-5555-5555-5555-555555555555",
                        "apply_indexation",
                        "DOSSIER",
                        new DateOnly(2021, 1, 1),
                        new Dictionary<string, object>
                        {
                            ["percentage"] = 0.03
                        },
                        "d2222222-2222-2222-2222-222222222222"
                    )
                }
            )
        );

        // Act
        var response = await _engine.ProcessCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("SUCCESS");
        response.CalculationMetadata.TenantId.Should().Be("tenant-001");
        response.CalculationResult.Messages.Should().BeEmpty();
        response.CalculationResult.Mutations.Should().HaveCount(3);
        response.CalculationResult.EndSituation.Situation.Dossier.Should().NotBeNull();
        response.CalculationResult.EndSituation.Situation.Dossier!.Policies.Should().HaveCount(1);
        response.CalculationResult.EndSituation.Situation.Dossier.Policies[0].Salary.Should().Be(51500);
    }

    [Fact]
    public async Task ProcessCalculationRequest_WithCriticalError_HaltsProcessing()
    {
        // Arrange
        var request = new CalculationRequest(
            "tenant-001",
            new CalculationInstructions(
                new List<CalculationMutation>
                {
                    new CalculationMutation(
                        "a1111111-1111-1111-1111-111111111111",
                        "create_dossier",
                        "DOSSIER_CREATION",
                        new DateOnly(2020, 1, 1),
                        new Dictionary<string, object>
                        {
                            ["dossier_id"] = "d2222222-2222-2222-2222-222222222222",
                            ["person_id"] = "p3333333-3333-3333-3333-333333333333",
                            ["name"] = "", // Invalid - will cause CRITICAL error
                            ["birth_date"] = "1960-06-15"
                        }
                    ),
                    new CalculationMutation(
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
                    )
                }
            )
        );

        // Act
        var response = await _engine.ProcessCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        response.CalculationResult.Messages.Should().HaveCount(1);
        response.CalculationResult.Messages[0].Level.Should().Be("CRITICAL");
        response.CalculationResult.Mutations.Should().HaveCount(1); // Only first mutation
        response.CalculationResult.EndSituation.Situation.Dossier.Should().BeNull();
    }
}
