using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Contracts.Users.Dto;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Moq;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class UserSessionServiceTests
{
    [Fact]
    public async Task SignInAsync_Should_Apply_Eligible_Pin_Profile_And_Set_Trusted_Cookie()
    {
        var user = CreateEligibleUser();
        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetSignInUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([user]);
        userService.Setup(x => x.ValidatePinAsync(user.Id, "1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var context = new CurrentUserContext();
        var jsRuntime = new CookieJsRuntime();
        var service = CreateService(userService.Object, context, jsRuntime);

        var result = await service.SignInAsync(user.Id, "1234", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.UserId.Should().Be(user.Id);
        context.BudgetOwnerUserId.Should().Be(User.DefaultUserId);
        jsRuntime.CookieValue.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryRestoreFromCookieAsync_Should_Reject_Technical_Owner_Even_If_Full_User_List_Contains_It()
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var jsRuntime = new CookieJsRuntime
        {
            CookieValue = dataProtectionProvider
                .CreateProtector("HouseholdBudgetMate.CurrentUserCookie")
                .Protect(User.DefaultUserId)
        };
        var technicalOwner = new UserDto
        {
            Id = User.DefaultUserId,
            Username = User.TechnicalOwnerUsername,
            BudgetOwnerUserId = User.DefaultUserId,
            IsInteractive = false,
            HasPin = false
        };
        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([technicalOwner]);
        userService.Setup(x => x.GetSignInUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var context = new CurrentUserContext();
        var service = new UserSessionService(userService.Object, context, jsRuntime, dataProtectionProvider);

        var restored = await service.TryRestoreFromCookieAsync(CancellationToken.None);

        restored.Should().BeFalse();
        service.CurrentUser.Should().BeNull();
        context.UserId.Should().BeEmpty();
        jsRuntime.CookieValue.Should().BeNull();
        userService.Verify(x => x.GetUsersAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRestoreFromCookieAsync_Should_Restore_Eligible_Trusted_Profile()
    {
        var user = CreateEligibleUser();
        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetSignInUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([user]);
        userService.Setup(x => x.ValidatePinAsync(user.Id, "1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var jsRuntime = new CookieJsRuntime();

        var signInService = new UserSessionService(
            userService.Object,
            new CurrentUserContext(),
            jsRuntime,
            dataProtectionProvider);
        (await signInService.SignInAsync(user.Id, "1234", CancellationToken.None)).IsSuccess.Should().BeTrue();

        var restoredContext = new CurrentUserContext();
        var restoreService = new UserSessionService(
            userService.Object,
            restoredContext,
            jsRuntime,
            dataProtectionProvider);

        var restored = await restoreService.TryRestoreFromCookieAsync(CancellationToken.None);

        restored.Should().BeTrue();
        restoreService.CurrentUser!.Id.Should().Be(user.Id);
        restoredContext.UserId.Should().Be(user.Id);
        restoredContext.BudgetOwnerUserId.Should().Be(User.DefaultUserId);
    }

    [Fact]
    public async Task SignOutAsync_Should_Clear_User_Scope_And_Trusted_Cookie()
    {
        var user = CreateEligibleUser();
        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetSignInUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([user]);
        userService.Setup(x => x.ValidatePinAsync(user.Id, "1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var context = new CurrentUserContext();
        var jsRuntime = new CookieJsRuntime();
        var service = CreateService(userService.Object, context, jsRuntime);
        await service.SignInAsync(user.Id, "1234", CancellationToken.None);

        await service.SignOutAsync();

        service.CurrentUser.Should().BeNull();
        context.UserId.Should().BeEmpty();
        context.BudgetOwnerUserId.Should().BeNull();
        jsRuntime.CookieValue.Should().BeNull();
    }

    private static UserSessionService CreateService(
        IUserService userService,
        CurrentUserContext context,
        CookieJsRuntime jsRuntime)
    {
        return new UserSessionService(userService, context, jsRuntime, new EphemeralDataProtectionProvider());
    }

    private static UserDto CreateEligibleUser()
    {
        return new UserDto
        {
            Id = "visible-admin",
            Username = "Administrator",
            BudgetOwnerUserId = User.DefaultUserId,
            HasPin = true,
            IsInteractive = true,
            IsAdmin = true
        };
    }

    private sealed class CookieJsRuntime : IJSRuntime
    {
        public string? CookieValue { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            object? result = null;

            switch (identifier)
            {
                case "householdBudgetMate.cookies.get":
                    result = CookieValue;
                    break;
                case "householdBudgetMate.cookies.set":
                    CookieValue = args?[1] as string;
                    break;
                case "householdBudgetMate.cookies.delete":
                    CookieValue = null;
                    break;
            }

            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }
}
