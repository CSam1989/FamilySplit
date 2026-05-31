using FamilySplit.Application.Exceptions;
using FluentAssertions;

namespace FamilySplit.UnitTests.Exceptions;

public class ForbiddenExceptionTests
{
    [Fact]
    public void Ctor_Default_SetsDefaultMessage()
    {
        var ex = new ForbiddenException();
        ex.Message.Should().Be("You do not have permission to perform this action.");
    }

    [Fact]
    public void Ctor_CustomMessage_SetsMessage()
    {
        var ex = new ForbiddenException("custom");
        ex.Message.Should().Be("custom");
    }
}
