namespace ExpenseTracker.Dtos;

// Request body for POST /accounts.
// `Type` maps directly to AccountType — the enum value determines Code and Flags in TigerBeetle.
public record CreateAccountRequest(AccountType Type);