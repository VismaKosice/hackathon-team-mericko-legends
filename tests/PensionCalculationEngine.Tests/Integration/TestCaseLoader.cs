using System.Text.Json;
using System.Text.RegularExpressions;
using PensionCalculationEngine.Tests.TestData;

namespace PensionCalculationEngine.Tests.Integration;

/// <summary>
/// Test case loader utility that discovers and loads JSON test cases from the TestData directory.
/// Supports wildcard patterns and provides metadata about loaded tests.
/// </summary>
public class TestCaseLoader
{
    private readonly string _testDataPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public TestCaseLoader(string? testDataPath = null)
    {
        _testDataPath = testDataPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "TestData"
        );

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Loads all test cases matching the specified pattern.
    /// Supports both simple wildcards (*) and regex-like patterns with character classes [].
    /// </summary>
    public IEnumerable<TestCase> LoadTestCases(string searchPattern)
    {
        if (!Directory.Exists(_testDataPath))
        {
            yield break;
        }

        // Convert pattern to regex if it contains character classes
        var regex = ConvertPatternToRegex(searchPattern);
        
        // Get all JSON files and filter by pattern
        var files = Directory.GetFiles(_testDataPath, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => regex.IsMatch(Path.GetFileName(f)))
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files)
        {
            var testCase = LoadTestCaseFromFile(file);
            if (testCase != null)
            {
                yield return testCase;
            }
        }
    }

    /// <summary>
    /// Converts a search pattern (with wildcards and character classes) to a regex.
    /// </summary>
    private Regex ConvertPatternToRegex(string pattern)
    {
        // Escape special regex characters except *, ?, and []
        var regexPattern = Regex.Escape(pattern)
            .Replace(@"\*", ".*")           // * becomes .*
            .Replace(@"\?", ".")            // ? becomes .
            .Replace(@"\[", "[")            // Unescape [
            .Replace(@"\]", "]");           // Unescape ]

        return new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Loads a specific test case by file path.
    /// </summary>
    public TestCase? LoadTestCaseFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<TestCase>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load test case from {Path.GetFileName(filePath)}: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Gets summary information about all test cases in the TestData directory.
    /// </summary>
    public TestSuiteSummary GetTestSuiteSummary()
    {
        var summary = new TestSuiteSummary();

        if (!Directory.Exists(_testDataPath))
        {
            return summary;
        }

        var patterns = new[] { "C[0-1][0-9]-*.json", "C[1][1-4]-*.json", "B*.json" };

        foreach (var pattern in patterns)
        {
            var testCases = LoadTestCases(pattern).ToList();
            summary.AllTests.AddRange(testCases);

            if (pattern.StartsWith("C[0-1]"))
            {
                summary.CoreTests.AddRange(testCases);
            }
            else if (pattern.StartsWith("C[1]"))
            {
                summary.WarningTests.AddRange(testCases);
            }
            else
            {
                summary.BonusTests.AddRange(testCases);
            }
        }

        return summary;
    }
}

/// <summary>
/// Summary of test suite organization.
/// </summary>
public class TestSuiteSummary
{
    public List<TestCase> CoreTests { get; } = new();
    public List<TestCase> WarningTests { get; } = new();
    public List<TestCase> BonusTests { get; } = new();
    public List<TestCase> AllTests { get; } = new();

    public int TotalTests => AllTests.Count;
    public int TotalExpectedMessageCount => AllTests.Sum(t => t.Expected.MessageCount);
    public int SuccessTests => AllTests.Count(t => t.Expected.CalculationOutcome == "SUCCESS");
    public int ErrorTests => AllTests.Count(t => t.Expected.CalculationOutcome == "ERROR");
    public int WarningLevelTests => AllTests.Count(t => t.Expected.Messages.Any(m => m.Level == "WARNING"));

    public override string ToString()
    {
        return $@"Test Suite Summary:
  Total Tests: {TotalTests}
  Core Tests (C01-C10): {CoreTests.Count}
  Warning Tests (C11-C14): {WarningTests.Count}
  Bonus Tests (B*): {BonusTests.Count}
  
  Expected Outcomes:
    SUCCESS: {SuccessTests}
    ERROR: {ErrorTests}
    
  Message Validation:
    Total Expected Messages: {TotalExpectedMessageCount}
    Tests with Warnings: {WarningLevelTests}";
    }
}

