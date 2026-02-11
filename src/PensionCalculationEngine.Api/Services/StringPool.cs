using System.Runtime.CompilerServices;
using PensionCalculationEngine.Api.Mutations;

namespace PensionCalculationEngine.Api.Services;

/// <summary>
/// String pooling for commonly used strings to reduce allocations
/// </summary>
public static class StringPool
{
    // Calculation outcomes
    public const string Success = "SUCCESS";
    public const string Failure = "FAILURE";
    
    // Dossier statuses  
    public const string Active = "ACTIVE";
    public const string Retired = "RETIRED";
    
    // Person roles
    public const string Participant = "PARTICIPANT";
    
    // Message levels
    public const string Critical = "CRITICAL";
    public const string Warning = "WARNING";
    
    // Mutation types
    public const string DossierCreation = "DOSSIER_CREATION";
    public const string Dossier = "DOSSIER";
}
