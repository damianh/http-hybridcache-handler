// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace DamianH.Http.StructuredFieldValues.RfcCompliance;

/// <summary>
/// Helper class for loading RFC test cases from JSON files.
/// </summary>
public static class RfcTestLoader
{
    /// <summary>
    /// Loads test cases from a JSON file in the RfcTests directory.
    /// </summary>
    /// <param name="fileName">The name of the JSON file (e.g., "item.json")</param>
    /// <returns>Array of test cases.</returns>
    public static RfcTestCase[] LoadTests(string fileName)
    {
        var filePath = Path.Combine("RfcTests", fileName);
        var json = File.ReadAllText(filePath);
        var testCases = JsonSerializer.Deserialize<RfcTestCase[]>(json);
        return testCases!;
    }

    /// <summary>
    /// Converts test cases to xUnit theory data.
    /// </summary>
    public static IEnumerable<object[]> ToTheoryData(RfcTestCase[] tests)
        => tests.Select(t => new object[] { t });
}
