// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class ItemTypesTests
{
    [Fact]
    public void BooleanItem_TrueValue_Success()
    {
        var item = new BooleanItem(true);
        
        item.BooleanValue.ShouldBeTrue();
        item.Value.ShouldBe(true);
        item.Type.ShouldBe(ItemType.Boolean);
        item.ToString().ShouldBe("?1");
    }

    [Fact]
    public void BooleanItem_FalseValue_Success()
    {
        var item = new BooleanItem(false);
        
        item.BooleanValue.ShouldBeFalse();
        item.Value.ShouldBe(false);
        item.Type.ShouldBe(ItemType.Boolean);
        item.ToString().ShouldBe("?0");
    }

    [Fact]
    public void BooleanItem_Equals_SameValue_ReturnsTrue()
    {
        var item1 = new BooleanItem(true);
        var item2 = new BooleanItem(true);
        
        item1.Equals(item2).ShouldBeTrue();
    }

    [Fact]
    public void BooleanItem_Equals_DifferentValue_ReturnsFalse()
    {
        var item1 = new BooleanItem(true);
        var item2 = new BooleanItem(false);
        
        item1.Equals(item2).ShouldBeFalse();
    }
}
