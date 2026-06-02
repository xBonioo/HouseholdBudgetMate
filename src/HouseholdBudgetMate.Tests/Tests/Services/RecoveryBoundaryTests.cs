using System.Net;
using System.Text.Json;
using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Contracts.Users.Dto;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Tests.Shared;
using HouseholdBudgetMate.Web.Services;
using HouseholdBudgetMate.Web.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class RecoveryBoundaryTests
{
    private const string VisibleAdminId = "visible-admin";
    private const string VisibleAdminUsername = "Recovered Admin";
    private const string OldPin = "1234";
    private const string NewPin = "5678";
    private const string SharedAccountName = "Shared house account";

    [Fact]
    public async Task TryRestoreFromCookieAsync_Should_Fail_Closed_After_Recovery_And_Clear_Stale_Cookie()
    {
        var scenario = await CreateScenarioAsync();

        var signInContext = new CurrentUserContext();
        var signInSession = CreateSessionService(scenario, signInContext, scenario.CookieRuntime);
        (await signInSession.SignInAsync(VisibleAdminId, OldPin, CancellationToken.None)).IsSuccess.Should().BeTrue();

        scenario.CookieRuntime.CookieValue.Should().NotBeNullOrWhiteSpace();

        var recoveryResult = await scenario.AccessRecoveryService.RecoverAdministratorAsync(
            VisibleAdminUsername,
            NewPin,
            IssueLocalGrant(scenario.LocalAccessGrantService),
            CancellationToken.None);

        recoveryResult.IsSuccess.Should().BeTrue();
        scenario.RuntimeState.IsLocalAccessRecoveryEnabled.Should().BeFalse();

        var restoreContext = new CurrentUserContext();
        var restoreSession = CreateSessionService(scenario, restoreContext, scenario.CookieRuntime);

        var restored = await restoreSession.TryRestoreFromCookieAsync(CancellationToken.None);

        restored.Should().BeFalse();
        restoreSession.CurrentUser.Should().BeNull();
        restoreContext.UserId.Should().BeEmpty();
        restoreContext.BudgetOwnerUserId.Should().BeNull();
        scenario.CookieRuntime.CookieValue.Should().BeNull();

        await using var unauthorizedDbContext = CreateDbContext(scenario.DatabaseName, restoreContext);
        (await unauthorizedDbContext.Accounts.ToListAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task SignInAsync_Should_Use_Recovered_Pin_And_Preserve_Default_User_Budget_Scope()
    {
        var scenario = await CreateScenarioAsync();

        var recoveryResult = await scenario.AccessRecoveryService.RecoverAdministratorAsync(
            VisibleAdminUsername,
            NewPin,
            IssueLocalGrant(scenario.LocalAccessGrantService),
            CancellationToken.None);

        recoveryResult.IsSuccess.Should().BeTrue();

        var recoveredContext = new CurrentUserContext();
        var sessionService = CreateSessionService(scenario, recoveredContext, scenario.CookieRuntime);

        var signIn = await sessionService.SignInAsync(VisibleAdminId, NewPin, CancellationToken.None);

        signIn.IsSuccess.Should().BeTrue();
        sessionService.CurrentUser.Should().NotBeNull();
        sessionService.CurrentUser!.Id.Should().Be(VisibleAdminId);
        recoveredContext.UserId.Should().Be(VisibleAdminId);
        recoveredContext.BudgetOwnerUserId.Should().Be(User.DefaultUserId);

        await using var visibleScopeDbContext = CreateDbContext(scenario.DatabaseName, recoveredContext);
        var accounts = await visibleScopeDbContext.Accounts.ToListAsync(CancellationToken.None);
        accounts.Should().ContainSingle(x => x.UserId == User.DefaultUserId && x.Name == SharedAccountName);
    }

    [Fact]
    public async Task GetSignInUsersAsync_Should_Exclude_Technical_Owner_After_Recovery()
    {
        var scenario = await CreateScenarioAsync();

        var recoveryResult = await scenario.AccessRecoveryService.RecoverAdministratorAsync(
            VisibleAdminUsername,
            NewPin,
            IssueLocalGrant(scenario.LocalAccessGrantService),
            CancellationToken.None);

        recoveryResult.IsSuccess.Should().BeTrue();

        var signInUsers = await scenario.UserService.GetSignInUsersAsync(CancellationToken.None);

        signInUsers.Should().ContainSingle(x => x.Id == VisibleAdminId);
        signInUsers.Should().NotContain(x => x.Id == User.DefaultUserId);
        (await scenario.UserService.ValidatePinAsync(User.DefaultUserId, NewPin, CancellationToken.None)).Should().BeFalse();
    }

    private static async Task<RecoveryBoundaryScenario> CreateScenarioAsync()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var runtimeState = CreateRuntimeState(localAccessRecoveryEnabled: true);
        var cookieRuntime = new CookieJsRuntime();
        var localAccessGrantService = new LocalAccessGrantService();
        var factory = TestDbContextFactory.CreateFactory(databaseName);

        await using (var seedContext = CreateDbContext(databaseName, CurrentUserContext.ForTechnicalOwner()))
        {
            seedContext.Users.AddRange(
                new User
                {
                    Id = User.DefaultUserId,
                    Username = User.TechnicalOwnerUsername,
                    PasswordHash = string.Empty,
                    BudgetOwnerUserId = User.DefaultUserId,
                    IsAdmin = false
                },
                new User
                {
                    Id = VisibleAdminId,
                    Username = VisibleAdminUsername,
                    PasswordHash = PinHasher.Hash(OldPin),
                    BudgetOwnerUserId = User.DefaultUserId,
                    HouseholdMode = (int)HouseholdMode.SharedBudget,
                    IsAdmin = true
                });

            seedContext.Accounts.Add(new Account
            {
                Name = SharedAccountName,
                Order = 1,
                Type = (int)AccountType.Bank,
                UserId = User.DefaultUserId
            });

            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        return new RecoveryBoundaryScenario(
            databaseName,
            runtimeState,
            localAccessGrantService,
            cookieRuntime,
            new UserService(factory, CurrentUserContext.ForTechnicalOwner()),
            new AccessRecoveryService(runtimeState, factory, localAccessGrantService));
    }

    private static UserSessionService CreateSessionService(
        RecoveryBoundaryScenario scenario,
        CurrentUserContext currentUserContext,
        CookieJsRuntime jsRuntime)
    {
        return new UserSessionService(
            scenario.UserService,
            currentUserContext,
            jsRuntime,
            scenario.DataProtectionProvider);
    }

    private static ApplicationDbContext CreateDbContext(string databaseName, CurrentUserContext currentUserContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new ApplicationDbContext(options, currentUserContext);
    }

    private static RuntimeConfigurationState CreateRuntimeState(bool localAccessRecoveryEnabled)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hbm-recovery-boundary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var state = new RuntimeConfigurationState(directory);
        var config = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = new RuntimeDatabaseConfiguration
            {
                Host = "localhost",
                Port = 5432,
                Username = "test",
                Password = "test",
                Database = "hbm"
            },
            LocalAccessRecoveryEnabled = localAccessRecoveryEnabled
        };

        File.WriteAllText(
            state.ConfigFilePath,
            JsonSerializer.Serialize(config, RuntimeConfigurationState.JsonOptions));
        state.ReloadFromDisk();
        return state;
    }

    private static string IssueLocalGrant(LocalAccessGrantService grants)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        return grants.IssueGrantForRequest(context, LocalAccessPurposes.AccessRecovery)!;
    }

    private sealed record RecoveryBoundaryScenario(
        string DatabaseName,
        RuntimeConfigurationState RuntimeState,
        LocalAccessGrantService LocalAccessGrantService,
        CookieJsRuntime CookieRuntime,
        UserService UserService,
        AccessRecoveryService AccessRecoveryService)
    {
        public IDataProtectionProvider DataProtectionProvider { get; } = new EphemeralDataProtectionProvider();
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
