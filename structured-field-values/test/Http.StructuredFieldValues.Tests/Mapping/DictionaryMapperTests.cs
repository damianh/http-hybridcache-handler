// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.Mapping;

public class DictionaryMapperTests
{
    private static readonly StructuredFieldMapper<PriorityHeader> PriorityMapper =
        StructuredFieldMapper<PriorityHeader>.Dictionary(b => b
            .Member("u", x => x.Urgency)
            .Member("i", x => x.Incremental));

    private static readonly StructuredFieldMapper<CacheControlHeader> CacheControlMapper =
        StructuredFieldMapper<CacheControlHeader>.Dictionary(b => b
            .Member("max-age", x => x.MaxAge)
            .Member("s-maxage", x => x.SMaxAge)
            .Member("no-cache", x => x.NoCache)
            .Member("no-store", x => x.NoStore)
            .Member("must-revalidate", x => x.MustRevalidate)
            .Member("private", x => x.Private)
            .Member("public", x => x.Public));

    [Fact]
    public void Parse_ValidPriorityHeader_ReturnsExpectedValues()
    {
        var priority = PriorityMapper.Parse("u=3, i");

        priority.Urgency.ShouldBe(3);
        priority.Incremental.ShouldBe(true);
    }

    [Fact]
    public void Parse_UrgencyOnly_ReturnsExpectedValues()
    {
        var priority = PriorityMapper.Parse("u=5");

        priority.Urgency.ShouldBe(5);
        priority.Incremental.ShouldBeNull();
    }

    [Fact]
    public void Parse_IncrementalOnly_BooleanShorthand_ReturnsExpectedValues()
    {
        var priority = PriorityMapper.Parse("i");

        priority.Urgency.ShouldBeNull();
        priority.Incremental.ShouldBe(true);
    }

    [Fact]
    public void Parse_EmptyDictionary_ReturnsNullValues()
    {
        var priority = PriorityMapper.Parse("");

        priority.Urgency.ShouldBeNull();
        priority.Incremental.ShouldBeNull();
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        var result = PriorityMapper.TryParse("u=3, i", out var priority);

        result.ShouldBeTrue();
        priority.ShouldNotBeNull();
        priority.Urgency.ShouldBe(3);
        priority.Incremental.ShouldBe(true);
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        var result = PriorityMapper.TryParse(null, out var priority);

        result.ShouldBeFalse();
        priority.ShouldBeNull();
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        var result = PriorityMapper.TryParse("<<<invalid>>>", out var priority);

        result.ShouldBeFalse();
        priority.ShouldBeNull();
    }

    [Fact]
    public void Serialize_AllValues_ReturnsExpectedString()
    {
        var priority = new PriorityHeader { Urgency = 3, Incremental = true };

        PriorityMapper.Serialize(priority).ShouldBe("u=3, i");
    }

    [Fact]
    public void Serialize_UrgencyOnly_ReturnsExpectedString()
    {
        var priority = new PriorityHeader { Urgency = 5, Incremental = null };

        PriorityMapper.Serialize(priority).ShouldBe("u=5");
    }

    [Fact]
    public void Serialize_NullValues_ReturnsEmptyString()
    {
        var priority = new PriorityHeader { Urgency = null, Incremental = null };

        PriorityMapper.Serialize(priority).ShouldBe("");
    }

    [Fact]
    public void Serialize_IncrementalOnly_BooleanShorthand()
    {
        var priority = new PriorityHeader { Incremental = true };

        PriorityMapper.Serialize(priority).ShouldBe("i");
    }

    [Fact]
    public void Roundtrip_ParseAndSerialize_ReturnsEquivalentValue()
    {
        var original = "u=3, i";
        var parsed = PriorityMapper.Parse(original);

        PriorityMapper.Serialize(parsed).ShouldBe(original);
    }

    [Fact]
    public void Parse_CacheControl_MaxAgeAndNoStore()
    {
        var cc = CacheControlMapper.Parse("max-age=3600, no-store");

        cc.MaxAge.ShouldBe(3600);
        cc.NoStore.ShouldBe(true);
        cc.NoCache.ShouldBeNull();
    }

    [Fact]
    public void Serialize_CacheControl_MultipleDirectives()
    {
        var cc = new CacheControlHeader { MaxAge = 3600, Public = true, MustRevalidate = true };

        var serialized = CacheControlMapper.Serialize(cc);

        serialized.ShouldContain("max-age=3600");
        serialized.ShouldContain("must-revalidate");
        serialized.ShouldContain("public");
    }

    [Fact]
    public void Roundtrip_CacheControl()
    {
        var original = "max-age=3600, no-cache, must-revalidate";
        var parsed = CacheControlMapper.Parse(original);

        parsed.MaxAge.ShouldBe(3600);
        parsed.NoCache.ShouldBe(true);
        parsed.MustRevalidate.ShouldBe(true);

        var serialized = CacheControlMapper.Serialize(parsed);
        serialized.ShouldBe(original);
    }

    [Fact]
    public void Parse_IntegerOverflowForInt32_ThrowsParseException()
    {
        // RFC 8941 allows integers up to 999_999_999_999_999 which overflows int.MaxValue (2_147_483_647)
        var mapper = StructuredFieldMapper<PriorityHeader>.Dictionary(b => b
            .Member("u", x => x.Urgency)
            .Member("i", x => x.Incremental));

        var ex = Should.Throw<StructuredFieldParseException>(() => mapper.Parse("u=999999999999"));
        ex.Message.ShouldContain("overflows Int32");
    }
}
