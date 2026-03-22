// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.RfcCompliance;

/// <summary>
/// RFC 8941 Examples compliance tests.
/// Tests the examples provided in the RFC specification.
/// </summary>
public class RfcExamplesTests
{
    private static readonly RfcTestCase[] Tests = RfcTestLoader.LoadTests("examples.json");

    public static IEnumerable<object[]> GetExampleTests() => RfcTestLoader.ToTheoryData(Tests);

    [Theory]
    [MemberData(nameof(GetExampleTests))]
    public void Examples_ShouldMatchRfcBehavior(RfcTestCase test)
    {
        var input = string.Join(", ", test.Raw);

        if (test.MustFail)
        {
            // Test must fail
            Should.Throw<StructuredFieldParseException>(() =>
            {
                ParseByHeaderType(input, test.HeaderType);
            });
        }
        else if (test.CanFail)
        {
            // Test may fail - implementation dependent
            try
            {
                ParseByHeaderType(input, test.HeaderType);
            }
            catch (StructuredFieldParseException)
            {
                // Acceptable for can_fail tests
            }
        }
        else
        {
            // Test must succeed
            var result = ParseByHeaderType(input, test.HeaderType);
            result.ShouldNotBeNull();

            // If canonical form is specified, test serialization
            if (test.Canonical != null && test.Canonical.Length > 0)
            {
                var serialized = SerializeByHeaderType(result, test.HeaderType);
                var expected = test.Canonical[0];
                serialized.ShouldBe(expected, $"Canonical form mismatch for test '{test.Name}'");
            }
        }
    }

    private object ParseByHeaderType(string input, string? headerType) =>
        headerType switch
        {
            "item" => StructuredFieldParser.ParseItem(input),
            "list" => StructuredFieldParser.ParseList(input),
            "dictionary" => StructuredFieldParser.ParseDictionary(input),
            _ => throw new InvalidOperationException($"Unknown header type: {headerType}")
        };

    private string SerializeByHeaderType(object value, string? headerType) =>
        headerType switch
        {
            "item" when value is StructuredFieldItem item => StructuredFieldSerializer.SerializeItem(item),
            "list" when value is StructuredFieldList list => StructuredFieldSerializer.SerializeList(list),
            "dictionary" when value is StructuredFieldDictionary dictionary => StructuredFieldSerializer.SerializeDictionary(dictionary),
            _ => throw new InvalidOperationException($"Unknown header type or value type mismatch: {headerType}")
        };
}
