using System.Diagnostics.CodeAnalysis;
using HouseholdBudgetMate.Abstractions;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.AbstractionsTests;


[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
public sealed class RequestsTests
{
    [Fact]
    public void RequestsArePublicAndNotSealed()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .That()
            .AreClasses()
                .And().HaveNameEndingWith("Request")
            .Should()
                .BePublic()
                .And().NotBeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }

    [Fact]
    public void RequestsAreInProperFolder()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .That()
            .AreClasses()
                .And().HaveNameEndingWith("Request")
            .And().DoNotHaveName("TableRequest")
            .Should()
                .ResideInNamespaceEndingWith("Requests")
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }
}
