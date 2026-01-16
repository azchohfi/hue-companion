using FluentAssertions;
using HueWindows.Core.Models;
using Xunit;

namespace HueWindows.Tests.Models;

/// <summary>
/// Unit tests for Result model
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Success_WithValue_ShouldCreateSuccessResultWithValue()
    {
        // Act
        var result = Result<string>.Success("test-value");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test-value");
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        // Act
        var result = Result.Failure("Something went wrong");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void Failure_WithValue_ShouldCreateFailureResultWithoutValue()
    {
        // Act
        var result = Result<string>.Failure("Error occurred");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.ErrorMessage.Should().Be("Error occurred");
    }
}
