using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TigerBeetle;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("transactions")]
public class TransactionController : ControllerBase
{
    // POST /transactions/income
    // Records money arriving in a cash account as earned income.
    //
    // Double-entry for "receive $3,000 paycheck into cash":
    //   DEBIT  Cash           +$3,000  (asset grows — you physically have more cash)
    //   CREDIT Income:Salary  +$3,000  (income recognized — you earned this this month)
    //
    // In TigerBeetle terms: one Transfer with debit = cash account, credit = income account.
    // Both accounts' balances go UP — that's the point of double-entry. No money is created
    // or destroyed; it's recorded from two perspectives simultaneously.
    //
    // Body: { "cashAccountId": "...", "incomeAccountId": "...", "amountCents": 300000 }
    [HttpPost("income")]
    public IActionResult RecordIncome([FromBody] RecordIncomeRequest request)
    {
        Debug.Assert(UInt128.TryParse(request.CashAccountId, out var cashId));
        Debug.Assert(UInt128.TryParse(request.IncomeAccountId, out var incomeId));

        var transfer = new Transfer
        {
            Id = ID.Create(),

            // DEBIT side: the cash account (an Asset).
            // Debiting an asset increases it → cash balance goes up.
            DebitAccountId = cashId,

            // CREDIT side: the salary income account (Income).
            // Crediting an income account increases it → earned income this period goes up.
            CreditAccountId = incomeId,

            // Amount in cents. $3,000.00 = 300_000.
            // TigerBeetle is integer-only — no decimals, no floating point.
            Amount = (UInt128)request.AmountCents,

            Ledger = Ledger.Eur, // Must match both accounts' ledger.

            // Transfer code classifies the type of money movement — user-defined convention.
            Code = TransferCode.IncomeReceipt,

            Flags = TransferFlags.None,
            Timestamp = 0,
        };

        var results = TigerBeetle.Execute(client => client.CreateTransfers([transfer]));

        // CreateTransfers returns only failed results. Empty array = success.
        Debug.Assert(results.Length == 0);

        return Created($"/transactions/{transfer.Id}", new
        {
            id = transfer.Id.ToString(),
            amountCents = request.AmountCents,
            amountDollars = request.AmountCents / 100m,
        });
    }
}

public record RecordIncomeRequest(
    string CashAccountId,
    string IncomeAccountId,
    long AmountCents
);
