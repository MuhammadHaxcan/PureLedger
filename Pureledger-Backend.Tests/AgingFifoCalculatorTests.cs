using PureLedger.Services;
using PureLedger.ViewModels.Reporting;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class AgingFifoCalculatorTests
{
    [Fact]
    public void TenMillionPurchase_NineMillionPayment_FiveMillionPurchase_SixMillionPayment()
    {
        var result = AgingFifoCalculator.Calculate([
            Movement(1, debit: 10_000_000), Movement(2, credit: 9_000_000),
            Movement(3, debit: 5_000_000), Movement(4, credit: 6_000_000)]);

        Assert.Equal(0m, result.NetBalance);
        Assert.All(result.Rows, row => Assert.Equal(0m, row.RemainingDebit + row.UnappliedCredit));
        Assert.Collection(result.Allocations,
            a => Assert.Equal((1, 2, 9_000_000m), (a.DebitEntryId, a.CreditEntryId, a.Amount)),
            a => Assert.Equal((1, 4, 1_000_000m), (a.DebitEntryId, a.CreditEntryId, a.Amount)),
            a => Assert.Equal((3, 4, 5_000_000m), (a.DebitEntryId, a.CreditEntryId, a.Amount)));
        Assert.Equal(new decimal[] { 10_000_000, 1_000_000, 6_000_000, 0 }, result.Rows.Select(x => x.RunningBalance));
    }

    [Fact]
    public void PartialPayments_LeaveTheOldestDebitPartlyOpen()
    {
        var result = AgingFifoCalculator.Calculate([
            Movement(1, debit: 200_000), Movement(2, credit: 50_000),
            Movement(3, debit: 150_000), Movement(4, credit: 100_000), Movement(5, debit: 100_000)]);
        Assert.Equal(new decimal[] { 50_000, 0, 150_000, 0, 100_000 }, result.Rows.Select(x => x.RemainingDebit));
        Assert.Equal(300_000m, result.NetBalance);
        Assert.Equal(150_000m, result.Rows[0].SettledDebit);
    }

    [Fact]
    public void PrepaymentsAndOverpayments_CarryForwardInCreditOrder()
    {
        var result = AgingFifoCalculator.Calculate([
            Movement(1, credit: 120), Movement(2, credit: 50), Movement(3, debit: 150),
            Movement(4, debit: 100), Movement(5, credit: 90)]);
        Assert.Equal(-10m, result.NetBalance);
        Assert.Equal(10m, result.UnappliedCredits);
        Assert.Equal(0m, result.OutstandingDebits);
        Assert.Equal(new decimal[] { 120, 30, 20, 80 }, result.Allocations.Select(x => x.Amount));
        Assert.Equal(new[] { 1, 2, 2, 5 }, result.Allocations.Select(x => x.CreditEntryId));
    }

    [Fact]
    public void EntryDatesAndIdsDetermineOrder_NotCreditableDatesOrInputOrder()
    {
        var older = Movement(1, debit: 200); older.CreditableDate = new(2026, 12, 1);
        var newer = Movement(2, debit: 150); newer.CreditableDate = new(2026, 1, 2);
        var result = AgingFifoCalculator.Calculate([Movement(3, credit: 100), newer, older]);
        Assert.Equal(1, Assert.Single(result.Allocations).DebitEntryId);

        var a = Movement(5, debit: 30); a.EntryDate = new(2026, 1, 1); a.JournalVoucherId = 10;
        var b = Movement(4, debit: 40); b.EntryDate = a.EntryDate; b.JournalVoucherId = 10;
        var c = Movement(3, debit: 50); c.EntryDate = a.EntryDate; c.JournalVoucherId = 9;
        var tied = AgingFifoCalculator.Calculate([a, b, c, Movement(6, credit: 100)]);
        Assert.Equal(new[] { 3, 4, 5 }, tied.Allocations.Select(x => x.DebitEntryId));
        Assert.Equal(20m, tied.OutstandingDebits);
    }

    [Fact]
    public void MixedAndZeroRows_ArePreservedAndBothSidesAreAccountedFor()
    {
        var result = AgingFifoCalculator.Calculate([
            Movement(1, credit: 20), Movement(2, debit: 100, credit: 30), Movement(3)]);
        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(50m, result.NetBalance);
        Assert.Equal(50m, result.Rows[1].SettledDebit);
        Assert.Equal(30m, result.Rows[1].AppliedCredit);
        Assert.Equal(1, result.MixedEntryCount);
    }

    [Fact]
    public void FourDecimalPrecision_IsNotRoundedDuringAllocation()
    {
        var result = AgingFifoCalculator.Calculate([
            Movement(1, debit: 0.0003m), Movement(2, debit: 0.0004m), Movement(3, credit: 0.0005m)]);
        Assert.Equal(0.0002m, result.NetBalance);
        Assert.Equal(new[] { 0.0003m, 0.0002m }, result.Allocations.Select(x => x.Amount));
    }

    [Fact]
    public void EmptyHistory_HasZeroSummary()
    {
        var result = AgingFifoCalculator.Calculate([]);
        Assert.Empty(result.Rows);
        Assert.Empty(result.Allocations);
        Assert.Equal(0m, result.NetBalance);
    }

    [Fact]
    public void RandomLedger_ConservesEveryDebitCreditAndAllocation()
    {
        var random = new Random(42);
        var rows = Enumerable.Range(1, 2000).Select(id => Movement(id,
            debit: id % 2 == 0 ? random.Next(0, 50000) / 10000m : 0,
            credit: id % 2 == 1 ? random.Next(0, 50000) / 10000m : 0)).ToList();
        var result = AgingFifoCalculator.Calculate(rows);
        Assert.Equal(rows.Sum(x => x.Debit - x.Credit), result.OutstandingDebits - result.UnappliedCredits);
        foreach (var row in result.Rows)
        {
            Assert.Equal(row.Debit, row.RemainingDebit + result.Allocations.Where(x => x.DebitEntryId == row.JournalEntryId).Sum(x => x.Amount));
            Assert.Equal(row.Credit, row.UnappliedCredit + result.Allocations.Where(x => x.CreditEntryId == row.JournalEntryId).Sum(x => x.Amount));
            Assert.True(row.RemainingDebit >= 0 && row.UnappliedCredit >= 0);
        }
    }

    internal static AgingFifoMovement Movement(int id, decimal debit = 0, decimal credit = 0) => new()
    {
        JournalEntryId = id, JournalVoucherId = id,
        EntryDate = new DateOnly(2026, 1, 1).AddDays(id - 1),
        CreditableDate = new DateOnly(2026, 1, 1).AddDays(id - 1),
        Description = $"Movement {id}", Debit = debit, Credit = credit
    };
}
