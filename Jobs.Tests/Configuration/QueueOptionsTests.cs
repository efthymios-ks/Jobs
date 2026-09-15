namespace Jobs.Tests.Configuration;

public sealed class QueueOptionsTests
{
    [Fact]
    public void Validate_WhenQueueNameIsEmpty_ShouldThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = string.Empty };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_WhenQueueNameIsWhiteSpace_ShouldThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "   " };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_WhenMaxConcurrencyIsZero_ShouldThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "q", MaxConcurrency = 0 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_WhenMaxConcurrencyIsNegative_ShouldThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "q", MaxConcurrency = -1 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_WhenMaxCapacityIsZero_ShouldThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "q", MaxCapacity = 0 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_WhenMaxCapacityIsNegative_ShouldThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "q", MaxCapacity = -1 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_WhenMaxCapacityIsNull_ShouldNotThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "q", MaxCapacity = null };

        // Act & Assert (no throw)
        options.Validate();
    }

    [Fact]
    public void Validate_WhenAllValuesAreValid_ShouldNotThrow()
    {
        // Arrange
        var options = new QueueOptions { QueueName = "q", MaxConcurrency = 4, MaxCapacity = 100 };

        // Act & Assert (no throw)
        options.Validate();
    }
}
