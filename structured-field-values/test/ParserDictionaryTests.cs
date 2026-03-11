// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class ParserDictionaryTests
{
    [Fact]
    public void ParseDictionary_Empty_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("");

        // Assert
        dict.Count.ShouldBe(0);
    }

    [Fact]
    public void ParseDictionary_SingleMember_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("a=1");

        // Assert
        dict.Count.ShouldBe(1);
        dict.ContainsKey("a").ShouldBeTrue();
        dict["a"].Item.ShouldBeOfType<IntegerItem>();
        ((IntegerItem)dict["a"].Item).LongValue.ShouldBe(1);
    }

    [Fact]
    public void ParseDictionary_MultipleMembers_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("a=1, b=2, c=3");

        // Assert
        dict.Count.ShouldBe(3);
        ((IntegerItem)dict["a"].Item).LongValue.ShouldBe(1);
        ((IntegerItem)dict["b"].Item).LongValue.ShouldBe(2);
        ((IntegerItem)dict["c"].Item).LongValue.ShouldBe(3);
    }

    [Fact]
    public void ParseDictionary_MixedTypes_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("a=42, b=\"hello\", c=foo");

        // Assert
        dict.Count.ShouldBe(3);
        dict["a"].Item.ShouldBeOfType<IntegerItem>();
        dict["b"].Item.ShouldBeOfType<StringItem>();
        dict["c"].Item.ShouldBeOfType<Http.StructuredFieldValues.TokenItem>();
    }

    [Fact]
    public void ParseDictionary_BooleanTrueNoValue_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("foo");

        // Assert
        dict.Count.ShouldBe(1);
        dict["foo"].Item.ShouldBeOfType<BooleanItem>();
        ((BooleanItem)dict["foo"].Item).BooleanValue.ShouldBeTrue();
    }

    [Fact]
    public void ParseDictionary_BooleanWithParameters_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("foo;bar=baz");

        // Assert
        dict.Count.ShouldBe(1);
        dict["foo"].Item.ShouldBeOfType<BooleanItem>();
        dict["foo"].Item.Parameters.ContainsKey("bar").ShouldBeTrue();
    }

    [Fact]
    public void ParseDictionary_InnerList_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("a=(1 2 3)");

        // Assert
        dict.Count.ShouldBe(1);
        dict["a"].IsInnerList.ShouldBeTrue();
        dict["a"].InnerList.Count.ShouldBe(3);
    }

    [Fact]
    public void ParseDictionary_RealWorldCacheControl_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("max-age=3600, private");

        // Assert
        dict.Count.ShouldBe(2);
        dict["max-age"].Item.ShouldBeOfType<IntegerItem>();
        ((IntegerItem)dict["max-age"].Item).LongValue.ShouldBe(3600);
        
        dict["private"].Item.ShouldBeOfType<BooleanItem>();
        ((BooleanItem)dict["private"].Item).BooleanValue.ShouldBeTrue();
    }

    [Fact]
    public void ParseDictionary_WithWhitespace_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("  a=1  ,  b=2  ");

        // Assert
        dict.Count.ShouldBe(2);
        ((IntegerItem)dict["a"].Item).LongValue.ShouldBe(1);
        ((IntegerItem)dict["b"].Item).LongValue.ShouldBe(2);
    }

    [Fact]
    public void ParseDictionary_TrailingComma_ThrowsException() =>
        // Arrange & Act & Assert
        Should.Throw<StructuredFieldParseException>(() => 
            StructuredFieldParser.ParseDictionary("a=1,"));

    [Fact]
    public void ParseDictionary_InvalidKey_ThrowsException() =>
        // Arrange & Act & Assert
        Should.Throw<StructuredFieldParseException>(() => 
            StructuredFieldParser.ParseDictionary("123=value"));
}
