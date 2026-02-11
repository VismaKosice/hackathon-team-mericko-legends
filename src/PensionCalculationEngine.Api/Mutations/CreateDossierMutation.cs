using PensionCalculationEngine.Api.Domain;
using PensionCalculationEngine.Api.Models;
using PensionCalculationEngine.Api.Services;

namespace PensionCalculationEngine.Api.Mutations;

public sealed class CreateDossierMutation : IMutation
{
    public string MutationName => "create_dossier";

    public Task<MutationResult> ExecuteAsync(Situation situation, CalculationMutation mutation, CancellationToken cancellationToken = default)
    {
        var messages = new List<CalculationMessage>();
        
        // Validation: Dossier already exists
        if (situation.Dossier is not null)
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "DOSSIER_ALREADY_EXISTS", "A dossier already exists in the situation"));
            return Task.FromResult(new MutationResult(situation, messages));
        }

        var props = mutation.MutationProperties;
        
        // Extract properties using optimized extractor
        var dossierId = PropertyExtractor.GetString(props, "dossier_id");
        var personId = PropertyExtractor.GetString(props, "person_id");
        var name = PropertyExtractor.GetString(props, "name");
        var birthDate = PropertyExtractor.GetDate(props, "birth_date");

        // Validation: Empty name
        if (string.IsNullOrWhiteSpace(name))
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "INVALID_NAME", "Name is empty or blank"));
            return Task.FromResult(new MutationResult(situation, messages));
        }

        // Validation: Invalid birth_date
        if (birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            messages.Add(new CalculationMessage(0, MessageLevel.Critical, "INVALID_BIRTH_DATE", "Birth date is in the future"));
            return Task.FromResult(new MutationResult(situation, messages));
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
        return Task.FromResult(new MutationResult(updatedSituation, messages));
    }
}
