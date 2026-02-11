using System.Buffers;
using System.Runtime.CompilerServices;

namespace PensionCalculationEngine.Api.Services;

/// <summary>
/// High-performance pooling and caching for calculation operations.
/// Reduces allocations by reusing buffers and caching common values.
/// </summary>
public static class CalculationCache
{
    // Shared array pool for temporary integer arrays
    private static readonly ArrayPool<int> IntArrayPool = ArrayPool<int>.Shared;
    
    // Common date value to avoid repeated allocation
    private static readonly DateOnly MinDate = DateOnly.MinValue;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int[] RentIntArray(int minimumLength)
    {
        return IntArrayPool.Rent(minimumLength);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReturnIntArray(int[] array)
    {
        IntArrayPool.Return(array, clearArray: false); // Don't clear for performance
    }
    
    // Cache for common calculations (years of service uses same formula)
    private const decimal DaysPerYear = 365.25m;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal CalculateYearsOfService(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            return 0m;

        var days = endDate.DayNumber - startDate.DayNumber;
        return days / DaysPerYear;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateAge(DateOnly birthDate, DateOnly referenceDate)
    {
        var age = referenceDate.Year - birthDate.Year;
        if (referenceDate < birthDate.AddYears(age))
            age--;
        return age;
    }
}
