using TigerBeetle;

namespace ExpenseTracker;

// AccountFactory centralises the mapping from AccountType to TigerBeetle Account configuration.
// Each account type has a fixed Code (its position in the chart of accounts) and a fixed set
// of Flags that encode balance constraints and history tracking at the database level.
//
// This is a factory, not a builder — the type discriminator fully determines the output.
// There is no incremental assembly step; it's a pure lookup.
public static class AccountFactory
{
    public static Account Create(AccountType type) => type switch
    {
        // TigerBeetle's time-based ID. Monotonically increasing, globally unique,
        // and doubles as a creation timestamp — no separate created_at column needed.
        // account.Id = ID.Create();
        
        // Every account must be on the same ledger to transact with each other.
        // Ledger.Eur = 1 — all amounts in this app are stored as euro cents (integers).
        // TigerBeetle rejects transfers between accounts on different ledgers at the DB level.
        // account.Ledger = Ledger.Eur;
        
        // 0 = let TigerBeetle assign the commit timestamp.
        // Never set this manually unless replaying historical data.
        // account.Timestamp = 0;

        
        // ── Assets ────────────────────────────────────────────────────────────────────────────
        // Debit-normal. CreditsMustNotExceedDebits prevents the balance from going below zero —
        // overdrafts are rejected by TigerBeetle, not by application code.
        // History flag on Checking and Savings enables point-in-time balance snapshots
        // (useful for "account balance on date X" without replaying all transfers).
        AccountType.Checking => new Account { Id = ID.Create(), Code = (ushort)AccountType.Checking, Ledger = Ledger.Eur, Flags = AccountFlags.CreditsMustNotExceedDebits | AccountFlags.History, Timestamp = 0},
        AccountType.Savings  => new Account { Id = ID.Create(), Code = (ushort)AccountType.Savings,  Ledger = Ledger.Eur, Flags = AccountFlags.CreditsMustNotExceedDebits | AccountFlags.History, Timestamp = 0},
        AccountType.Cash     => new Account { Id = ID.Create(), Code = (ushort)AccountType.Cash,     Ledger = Ledger.Eur, Flags = AccountFlags.CreditsMustNotExceedDebits },

        // ── Liabilities ───────────────────────────────────────────────────────────────────────
        // Credit-normal. DebitsMustNotExceedCredits prevents paying off more than you owe —
        // the balance (amount owed) can never go negative.
        AccountType.CreditCard => new Account { Id = ID.Create(), Code = (ushort)AccountType.CreditCard, Ledger = Ledger.Eur, Flags = AccountFlags.DebitsMustNotExceedCredits, Timestamp = 0 },
        AccountType.Loan       => new Account { Id = ID.Create(), Code = (ushort)AccountType.Loan,       Ledger = Ledger.Eur, Flags = AccountFlags.DebitsMustNotExceedCredits, Timestamp = 0 },

        // ── Income ────────────────────────────────────────────────────────────────────────────
        // Credit-normal, no constraints. Income accumulates credits throughout an accounting period.
        // At month-end close, the balance is swept into Net Worth (Equity) with a debit entry,
        // resetting the account to zero for the next period.
        AccountType.Salary      => new Account { Id = ID.Create(), Code = (ushort)AccountType.Salary,      Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.Freelance   => new Account { Id = ID.Create(), Code = (ushort)AccountType.Freelance,   Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.OtherIncome => new Account { Id = ID.Create(), Code = (ushort)AccountType.OtherIncome, Ledger = Ledger.Eur, Flags = AccountFlags.None , Timestamp = 0},

        // ── Expenses ──────────────────────────────────────────────────────────────────────────
        // Debit-normal, no constraints. Spending accumulates debits throughout an accounting period.
        // At month-end close, the balance is swept into Net Worth with a credit entry,
        // resetting the account to zero for the next period.
        AccountType.Housing        => new Account { Id = ID.Create(), Code = (ushort)AccountType.Housing,        Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.Groceries      => new Account { Id = ID.Create(), Code = (ushort)AccountType.Groceries,      Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.Restaurants    => new Account { Id = ID.Create(), Code = (ushort)AccountType.Restaurants,    Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.Transportation => new Account { Id = ID.Create(), Code = (ushort)AccountType.Transportation, Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.Entertainment  => new Account { Id = ID.Create(), Code = (ushort)AccountType.Entertainment,  Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.Utilities      => new Account { Id = ID.Create(), Code = (ushort)AccountType.Utilities,      Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.Healthcare     => new Account { Id = ID.Create(), Code = (ushort)AccountType.Healthcare,     Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },
        AccountType.OtherExpense   => new Account { Id = ID.Create(), Code = (ushort)AccountType.OtherExpense,   Ledger = Ledger.Eur, Flags = AccountFlags.None, Timestamp = 0 },

        // ── Equity ────────────────────────────────────────────────────────────────────────────
        // Credit-normal. Net Worth grows permanently via month-end closing entries — it is never
        // reset. The History flag retains balance snapshots at each closing entry, enabling
        // "net worth over time" queries via TigerBeetle's get_account_balances — no aggregation needed.
        AccountType.NetWorth => new Account { Code = (ushort)AccountType.NetWorth, Ledger = Ledger.Eur, Flags = AccountFlags.History, Timestamp = 0 },

        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}