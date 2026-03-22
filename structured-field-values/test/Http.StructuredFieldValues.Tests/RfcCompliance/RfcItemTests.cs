// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.RfcCompliance;

/// <summary>
/// RFC 8941 Item compliance tests using official test suite.
/// </summary>
public class RfcItemTests
{
    private static readonly RfcTestCase[] Tests = RfcTestLoader.LoadTests("item.json");

    public static IEnumerable<object[]> GetItemTests() => RfcTestLoader.ToTheoryData(Tests);

    [Theory]
    [MemberData(nameof(GetItemTests))]
    public void Item_ShouldMatchRfcBehavior(RfcTestCase test)
    {
        var input = string.Join(", ", test.Raw);

        if (test.MustFail)
        {
            // Test must fail - expect exception
            Should.Throw<StructuredFieldParseException>(() =>
                StructuredFieldParser.ParseItem(input));
        }
        else if (test.CanFail)
        {
            // Test may fail - implementation dependent
            // We'll try to parse but won't assert on failure
            try
            {
                var item = StructuredFieldParser.ParseItem(input);
                // If it succeeds, we can optionally validate against expected
            }
            catch (StructuredFieldParseException)
            {
                // Acceptable for can_fail tests
            }
        }
        else
        {
            // Test must succeed
            var item = StructuredFieldParser.ParseItem(input);
            item.ShouldNotBeNull();

            // If canonical form is specified, test serialization
            if (test.Canonical != null && test.Canonical.Length > 0)
            {
                var serialized = StructuredFieldSerializer.SerializeItem(item);
                var expected = test.Canonical[0];
                serialized.ShouldBe(expected, $"Canonical form mismatch for test '{test.Name}'");
            }
        }
    }
}
