using System.Text.Json;
using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;

namespace PensionCalculationEngine.Api.Mutations;

public sealed class CreateDossierMutation : IMutation
{
    public string MutationName => "create_dossier";

    public MutationResult Execute(Situation situation, CalculationMutation mutation)
    {
        var messages = new List<CalculationMessage>();
        
        // Validation: Dossier already exists
        if (situation.Dossier is not null)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "DOSSIER_ALREADY_EXISTS", "A dossier already exists in the situation"));
            return new MutationResult(situation, messages);
        }

        var props = mutation.MutationProperties;
        
        // Extract properties
        var dossierId = GetString(props, "dossier_id");
        var personId = GetString(props, "person_id");
        var name = GetString(props, "name");
        var birthDate = GetDate(props, "birth_date");

        // Validation: Empty name
        if (string.IsNullOrWhiteSpace(name))
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "INVALID_NAME", "Name is empty or blank"));
            return new MutationResult(situation, messages);
        }

        // Validation: Invalid birth_date
        if (birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "INVALID_BIRTH_DATE", "Birth date is in the future"));
            return new MutationResult(situation, messages);
        }

        // Create dossier
        var person = new Person(personId, "PARTICIPANT", name, birthDate);
        var dossier = new Dossier(
            dossierId,
            DossierStatus.Active,
            null,
            [person],
            []
        );

        var updatedSituation = new Situation(dossier);
        return new MutationResult(updatedSituation, messages);
    }

    private static string GetString(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.GetString() ?? string.Empty;
            }
            return value?.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static DateOnly GetDate(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return DateOnly.Parse(jsonElement.GetString()!);
            }
            if (value is string str)
            {
                return DateOnly.Parse(str);
            }
            if (value is DateOnly date)
            {
                return date;
            }
        }
        return DateOnly.MinValue;
    }
}
