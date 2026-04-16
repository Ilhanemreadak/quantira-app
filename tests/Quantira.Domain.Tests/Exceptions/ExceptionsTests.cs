using Quantira.Domain.Exceptions;

namespace Quantira.Domain.Tests.Exceptions;

public sealed class ExceptionsTests
{
    [Fact]
    public void DomainException_WithMessage_ShouldSetMessage()
    {
        // Arrange

        // Act
        var exception = new DomainException("Rule violated.");

        // Assert
        Assert.Equal("Rule violated.", exception.Message);
    }

    [Fact]
    public void DomainException_WithInnerException_ShouldSetMessageAndInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner");

        // Act
        var exception = new DomainException("Rule violated.", innerException);

        // Assert
        Assert.Equal("Rule violated.", exception.Message);
        Assert.Equal(innerException, exception.InnerException);
    }

    [Fact]
    public void NotFoundException_ShouldSetPropertiesAndFormattedMessage()
    {
        // Arrange
        const string entityName = "Portfolio";
        var entityId = Guid.NewGuid();

        // Act
        var exception = new NotFoundException(entityName, entityId);

        // Assert
        Assert.Equal(entityName, exception.EntityName);
        Assert.Equal(entityId, exception.EntityId);
        Assert.Equal($"Portfolio with id '{entityId}' was not found.", exception.Message);
    }
}