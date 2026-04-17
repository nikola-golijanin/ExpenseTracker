namespace ExpenseTracker;

// The ledger partitions accounts by currency. Accounts on different ledgers cannot
// transact with each other — TigerBeetle enforces this at the database level.
// All amounts within a ledger are stored in that currency's smallest unit (cents for EUR).
public static class Ledger
{
    public const uint Eur = 1;
}

// Account codes identify the category of an account within a ledger.
// The code range determines account type, which in turn determines:
//   - Which side the account grows on (debit vs credit)
//   - The balance formula (debits_posted - credits_posted, or vice versa)
//   - Which TigerBeetle flags are appropriate
//
// Ranges:
//   1000-1999  Assets      — grow on debit,  balance = debits - credits
//   2000-2999  Liabilities — grow on credit, balance = credits - debits
//   3000-3999  Income      — grow on credit, balance = credits - debits
//   4000-4999  Expenses    — grow on debit,  balance = debits - credits
//   5000-5999  Equity      — grow on credit, balance = credits - debits
//   6000-6999  Envelopes   — grow on credit, balance = credits - debits (budget remaining)
public enum AccountType : ushort
{
    // Assets — physical money and bank balances you own
    Checking = 1001,
    Savings  = 1002,
    Cash     = 1003,

    // Liabilities — money you owe
    CreditCard = 2001,
    Loan       = 2002,

    // Income — sources of money earned this accounting period
    Salary      = 3001,
    Freelance   = 3002,
    OtherIncome = 3099,

    // Expenses — spending categories for this accounting period
    // (user-defined at runtime; these are well-known defaults)
    Housing        = 4001,
    Groceries      = 4002,
    Restaurants    = 4003,
    Transportation = 4004,
    Entertainment  = 4005,
    Utilities      = 4006,
    Healthcare     = 4007,
    OtherExpense   = 4099,

    // Equity — permanent net worth (assets minus liabilities)
    NetWorth = 5001,

    // Envelopes — budget buckets; debits_must_not_exceed_credits enforces the spending cap
    // (codes assigned at runtime when a user creates a budget envelope)
}

// Transfer codes classify what kind of money movement a transfer represents.
// User-defined — these are our application conventions.
public static class TransferCode
{
    public const ushort IncomeReceipt     = 1; // Money earned arriving into an asset account
    public const ushort Expense           = 2; // Money spent from an asset account
    public const ushort AccountTransfer   = 3; // Moving money between your own accounts (no income/expense)
    public const ushort CreditCardPayment = 4; // Paying down a credit card balance from a bank account
    public const ushort BudgetAllocation  = 5; // Filling an envelope from BudgetSource at period start
    public const ushort Correction        = 6; // Reversing entry that corrects a previously recorded transfer
    public const ushort PeriodClose       = 7; // Month-end sweep of income/expense balances into Net Worth
}
