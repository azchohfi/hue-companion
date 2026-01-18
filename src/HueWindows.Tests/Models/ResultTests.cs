using FluentAssertions;
using HueWindows.Core.Models;
using Xunit;

namespace HueWindows.Tests.Models;

public class ResultTests
{
    public class GenericResult
    {
        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Arrange & Act
            var result = Result<int>.Success(42);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Value.Should().Be(42);
            result.Error.Should().BeNull();
        }

        [Fact]
        public void Failure_CreatesFailedResult()
        {
            // Arrange & Act
            var result = Result<int>.Failure("Something went wrong");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Value.Should().Be(default);
            result.Error.Should().Be("Something went wrong");
        }

        [Fact]
        public void GetValueOrDefault_ReturnsValueOnSuccess()
        {
            // Arrange
            var result = Result<string>.Success("hello");

            // Act
            var value = result.GetValueOrDefault("default");

            // Assert
            value.Should().Be("hello");
        }

        [Fact]
        public void GetValueOrDefault_ReturnsDefaultOnFailure()
        {
            // Arrange
            var result = Result<string>.Failure("error");

            // Act
            var value = result.GetValueOrDefault("default");

            // Assert
            value.Should().Be("default");
        }

        [Fact]
        public void ImplicitBoolConversion_ReturnsTrueForSuccess()
        {
            // Arrange
            var result = Result<int>.Success(1);

            // Act & Assert
            if (result)
            {
                // Should enter this branch
                true.Should().BeTrue();
            }
            else
            {
                throw new Exception("Should not reach here");
            }
        }

        [Fact]
        public void ImplicitBoolConversion_ReturnsFalseForFailure()
        {
            // Arrange
            var result = Result<int>.Failure("error");

            // Act & Assert
            if (result)
            {
                throw new Exception("Should not reach here");
            }
            else
            {
                // Should enter this branch
                true.Should().BeTrue();
            }
        }
    }

    public class NonGenericResult
    {
        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Arrange & Act
            var result = Result.Success();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Error.Should().BeNull();
        }

        [Fact]
        public void Failure_CreatesFailedResult()
        {
            // Arrange & Act
            var result = Result.Failure("Operation failed");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("Operation failed");
        }

        [Fact]
        public void ImplicitBoolConversion_WorksCorrectly()
        {
            // Arrange
            var success = Result.Success();
            var failure = Result.Failure("error");

            // Assert
            ((bool)success).Should().BeTrue();
            ((bool)failure).Should().BeFalse();
        }
    }
}
