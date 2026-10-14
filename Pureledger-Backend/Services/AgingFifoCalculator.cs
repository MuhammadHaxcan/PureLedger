using PureLedger.ViewModels.Reporting;

namespace PureLedger.Services;

/// <summary>Read-only ledger reconstruction. Allocation order is entry date, voucher ID, entry ID.</summary>
public static class AgingFifoCalculator
{
    public sealed class Result
    {
        public List<AgingFifoRow> Rows { get; } = new();
        public List<AgingFifoAllocation> Allocations { get; } = new();
        public decimal TotalDebits { get; internal set; }
        public decimal TotalCredits { get; internal set; }
        public decimal OutstandingDebits { get; internal set; }
        public decimal UnappliedCredits { get; internal set; }
        public decimal NetBalance => TotalDebits - TotalCredits;
        public int MixedEntryCount { get; internal set; }
    }

    public static Result Calculate(IEnumerable<AgingFifoMovement> movements, CancellationToken cancellationToken = default)
    {
        var result = new Result();
        var debits = new Queue<AgingFifoRow>();
        var credits = new Queue<AgingFifoRow>();
        decimal runningBalance = 0;

        void Apply(AgingFifoRow debit, AgingFifoRow credit)
        {
            var amount = Math.Min(debit.RemainingDebit, credit.UnappliedCredit);
            debit.RemainingDebit -= amount;
            credit.UnappliedCredit -= amount;
            debit.AllocationCount++;
            if (credit != debit) credit.AllocationCount++;
            result.Allocations.Add(new AgingFifoAllocation
            {
                DebitEntryId = debit.JournalEntryId, CreditEntryId = credit.JournalEntryId, Amount = amount,
                DebitVoucherId = debit.JournalVoucherId, CreditVoucherId = credit.JournalVoucherId,
                DebitEntryDate = debit.EntryDate, CreditEntryDate = credit.EntryDate,
                DebitDescription = debit.Description, CreditDescription = credit.Description
            });
        }

        foreach (var movement in movements.OrderBy(x => x.EntryDate).ThenBy(x => x.JournalVoucherId).ThenBy(x => x.JournalEntryId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (movement.Debit < 0 || movement.Credit < 0)
                throw new InvalidOperationException($"Entry {movement.JournalEntryId} has a negative debit or credit and cannot be allocated.");
            runningBalance += movement.Debit - movement.Credit;
            var row = new AgingFifoRow
            {
                JournalEntryId = movement.JournalEntryId, JournalVoucherId = movement.JournalVoucherId,
                EntryDate = movement.EntryDate, CreditableDate = movement.CreditableDate, Description = movement.Description,
                Debit = movement.Debit, Credit = movement.Credit, RemainingDebit = movement.Debit,
                UnappliedCredit = movement.Credit, RunningBalance = runningBalance
            };
            result.Rows.Add(row);
            result.TotalDebits += row.Debit;
            result.TotalCredits += row.Credit;
            if (row.Debit > 0 && row.Credit > 0) result.MixedEntryCount++;

            // Process debit then credit for a mixed row; preserve both gross sides.
            // Earlier unapplied credits settle subsequent debits in credit order.
            while (row.RemainingDebit > 0 && credits.Count > 0)
            {
                var credit = credits.Peek();
                Apply(row, credit);
                if (credit.UnappliedCredit == 0) credits.Dequeue();
            }
            if (row.RemainingDebit > 0) debits.Enqueue(row);
            while (row.UnappliedCredit > 0 && debits.Count > 0)
            {
                var debit = debits.Peek();
                Apply(debit, row);
                if (debit.RemainingDebit == 0) debits.Dequeue();
            }
            if (row.UnappliedCredit > 0) credits.Enqueue(row);
        }
        result.OutstandingDebits = result.Rows.Sum(x => x.RemainingDebit);
        result.UnappliedCredits = result.Rows.Sum(x => x.UnappliedCredit);
        return result;
    }
}
