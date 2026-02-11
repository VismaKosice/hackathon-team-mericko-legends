using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;

namespace PensionCalculationEngine.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<CalculationBenchmarks>();
    }
}

[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class CalculationBenchmarks
{
    private CalculationEngine _engine = null!;
    private CalculationRequest _simpleRequest = null!;
    private CalculationRequest _complexRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        var schemeRegistry = new SchemeRegistryService(null);
        var registry = new MutationRegistry(schemeRegistry);
        var patchGenerator = new JsonPatchGenerator();
        _engine = new CalculationEngine(registry, patchGenerator);

        // Simple request: create dossier + add policy + indexation
        _simpleRequest = new CalculationRequest(
            "tenant-001",
            new CalculationInstructions(
                new List<CalculationMutation>
                {
                    new(
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
                    new(
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
                    new(
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

        // Complex request: multiple policies + indexations + retirement
        var complexMutations = new List<CalculationMutation>
        {
            new(
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
            )
        };

        // Add 10 policies
        for (int i = 0; i < 10; i++)
        {
            complexMutations.Add(new(
                Guid.NewGuid().ToString(),
                "add_policy",
                "DOSSIER",
                new DateOnly(2020, 1, 1 + i),
                new Dictionary<string, object>
                {
                    ["scheme_id"] = $"SCHEME-{i % 3}",
                    ["employment_start_date"] = new DateOnly(2000 + i, 1, 1).ToString("yyyy-MM-dd"),
                    ["salary"] = 50000 + (i * 5000),
                    ["part_time_factor"] = 0.8 + (i * 0.02)
                },
                "d2222222-2222-2222-2222-222222222222"
            ));
        }

        // Add 5 indexations
        for (int i = 0; i < 5; i++)
        {
            complexMutations.Add(new(
                Guid.NewGuid().ToString(),
                "apply_indexation",
                "DOSSIER",
                new DateOnly(2021 + i, 1, 1),
                new Dictionary<string, object>
                {
                    ["percentage"] = 0.03,
                    ["scheme_id"] = $"SCHEME-{i % 3}"
                },
                "d2222222-2222-2222-2222-222222222222"
            ));
        }

        // Add retirement calculation
        complexMutations.Add(new(
            Guid.NewGuid().ToString(),
            "calculate_retirement_benefit",
            "DOSSIER",
            new DateOnly(2025, 6, 15),
            new Dictionary<string, object>
            {
                ["retirement_date"] = "2025-06-15"
            },
            "d2222222-2222-2222-2222-222222222222"
        ));

        _complexRequest = new CalculationRequest(
            "tenant-001",
            new CalculationInstructions(complexMutations)
        );
    }

    [Benchmark]
    public CalculationResponse SimpleRequest()
    {
        return _engine.ProcessCalculationRequestAsync(_simpleRequest).GetAwaiter().GetResult();
    }

    [Benchmark]
    public CalculationResponse ComplexRequest()
    {
        return _engine.ProcessCalculationRequestAsync(_complexRequest).GetAwaiter().GetResult();
    }

    [Benchmark]
    public List<CalculationResponse> Throughput_10Requests()
    {
        var results = new List<CalculationResponse>(10);
        for (int i = 0; i < 10; i++)
        {
            results.Add(_engine.ProcessCalculationRequestAsync(_simpleRequest).GetAwaiter().GetResult());
        }
        return results;
    }
}
