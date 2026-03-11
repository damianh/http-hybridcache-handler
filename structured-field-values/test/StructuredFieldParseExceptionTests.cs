// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class StructuredFieldParseExceptionTests
{
    [Fact]
    public void Constructor_WithMessageAndPosition_Success()
    {
        var message = "Test error";
        var position = 42;
        var exception = new StructuredFieldParseException(message, position);
        
        exception.Message.ShouldBe($"{message} at position {position}");
        exception.Position.ShouldBe(position);
    }

    [Fact]
    public void IsFormatException_Success()
    {
        var exception = new StructuredFieldParseException();
        
        exception.ShouldBeAssignableTo<FormatException>();
    }
}
