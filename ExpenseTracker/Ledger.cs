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
//   9000-9999  System      — internal control accounts
public static class AccountCode
{
    // Assets — physical money and bank balances you own
    public const ushort Checking = 1001;
    public const ushort Savings  = 1002;
    public const ushort Cash     = 1003;

    // Liabilities — money you owe
    public const ushort CreditCard = 2001;
    public const ushort Loan       = 2002;

    // Income — sources of money earned this accounting period
    public const ushort Salary      = 3001;
    public const ushort Freelance   = 3002;
    public const ushort OtherIncome = 3099;

    // Expenses — spending categories for this accounting period
    // (user-defined at runtime; these are well-known defaults)
    public const ushort Housing        = 4001;
    public const ushort Groceries      = 4002;
    public const ushort Restaurants    = 4003;
    public const ushort Transportation = 4004;
    public const ushort Entertainment  = 4005;
    public const ushort Utilities      = 4006;
    public const ushort Healthcare     = 4007;
    public const ushort OtherExpense   = 4099;

    // Equity — permanent net worth (assets minus liabilities)
    public const ushort NetWorth = 5001;

    // Envelopes — budget buckets; debits_must_not_exceed_credits enforces the spending cap
    // (codes assigned at runtime when a user creates a budget envelope)

    // System — internal control accounts not visible to the user
    public const ushort BudgetSource = 9001; // absorbs and releases envelope allocations; allowed to go negative
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
