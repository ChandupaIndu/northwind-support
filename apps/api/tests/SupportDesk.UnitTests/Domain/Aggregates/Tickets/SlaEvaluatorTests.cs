using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Fact]
    public void Evaluate_OpenAtAtRiskThreshold_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now.AddHours(2), null, Now, 8, 25));

    [Fact]
    public void Evaluate_OpenJustOutsideAtRiskThreshold_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(2).AddTicks(1), null, Now, 8, 25));

    [Fact]
    public void Evaluate_AtExactDueTime_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now, null, Now, 8, 25));
}
