using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using HouseholdBudgetMate.Abstractions;
using HouseholdBudgetMate.Abstractions.Models;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.AbstractionsTests;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
public sealed class ResultsTests
{
    [Fact]
    public void ResultsArePublicAndSealed()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .That()
            .AreClasses()
                .And().HaveNameEndingWith("Result")
            .Should()
                .BeSealed()
                .And().BePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }

    // [Fact]
    // public void ResultsExtendsServiceResult()
    // {
    //     var result = Types.InAssembly(typeof(IServiceResult).Assembly)
    //         .That()
    //         .AreClasses()
    //         .And().HaveNameEndingWith("Result")
    //         .Should()
    //         .Inherit(typeof(BaseResponse<>))
    //         .GetResult();
    //
    //     Assert.True(result.IsSuccessful, result.FailingTypes is not null
    //         ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
    //         : ""
    //     );
    // }

    [Fact]
    public void ResultsAreInProperFolder()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .That()
            .AreClasses()
                .And().HaveNameEndingWith("Result")
            .Should()
                .ResideInNamespaceEndingWith("Responses")
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }
}
