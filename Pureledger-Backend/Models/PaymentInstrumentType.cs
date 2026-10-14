namespace PureLedger.Models;

public enum PaymentInstrumentType
{
    Cash,
    Cheque,
    BankTransfer,
    WireTransfer
}

public enum ApprovalVoucherType
{
    ManualExpense,
    PaymentReceipt
}
