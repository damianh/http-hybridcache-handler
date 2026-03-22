// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.RfcCompliance;

/// <summary>
/// RFC 8941 List compliance tests using official test suite.
/// </summary>
public class RfcListTests
{
    private static readonly RfcTestCase[] Tests = RfcTestLoader.LoadTests("list.json");

    public static IEnumerable<object[]> GetListTests() => RfcTestLoader.ToTheoryData(Tests);

    [Theory]
    [MemberData(nameof(GetListTests))]
    public void List_ShouldMatchRfcBehavior(RfcTestCase test)
    {
        var input = string.Join(", ", test.Raw);

        if (test.MustFail)
        {
            // Test must fail - expect exception
            Should.Throw<StructuredFieldParseException>(() =>
                StructuredFieldParser.ParseList(input));
        }
        else if (test.CanFail)
        {
            // Test may fail - implementation dependent
            try
            {
                var list = StructuredFieldParser.ParseList(input);
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
            var list = StructuredFieldParser.ParseList(input);
            list.ShouldNotBeNull();

            // If canonical form is specified, test serialization
            if (test.Canonical != null && test.Canonical.Length > 0)
            {
                var serialized = StructuredFieldSerializer.SerializeList(list);
                var expected = test.Canonical[0];
                serialized.ShouldBe(expected, $"Canonical form mismatch for test '{test.Name}'");
            }
        }
    }
}
