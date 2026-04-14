using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TigerBeetle;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("[controller]")]
public class AccountController : ControllerBase
{
    [HttpPost(Name = "CreateAccount")]
    public IActionResult CreateAccount()
    {
        var account = new Account
        {
            Id = ID.Create(), // TigerBeetle time-based ID.
            UserData128 = 0,
            UserData64 = 0,
            UserData32 = 0,
            Ledger = 1,
            Code = 718,
            Flags = AccountFlags.None,
            Timestamp = 0,
        };

        var accountResults = TigerBeetle.Execute(client => client.CreateAccounts([account]));

        Debug.Assert(accountResults.Length == 1);

        return Ok(new
        {
            Account = account,
            AccountStatus = accountResults[0].Status.ToString(),
        });
    }
}