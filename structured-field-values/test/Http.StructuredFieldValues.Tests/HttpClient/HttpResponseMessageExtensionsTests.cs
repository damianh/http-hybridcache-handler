using DamianH.Http.StructuredFieldValues.Mapping;
using Shouldly;

namespace DamianH.Http.StructuredFieldValues.HttpClient;

public class HttpResponseMessageExtensionsTests
{
    private static readonly StructuredFieldMapper<PriorityHeader> PriorityMapper =
        StructuredFieldMapper<PriorityHeader>.Dictionary(b => b
            .Member("u", x => x.Urgency)
            .Member("i", x => x.Incremental));

    private static readonly StructuredFieldMapper<AcceptClientHintHeaderValue> AcceptChMapper =
        StructuredFieldMapper<AcceptClientHintHeaderValue>.List(b => b
            .TokenElements(x => x.Hints));

    [Fact]
    public void TryGetHeader_WithValidDictionaryHeader_Success()
    {
        var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("Priority", "u=3, i");

        var result = response.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeTrue();
        priority.ShouldNotBeNull();
        priority.Urgency.ShouldBe(3);
        priority.Incremental.ShouldBe(true);
    }

    [Fact]
    public void TryGetHeader_WithMissingHeader_ReturnsFalse()
    {
        var response = new HttpResponseMessage();

        var result = response.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeFalse();
        priority.ShouldBeNull();
    }

    [Fact]
    public void TryGetHeader_WithInvalidHeader_ReturnsFalse()
    {
        var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("Priority", "<<<invalid>>>");

        var result = response.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeFalse();
        priority.ShouldBeNull();
    }

    [Fact]
    public void TryGetHeader_WithValidListHeader_Success()
    {
        var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("Accept-CH", "Sec-CH-UA, Sec-CH-UA-Platform, Sec-CH-UA-Mobile");

        var result = response.TryGetHeader("Accept-CH", AcceptChMapper, out var header);

        result.ShouldBeTrue();
        header.ShouldNotBeNull();
        header.Hints.Count.ShouldBe(3);
        header.Contains("Sec-CH-UA").ShouldBeTrue();
    }

    [Fact]
    public void TryGetHeader_WithPartialDictionaryHeader_Success()
    {
        var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("Priority", "u=5");

        var result = response.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeTrue();
        priority.ShouldNotBeNull();
        priority.Urgency.ShouldBe(5);
        priority.Incremental.ShouldBeNull();
    }

    [Fact]
    public void TryGetHeader_RoundTrip_Success()
    {
        var response = new HttpResponseMessage();
        var original = new PriorityHeader { Urgency = 3, Incremental = true };
        response.Headers.TryAddWithoutValidation("Priority", PriorityMapper.Serialize(original));

        var result = response.TryGetHeader("Priority", PriorityMapper, out var parsed);

        result.ShouldBeTrue();
        parsed.ShouldNotBeNull();
        PriorityMapper.Serialize(parsed).ShouldBe(PriorityMapper.Serialize(original));
    }
}
