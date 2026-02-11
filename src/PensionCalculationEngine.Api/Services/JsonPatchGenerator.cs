using System.Text.Json;
using System.Text.Json.Serialization;
using PensionCalculationEngine.Api.Domain;

namespace PensionCalculationEngine.Api.Services;

public sealed class JsonPatchGenerator
{
    private readonly JsonSerializerOptions _options;

    public JsonPatchGenerator()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
    }

    public List<JsonPatchOperation> GeneratePatch(Situation previousSituation, Situation currentSituation)
    {
        var operations = new List<JsonPatchOperation>();

        // Compare dossiers
        if (previousSituation.Dossier == null && currentSituation.Dossier == null)
        {
            return operations; // No change
        }

        if (previousSituation.Dossier == null && currentSituation.Dossier != null)
        {
            // Add entire dossier
            operations.Add(new JsonPatchOperation(
                "add",
                "/situation/dossier",
                SerializeDossier(currentSituation.Dossier)
            ));
            return operations;
        }

        if (previousSituation.Dossier != null && currentSituation.Dossier == null)
        {
            // Remove dossier
            operations.Add(new JsonPatchOperation("remove", "/situation/dossier", null));
            return operations;
        }

        // Both dossiers exist - compare fields
        var prevDossier = previousSituation.Dossier!;
        var currDossier = currentSituation.Dossier!;

        // Compare dossier_id
        if (prevDossier.DossierId != currDossier.DossierId)
        {
            operations.Add(new JsonPatchOperation("replace", "/situation/dossier/dossier_id", currDossier.DossierId));
        }

        // Compare status
        if (prevDossier.Status != currDossier.Status)
        {
            operations.Add(new JsonPatchOperation("replace", "/situation/dossier/status", currDossier.Status));
        }

        // Compare retirement_date
        if (prevDossier.RetirementDate != currDossier.RetirementDate)
        {
            operations.Add(new JsonPatchOperation(
                "replace",
                "/situation/dossier/retirement_date",
                currDossier.RetirementDate?.ToString("yyyy-MM-dd")
            ));
        }

        // Compare persons
        CompareLists(operations, "/situation/dossier/persons", prevDossier.Persons, currDossier.Persons, SerializePerson);

        // Compare policies
        CompareLists(operations, "/situation/dossier/policies", prevDossier.Policies, currDossier.Policies, SerializePolicy);

        return operations;
    }

    private void CompareLists<T>(
        List<JsonPatchOperation> operations,
        string basePath,
        List<T> previousList,
        List<T> currentList,
        Func<T, object> serializer)
    {
        var prevCount = previousList.Count;
        var currCount = currentList.Count;

        // Handle added items
        if (currCount > prevCount)
        {
            for (int i = prevCount; i < currCount; i++)
            {
                operations.Add(new JsonPatchOperation(
                    "add",
                    $"{basePath}/{i}",
                    serializer(currentList[i])
                ));
            }
        }

        // Handle removed items (from end)
        if (prevCount > currCount)
        {
            for (int i = prevCount - 1; i >= currCount; i--)
            {
                operations.Add(new JsonPatchOperation("remove", $"{basePath}/{i}", null));
            }
        }

        // Handle modified items
        var minCount = Math.Min(prevCount, currCount);
        for (int i = 0; i < minCount; i++)
        {
            var prev = previousList[i];
            var curr = currentList[i];
            
            if (!AreEqual(prev, curr))
            {
                operations.Add(new JsonPatchOperation(
                    "replace",
                    $"{basePath}/{i}",
                    serializer(curr)
                ));
            }
        }
    }

    private bool AreEqual<T>(T a, T b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        
        // Use record equality for our domain objects
        return a.Equals(b);
    }

    private object SerializeDossier(Dossier dossier)
    {
        return new
        {
            dossier_id = dossier.DossierId,
            status = dossier.Status,
            retirement_date = dossier.RetirementDate?.ToString("yyyy-MM-dd"),
            persons = dossier.Persons.Select(SerializePerson).ToList(),
            policies = dossier.Policies.Select(SerializePolicy).ToList()
        };
    }

    private object SerializePerson(Person person)
    {
        return new
        {
            person_id = person.PersonId,
            role = person.Role,
            name = person.Name,
            birth_date = person.BirthDate.ToString("yyyy-MM-dd")
        };
    }

    private object SerializePolicy(Policy policy)
    {
        return new
        {
            policy_id = policy.PolicyId,
            scheme_id = policy.SchemeId,
            employment_start_date = policy.EmploymentStartDate.ToString("yyyy-MM-dd"),
            salary = policy.Salary,
            part_time_factor = policy.PartTimeFactor,
            attainable_pension = policy.AttainablePension,
            projections = policy.Projections?.Select(p => new
            {
                date = p.Date.ToString("yyyy-MM-dd"),
                projected_pension = p.ProjectedPension
            }).ToList()
        };
    }
}

public sealed record JsonPatchOperation(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("value")] object? Value
);
