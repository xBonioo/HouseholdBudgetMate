using System.Globalization;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class MonthlyBudgetingLoopRenderedTests : BunitContext
{
    private const string RemainingInPlanLabel = "Pozosta\u0142o w planie";
    private const string IncompleteBalanceGuidance = "Wymagane saldo zamkni\u0119cia poprzedniego miesi\u0105ca";

    [Fact]
    public void AcceptedMonthlyState_Should_Render_ServiceProvided_Contract_Without_SafeToSpend()
    {
        var state = new MonthlyUiContractState(
            RemainingInPlan: 800.00m,
            LiveBalance: 7075.00m,
            HasCompleteBalanceBase: false,
            MissingBalanceAccountNames: ["Konto domowe", "Oszcz\u0119dno\u015bci"]);

        var cut = Render<MonthlyContractSmokeHost>(parameters => parameters
            .Add(component => component.State, state));

        cut.Find("[data-testid='remaining-in-plan']").TextContent
            .Should().Contain(RemainingInPlanLabel)
            .And.Contain("800.00 PLN");

        cut.Find("[data-testid='live-balance']").TextContent
            .Should().Contain("Live balance")
            .And.Contain("7075.00 PLN");

        cut.Find("[data-testid='incomplete-balance-guidance']").TextContent
            .Should().Contain(IncompleteBalanceGuidance)
            .And.Contain("Konto domowe")
            .And.Contain("Oszcz\u0119dno\u015bci");

        cut.Markup.Should().NotContain("Safe-to-spend");
        cut.Markup.Should().NotContain("SafeToSpend");
    }

    private sealed record MonthlyUiContractState(
        decimal RemainingInPlan,
        decimal LiveBalance,
        bool HasCompleteBalanceBase,
        IReadOnlyCollection<string> MissingBalanceAccountNames);

    private sealed class MonthlyContractSmokeHost : ComponentBase
    {
        [Parameter]
        [EditorRequired]
        public MonthlyUiContractState State { get; set; } = null!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(State);

            builder.OpenElement(0, "section");
            builder.AddAttribute(1, "aria-label", "monthly budget contract");

            builder.OpenElement(2, "p");
            builder.AddAttribute(3, "data-testid", "remaining-in-plan");
            builder.AddContent(4, $"{RemainingInPlanLabel}: {FormatCurrency(State.RemainingInPlan)}");
            builder.CloseElement();

            builder.OpenElement(5, "p");
            builder.AddAttribute(6, "data-testid", "live-balance");
            builder.AddContent(7, $"Live balance: {FormatCurrency(State.LiveBalance)}");
            builder.CloseElement();

            if (!State.HasCompleteBalanceBase)
            {
                builder.OpenElement(8, "div");
                builder.AddAttribute(9, "role", "alert");
                builder.AddAttribute(10, "data-testid", "incomplete-balance-guidance");
                builder.AddContent(
                    11,
                    $"{IncompleteBalanceGuidance}: {string.Join(", ", State.MissingBalanceAccountNames)}");
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        private static string FormatCurrency(decimal amount)
            => string.Create(
                CultureInfo.InvariantCulture,
                $"{amount:0.00} PLN");
    }
}
