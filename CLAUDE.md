# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the API
dotnet run --project ExpenseTracker/ExpenseTracker.csproj

# Build
dotnet build

# Watch mode (auto-restart on changes)
dotnet watch --project ExpenseTracker/ExpenseTracker.csproj

# Run tests (none yet)
dotnet test
```

TigerBeetle must be running before starting the API. It listens on port `3000` by default. Override with the `TB_ADDRESS` environment variable:

```bash
TB_ADDRESS=3000 dotnet run --project ExpenseTracker/ExpenseTracker.csproj
```

The API runs at `http://localhost:5282` (HTTP) or `https://localhost:7285` (HTTPS). OpenAPI is available at `/openapi/v1.json` in Development.

## Architecture

This is a personal expense tracker built on **double-entry bookkeeping**, backed by **TigerBeetle** as the financial database. The design doc is in `expense-tracker.md`; the domain guide (non-technical) is in `domain.md`.

### Current structure

Single ASP.NET Core Web API project (`ExpenseTracker/`). The design doc describes a planned multi-project layout (`Api`, `Domain`, `Infra`, `Tests`) that has not been implemented yet.

### TigerBeetle integration

`ExpenseTracker/TigerBeetle.cs` — static wrapper that creates a new `Client` per operation (short-lived, disposable). The cluster ID is always `0`; the ledger is always `1` (USD, amounts in **cents as integers**).

All account and transfer IDs use `ID.Create()` (TigerBeetle time-based IDs). These are `UInt128` values stored as the canonical identifiers throughout the system — no separate GUIDs.

### Account code ranges

```
1000–1999   Assets      (Checking=1001, Savings=1002, Cash=1003)
2000–2999   Liabilities (CreditCard=2001, Loan=2002)
3000–3999   Income      (Salary=3001, Freelance=3002, Other=3099)
4000–4999   Expenses    (user-defined)
5000–5999   Equity      (NetWorth=5001)
6000–6999   Envelopes   (user-defined)
9000–9999   System      (BudgetSource=9001)
```

### Account flags by type

| Type | TigerBeetle flags |
|---|---|
| Asset (Checking, Savings) | `credits_must_not_exceed_debits` + `history` |
| Liability (Credit Card) | `debits_must_not_exceed_credits` |
| Net Worth | `history` |
| Envelope | `debits_must_not_exceed_credits` (budget ceiling, enforced by DB) |
| BudgetSource | none (control account, allowed to go negative) |

### Amount encoding

All amounts are integers in cents. `$85.47` → `8547`. The API accepts and returns decimal strings.

### Key domain rules

- **Transfers are immutable.** Corrections are new reversing transfers tagged with a correction code — never edits or deletes.
- **Envelope budget enforcement** is done at the database level via `debits_must_not_exceed_credits` — not application code.
- **Split transactions** use `flags.linked` so all legs are atomic.
- **Pending transfers** (`flags.pending`) reserve funds before a payment clears; confirmed with post or released with void.
- **Month-end close** sweeps all Income and Expense account balances into Net Worth in one atomic linked batch, then resets Envelope balances.

### Planned hybrid data store

TigerBeetle holds all financial state (balances, transfers). PostgreSQL (not yet implemented) will hold metadata: account names, institutions, merchant names, descriptions, tags. Every TigerBeetle ID maps to a PostgreSQL row; PostgreSQL is the query/search layer, TigerBeetle is the source of truth for money.
