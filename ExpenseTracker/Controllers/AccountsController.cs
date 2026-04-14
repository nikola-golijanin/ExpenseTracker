using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TigerBeetle;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("[controller]")]
public class AccountsController : ControllerBase
{
    // POST /accounts/cash
    // Creates a physical cash wallet account (an Asset account).
    //
    // Asset accounts grow on the DEBIT side.
    // Balance formula: debits_posted - credits_posted
    //   → positive when you have money, can never go below 0 (enforced by flag below).
    [HttpPost("cash")]
    public IActionResult CreateCashAccount()
    {
        var account = new Account
        {
            Id = ID.Create(), // Time-based unique ID — TigerBeetle's preferred ID strategy.

            // Ledger.Usd = our USD ledger. All amounts in this app are stored as cents.
            // Every account must share the same ledger to transact with each other.
            // TigerBeetle rejects transfers between accounts on different ledgers.
            Ledger = Ledger.Eur,

            // Code is the account category within the ledger — we define these ranges:
            //   1000-1999 = Assets  |  2000-2999 = Liabilities  |  3000-3999 = Income
            //   4000-4999 = Expenses  |  5000-5999 = Equity
            // AccountCode.Cash = 1003 (physical wallet).
            Code = AccountCode.Cash,

            // CreditsMustNotExceedDebits: TigerBeetle will reject any transfer that would
            // cause credits_posted > debits_posted on this account.
            // Since balance = debits - credits, this means the balance can never go below $0.
            // The database enforces this — no application-level overdraft check needed.
            Flags = AccountFlags.CreditsMustNotExceedDebits,

            Timestamp = 0, // 0 = let TigerBeetle assign the timestamp.
        };

        var results = TigerBeetle.Execute(client => client.CreateAccounts([account]));

        // CreateAccounts returns only failed results. Empty array = all succeeded.
        Debug.Assert(results.Length == 0);

        return Created($"/accounts/{account.Id}", new { id = account.Id.ToString() });
    }

    // POST /accounts/income?code=3001
    // Creates an Income account (salary, freelance, etc.).
    //
    // Income accounts grow on the CREDIT side.
    // Balance formula: credits_posted - debits_posted
    //   → positive when you've earned money this period.
    //
    // code: 3001=Salary, 3002=Freelance, 3099=Other
    [HttpPost("income")]
    public IActionResult CreateIncomeAccount([FromQuery] ushort code = 3001)
    {
        var account = new Account
        {
            Id = ID.Create(),
            Ledger = Ledger.Eur,  // Same ledger as all other accounts — EUR cents.
            Code = code,                // e.g. (ushort)AccountCode.Salary = 3001.

            // No balance constraints on income accounts.
            // Income accumulates credits throughout the month.
            // At month-end close, the balance is swept into Net Worth (Equity) and reset to 0.
            Flags = AccountFlags.None,

            Timestamp = 0,
        };

        var results = TigerBeetle.Execute(client => client.CreateAccounts([account]));

        // CreateAccounts returns only failed results. Empty array = all succeeded.
        Debug.Assert(results.Length == 0);

        return Created($"/accounts/{account.Id}", new { id = account.Id.ToString() });
    }

    // GET /accounts/{id}
    // Looks up an account and returns its human-readable balance.
    //
    // TigerBeetle stores raw debits_posted and credits_posted counters — it does NOT
    // compute a balance itself. That's the application's responsibility.
    // The correct formula depends on which side the account type grows on:
    //   Assets, Expenses  → grow on debit  → balance = debits_posted - credits_posted
    //   Liabilities, Income, Equity → grow on credit → balance = credits_posted - debits_posted
    [HttpGet("{id}")]
    public IActionResult GetAccount(string id)
    {
        Debug.Assert(UInt128.TryParse(id, out var accountId));

        var accounts = TigerBeetle.Execute(client => client.LookupAccounts([accountId]));

        if (accounts.Length == 0)
            return NotFound();

        var account = accounts[0];

        // Derive balance from the code range — each range maps to an account type.
        var balance = account.Code switch
        {
            // Assets (Checking=1001, Savings=1002, Cash=1003, ...): grow on debit.
            // Balance = money received minus money spent from this account.
            >= AccountCode.Checking and < AccountCode.CreditCard
                => (long)(account.DebitsPosted - account.CreditsPosted),

            // Liabilities (CreditCard=2001, Loan=2002, ...): grow on credit.
            // Balance = amount charged minus amount paid off.
            >= AccountCode.CreditCard and < AccountCode.Salary
                => (long)(account.CreditsPosted - account.DebitsPosted),

            // Income (Salary=3001, Freelance=3002, ...): grow on credit.
            // Balance = how much you've earned this period.
            >= AccountCode.Salary and < AccountCode.Housing
                => (long)(account.CreditsPosted - account.DebitsPosted),

            // Expenses (Housing=4001, Groceries=4002, ...): grow on debit.
            // Balance = how much you've spent in this category this period.
            >= AccountCode.Housing and < AccountCode.NetWorth
                => (long)(account.DebitsPosted - account.CreditsPosted),

            // Equity (NetWorth=5001): grows on credit.
            // Balance = total assets minus total liabilities — your true financial position.
            >= AccountCode.NetWorth and < AccountCode.BudgetSource
                => (long)(account.CreditsPosted - account.DebitsPosted),

            _ => 0L
        };

        return Ok(new
        {
            id = account.Id.ToString(),
            code = account.Code,
            balanceCents = balance,
            balanceEuros = balance / 100m, // All amounts stored as cents; divide for display.
            // Raw counters exposed for debugging — lets you verify the double-entry equation:
            // sum of all debitsPosted across all accounts == sum of all creditsPosted.
            debitsPosted = (long)account.DebitsPosted,
            creditsPosted = (long)account.CreditsPosted,
        });
    }
}
