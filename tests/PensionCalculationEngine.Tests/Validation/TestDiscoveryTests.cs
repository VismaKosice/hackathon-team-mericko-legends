using Xunit;
using PensionCalculationEngine.Tests.Integration;

namespace PensionCalculationEngine.Tests.Validation;

/// <summary>
/// Test case discovery and validation tests.
/// These tests verify that the test suite is properly configured and all test cases are valid.
/// </summary>
public class TestDiscoveryTests
{
    [Fact]
    public void TestDataDirectory_ShouldExist()
    {
        // Arrange
        var testDataPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "TestData"
        );

        // Assert
        Assert.True(Directory.Exists(testDataPath),
            $"TestData directory not found at {testDataPath}");
    }

    [Fact]
    public void CoreTestCases_ShouldBeLoaded()
    {
        // Arrange
        var loader = new TestCaseLoader();

        // Act
        var coreTests = loader.LoadTestCases("C[0-1][0-9]-*.json").ToList();

        // Assert
        Assert.NotEmpty(coreTests);
        Assert.True(coreTests.Count >= 10, $"Expected at least 10 core tests, found {coreTests.Count}");
    }

    [Fact]
    public void AllTestCases_ShouldHaveValidStructure()
    {
        // Arrange
        var loader = new TestCaseLoader();
        var allTests = loader.LoadTestCases("*.json").ToList();

        // Assert
        Assert.NotEmpty(allTests);

        foreach (var test in allTests)
        {
            Assert.False(string.IsNullOrWhiteSpace(test.Id),
                $"Test case missing ID");
            Assert.False(string.IsNullOrWhiteSpace(test.Name),
                $"Test case {test.Id} missing Name");
            Assert.NotNull(test.Expected);
            Assert.True(test.Expected.HttpStatus > 0,
                $"Test case {test.Id} has invalid HTTP status");
            Assert.False(string.IsNullOrWhiteSpace(test.Expected?.CalculationOutcome),
                $"Test case {test.Id} missing calculation_outcome");
        }
    }

    [Fact]
    public void TestSuiteSummary_ShouldProvideAccurateCount()
    {
        // Arrange
        var loader = new TestCaseLoader();

        // Act
        var summary = loader.GetTestSuiteSummary();

        // Assert
        Assert.True(summary.TotalTests > 0, "No tests found in test suite");
        Assert.True(summary.CoreTests.Count > 0, "No core tests found");
        Assert.Equal(summary.TotalTests, 
            summary.CoreTests.Count + summary.WarningTests.Count + summary.BonusTests.Count);
    }

    [Fact]
    public void TestCaseIds_ShouldFollowNamingConvention()
    {
        // Arrange
        var loader = new TestCaseLoader();
        var allTests = loader.LoadTestCases("*.json").ToList();

        // Assert
        var validPatterns = new[] { "C", "B" };

        foreach (var test in allTests)
        {
            Assert.NotNull(test.Id);
            Assert.False(string.IsNullOrWhiteSpace(test.Id),
                $"Test case ID cannot be empty");
            var firstChar = test.Id[0];
            Assert.True(validPatterns.Contains(firstChar.ToString()),
                $"Test case ID '{test.Id}' does not match expected pattern (C## or B##)");
        }
    }

    [Theory]
    [InlineData("SUCCESS")]
    [InlineData("FAILURE")]
    public void TestCases_ShouldHaveValidOutcomes(string outcome)
    {
        // Arrange
        var loader = new TestCaseLoader();
        var allTests = loader.LoadTestCases("*.json").ToList();

        // Act
        var testsWithOutcome = allTests.Where(t => t.Expected.CalculationOutcome == outcome).ToList();

        // Assert
        Assert.NotEmpty(testsWithOutcome);
    }

    [Fact]
    public void PrintTestSuiteSummary()
    {
        // Arrange & Act
        var loader = new TestCaseLoader();
        var summary = loader.GetTestSuiteSummary();

        // Output for debugging
        Xunit.Abstractions.ITestOutputHelper? output = null;
        if (output != null)
        {
            output.WriteLine(summary.ToString());
        }

        // Assert
        Assert.NotNull(summary);
    }
}

