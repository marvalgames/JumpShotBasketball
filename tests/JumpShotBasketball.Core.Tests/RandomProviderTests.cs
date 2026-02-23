using FluentAssertions;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class RandomProviderTests
{
    [Fact]
    public void IntRandom_ReturnsWithinRange()
    {
        for (int i = 0; i < 1000; i++)
        {
            int result = RandomProvider.IntRandom(10);
            result.Should().BeGreaterThanOrEqualTo(1);
            result.Should().BeLessThanOrEqualTo(10);
        }
    }

    [Fact]
    public void IntRandom_WithOne_AlwaysReturnsOne()
    {
        for (int i = 0; i < 100; i++)
        {
            RandomProvider.IntRandom(1).Should().Be(1);
        }
    }

    [Fact]
    public void NextDouble_ReturnsWithinRange()
    {
        for (int i = 0; i < 1000; i++)
        {
            double result = RandomProvider.NextDouble(5.0);
            result.Should().BeGreaterThanOrEqualTo(0.0);
            result.Should().BeLessThan(5.0);
        }
    }

    [Fact]
    public void Next_ReturnsWithinRange()
    {
        for (int i = 0; i < 1000; i++)
        {
            int result = RandomProvider.Next(10);
            result.Should().BeGreaterThanOrEqualTo(0);
            result.Should().BeLessThan(10);
        }
    }
}
