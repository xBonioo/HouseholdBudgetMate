using HouseholdBudgetMate.Abstractions;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.AbstractionsTests;

public sealed class DtoTests
{
    [Fact]
    public void DtoArePublicAndSealed()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .That()
            .AreClasses()
            .And().HaveNameEndingWith("Dto")
            .Should()
            .BeSealed().And().BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }

    [Fact]
    public void DtoAreProperlyNamed()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .Should()
            .NotHaveNameEndingWith("DTO", StringComparison.Ordinal)
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }

    [Fact]
    public void DtoAreInProperFolder()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .That()
            .AreClasses()
            .And().HaveNameEndingWith("Dto")
            .Should()
            .ResideInNamespaceEndingWith("Dto")
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }
}