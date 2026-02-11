namespace PensionCalculationEngine.Api.Domain;

public sealed record Person(
    string PersonId,
    string Role,
    string Name,
    DateOnly BirthDate
);

public sealed record Policy(
    string PolicyId,
    string SchemeId,
    DateOnly EmploymentStartDate,
    decimal Salary,
    decimal PartTimeFactor,
    decimal? AttainablePension = null,
    List<Projection>? Projections = null
);

public sealed record Projection(
    DateOnly Date,
    decimal ProjectedPension
);

public sealed record Dossier(
    string DossierId,
    string Status,
    DateOnly? RetirementDate,
    List<Person> Persons,
    List<Policy> Policies
);

public sealed record Situation(
    Dossier? Dossier
);
