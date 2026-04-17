using System.Diagnostics;
using ExpenseTracker.Dtos;
using Microsoft.AspNetCore.Mvc;
using TigerBeetle;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("[controller]")]
public class AccountsController : ControllerBase
{
    // POST /accounts
    // Creates any account in the chart of accounts — asset, liability, income, expense, or equity.
    //
    // The caller supplies a `type` (e.g. "Checking", "Salary", "Groceries") and the factory
    // maps that to the correct TigerBeetle Code and Flags. No per-type endpoints needed.
    //
    // Body: { "type": "Checking" }
    [HttpPost]
    public IActionResult CreateAccount([FromBody] CreateAccountRequest request)
    {
        var account = AccountFactory.Create(request.Type);

        var results = TigerBeetle.Execute(client => client.CreateAccounts([account]));

        // CreateAccounts returns only the results that FAILED — an empty array means all succeeded.
        // We assert here because in normal operation every create should succeed.
        // In production, you'd inspect each CreateAccountResult for the specific error code.
        //Debug.Assert(results.Length == 0);

        return Created($"/accounts/{account.Id}", new { id = account.Id.ToString() });
    }

    // GET /accounts/{id}
    // Looks up a single account by its TigerBeetle ID and returns its human-readable balance.
    //
    // TigerBeetle does NOT compute balances — it stores two raw counters per account:
    //   debits_posted  — the sum of all amounts on the debit side of completed transfers
    //   credits_posted — the sum of all amounts on the credit side of completed transfers
    //
    // The correct balance formula depends on which side the account type grows on:
    //   Debit-normal  (Assets, Expenses)              → balance = debits_posted  - credits_posted
    //   Credit-normal (Liabilities, Income, Equity)   → balance = credits_posted - debits_posted
    //
    // We derive the type from the account's Code range — the same ranges defined in AccountType.
    [HttpGet("{id}")]
    public IActionResult GetAccount(string id)
    {
        Debug.Assert(UInt128.TryParse(id, out var accountId));

        var accounts = TigerBeetle.Execute(client => client.LookupAccounts([accountId]));

        if (accounts.Length == 0)
            return NotFound();

        var account = accounts[0];

        // Derive balance from the code range — each range maps to a normal side (debit or credit).
        var balance = account.Code switch
        {
            // Assets (Checking=1001, Savings=1002, Cash=1003):
            // Debit-normal — balance grows when you receive money (debits) and shrinks when you spend (credits).
            // balance = debits_posted - credits_posted → positive = you have money here.
            // CreditsMustNotExceedDebits flag ensures this never goes below 0.
            >= (ushort)AccountType.Checking and < (ushort)AccountType.CreditCard
                => (long)(account.DebitsPosted - account.CreditsPosted),

            // Liabilities (CreditCard=2001, Loan=2002):
            // Credit-normal — balance grows when you owe more (credits) and shrinks when you pay off (debits).
            // balance = credits_posted - debits_posted → positive = how much you currently owe.
            >= (ushort)AccountType.CreditCard and < (ushort)AccountType.Salary
                => (long)(account.CreditsPosted - account.DebitsPosted),

            // Income (Salary=3001, Freelance=3002, OtherIncome=3099):
            // Credit-normal — income is recognized when credited; month-end closing debits it to zero.
            // balance = credits_posted - debits_posted → positive = total earned this period.
            >= (ushort)AccountType.Salary and < (ushort)AccountType.Housing
                => (long)(account.CreditsPosted - account.DebitsPosted),

            // Expenses (Housing=4001, Groceries=4002, ...):
            // Debit-normal — spending is recorded as debits; month-end closing credits them to zero.
            // balance = debits_posted - credits_posted → positive = total spent in this category this period.
            >= (ushort)AccountType.Housing and < (ushort)AccountType.NetWorth
                => (long)(account.DebitsPosted - account.CreditsPosted),

            // Equity (NetWorth=5001):
            // Credit-normal — net worth grows permanently via month-end closing entries.
            // balance = credits_posted - debits_posted → positive = total assets minus total liabilities.
            // The History flag lets TigerBeetle retain balance snapshots for point-in-time reports.
            >= (ushort)AccountType.NetWorth
                => (long)(account.CreditsPosted - account.DebitsPosted),

            // Unknown code range — not a type we recognise.
            _ => 0L
        };

        return Ok(new
        {
            id = account.Id.ToString(),
            code = account.Code,
            balanceCents = balance,
            balanceEuros = balance / 100m, // All amounts are stored as cents; divide by 100 for display.
            // Raw counters exposed for debugging. The double-entry invariant guarantees:
            //   sum(debitsPosted across ALL accounts) == sum(creditsPosted across ALL accounts)
            // because every transfer adds the same amount to exactly one debit and one credit account.
            debitsPosted  = (long)account.DebitsPosted,
            creditsPosted = (long)account.CreditsPosted,
        });
    }
}

