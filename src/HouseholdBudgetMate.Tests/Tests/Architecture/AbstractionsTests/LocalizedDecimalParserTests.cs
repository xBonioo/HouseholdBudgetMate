using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Parsing;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.AbstractionsTests;

public sealed class LocalizedDecimalParserTests
{
    [Theory]
    [InlineData("1234,56", 1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1 234,56", 1234.56)]
    public void TryParseParsesCommonLocalizedInputs(string input, decimal expected)
    {
        var result = LocalizedDecimalParser.TryParse(input, out var value);

        result.Should().BeTrue();
        value.Should().Be(expected);
    }

    [Fact]
    public void TryParseRequiresValue()
    {
        var result = LocalizedDecimalParser.TryParse(" ", out var value);

        result.Should().BeFalse();
        value.Should().Be(0);
    }

    [Fact]
    public void TryParseOrZeroAllowsBlankValue()
    {
        var result = LocalizedDecimalParser.TryParseOrZero(" ", out var value);

        result.Should().BeTrue();
        value.Should().Be(0);
    }

    [Fact]
    public void TryParseOptionalNonNegativeRejectsNegativeValue()
    {
        var result = LocalizedDecimalParser.TryParseOptionalNonNegative("-1", out var value);

        result.Should().BeFalse();
        value.Should().BeNull();
    }
}
