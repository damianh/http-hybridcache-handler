// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class ListMemberTests
{
    [Fact]
    public void FromItem_ValidItem_Success()
    {
        var item = new IntegerItem(42);
        var member = ListMember.FromItem(item);
        
        member.IsItem.ShouldBeTrue();
        member.IsInnerList.ShouldBeFalse();
        member.Item.ShouldBe(item);
    }

    [Fact]
    public void FromItem_NullItem_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => ListMember.FromItem(null!));

    [Fact]
    public void FromInnerList_ValidInnerList_Success()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        
        var member = ListMember.FromInnerList(innerList);
        
        member.IsInnerList.ShouldBeTrue();
        member.IsItem.ShouldBeFalse();
        member.InnerList.ShouldBe(innerList);
    }

    [Fact]
    public void FromInnerList_NullInnerList_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => ListMember.FromInnerList(null!));

    [Fact]
    public void Item_WhenIsItem_ReturnsItem()
    {
        var item = new IntegerItem(42);
        var member = ListMember.FromItem(item);
        
        member.Item.ShouldBe(item);
    }

    [Fact]
    public void Item_WhenIsInnerList_ThrowsInvalidOperationException()
    {
        var innerList = new InnerList();
        var member = ListMember.FromInnerList(innerList);
        
        Should.Throw<InvalidOperationException>(() => { _ = member.Item; });
    }

    [Fact]
    public void InnerList_WhenIsInnerList_ReturnsInnerList()
    {
        var innerList = new InnerList();
        var member = ListMember.FromInnerList(innerList);
        
        member.InnerList.ShouldBe(innerList);
    }

    [Fact]
    public void InnerList_WhenIsItem_ThrowsInvalidOperationException()
    {
        var item = new IntegerItem(42);
        var member = ListMember.FromItem(item);
        
        Should.Throw<InvalidOperationException>(() => { _ = member.InnerList; });
    }

    [Fact]
    public void TryGetItem_WhenIsItem_ReturnsTrue()
    {
        var item = new IntegerItem(42);
        var member = ListMember.FromItem(item);
        
        var result = member.TryGetItem(out var outItem);
        
        result.ShouldBeTrue();
        outItem.ShouldBe(item);
    }

    [Fact]
    public void TryGetItem_WhenIsInnerList_ReturnsFalse()
    {
        var innerList = new InnerList();
        var member = ListMember.FromInnerList(innerList);
        
        var result = member.TryGetItem(out var outItem);
        
        result.ShouldBeFalse();
        outItem.ShouldBeNull();
    }

    [Fact]
    public void TryGetInnerList_WhenIsInnerList_ReturnsTrue()
    {
        var innerList = new InnerList();
        var member = ListMember.FromInnerList(innerList);
        
        var result = member.TryGetInnerList(out var outInnerList);
        
        result.ShouldBeTrue();
        outInnerList.ShouldBe(innerList);
    }

    [Fact]
    public void TryGetInnerList_WhenIsItem_ReturnsFalse()
    {
        var item = new IntegerItem(42);
        var member = ListMember.FromItem(item);
        
        var result = member.TryGetInnerList(out var outInnerList);
        
        result.ShouldBeFalse();
        outInnerList.ShouldBeNull();
    }

    [Fact]
    public void ToString_Item_ReturnsItemString()
    {
        var item = new IntegerItem(42);
        var member = ListMember.FromItem(item);
        
        member.ToString().ShouldBe("42");
    }

    [Fact]
    public void ToString_InnerList_ReturnsInnerListString()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        innerList.Add(new IntegerItem(2));
        
        var member = ListMember.FromInnerList(innerList);
        
        member.ToString().ShouldBe("(1 2)");
    }
}
