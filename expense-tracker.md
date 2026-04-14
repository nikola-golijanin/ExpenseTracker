# Personal Expense Tracker — Design Document

## Overview

A personal expense tracker built on **double-entry bookkeeping** and backed by **TigerBeetle** as the financial database. This is not "income minus spending equals balance" — it is a real ledger that models your complete financial picture: assets, liabilities, income, expenses, and equity. Every money event is recorded as a double-entry transfer. The ledger is always self-consistent.

This document covers requirements, domain model, accounting mechanics, and API contracts.

---

## Why Build It This Way

Most expense trackers store spending as a simple list and derive balance by summing. This breaks down quickly:

- You can't model credit cards properly (the purchase and the payoff are different events)
- You can't see your true net worth (assets minus liabilities)
- You can't model budget envelopes atomically
- There is no audit trail — records can be edited or deleted
- Correcting a mistake means changing history

Double-entry solves all of this. Every event is immutable. Every event affects exactly two accounts. The ledger equation `Assets = Liabilities + Equity` always holds. You derive net worth, spending, income, and cash flow all from the same log of transfers.

TigerBeetle is the right persistence layer because it enforces these invariants at the database level, not just in application code. Balance constraints, atomic multi-leg transactions, and immutability are built in — not bolted on.

---

## Learning Goals

1. Model a real chart of accounts for personal finance
2. Understand how liabilities (credit cards) work in double-entry
3. Implement budget envelopes using account balance constraints — enforced at the DB level
4. Use two-phase transfers for future/tentative money movements
5. Implement month-end closing entries
6. Build a hybrid architecture: TigerBeetle for financial state, PostgreSQL for metadata

---

## Domain Model

### The Chart of Accounts

Every account belongs to one of five categories. The category determines how balance is calculated and which direction means "growing."

| Category | Balance formula | Grows with | Examples |
|---|---|---|---|
| **Asset** | `debits − credits` | Debits | Checking, Savings, Cash |
| **Liability** | `credits − debits` | Credits | Credit card balance, Loan |
| **Income** | `credits − debits` | Credits | Salary, Freelance, Dividends |
| **Expense** | `debits − credits` | Debits | Rent, Groceries, Restaurants |
| **Equity** | `credits − debits` | Credits | Net Worth (permanent) |

There are also two system-internal account types:

| Category | Purpose |
|---|---|
| **Envelope** | A budget allocation account — credit-normal, balance = allocated − spent |
| **BudgetSource** | A control account that funds and absorbs envelopes — no balance constraint |

### The Fundamental Invariant

At all times:

```
Sum(all Asset balances) = Sum(all Liability balances) + Equity balance
```

Every transfer you record maintains this. If it does not, something is wrong.

### Accounting Periods

Income and Expense accounts accumulate within an accounting period (typically a month). At period end, a **closing entry** zeros them out into Equity (Net Worth), recording the period's profit or loss permanently. This is why your net worth grows over time even though income/expense accounts reset each month.

---

## Core Operations

### 1. Receiving Income

Your paycheck of $3,000 arrives in your checking account.

```
DEBIT   Checking Account     $3,000   (asset increases)
CREDIT  Income: Salary       $3,000   (income recognized)
```

One transfer. Checking grows (debit to a debit-normal account). Salary income grows (credit to a credit-normal account).

### 2. Paying an Expense Directly (Debit Card)

You spend $85.47 on groceries, taken directly from checking.

```
DEBIT   Expense: Groceries   $85.47   (expense recorded)
CREDIT  Checking Account     $85.47   (asset decreases)
```

### 3. Credit Card Purchase

You spend $45 at a restaurant using your Visa. No money leaves your bank yet.

```
DEBIT   Expense: Restaurants  $45.00  (expense recorded)
CREDIT  Liability: Visa        $45.00  (you owe more to Visa)
```

Checking is untouched. The expense is recorded at the moment you swipe, not when you pay the bill.

### 4. Paying Off a Credit Card

You pay $200 toward your Visa balance from checking.

```
DEBIT   Liability: Visa       $200.00  (you owe less)
CREDIT  Checking Account      $200.00  (asset decreases)
```

No expense is recorded here — it was already recorded when you swiped the card. This is just a balance sheet transfer.

### 5. Transfer Between Your Own Accounts

You move $500 from checking to savings.

```
DEBIT   Savings Account       $500.00  (asset increases)
CREDIT  Checking Account      $500.00  (asset decreases)
```

No income, no expense. Your total assets are unchanged.

### 6. Splitting a Transaction (Multi-Category)

Your rent payment of $2,000 covers rent and utilities on one bank transfer. Two linked transfers (both succeed or both fail):

```
T1 (linked): DEBIT Expense: Housing    $1,700   CREDIT Checking   $1,700
T2:          DEBIT Expense: Utilities  $300     CREDIT Checking   $300
```

TigerBeetle's `flags.linked` chains these atomically. If either fails, neither posts.

### 7. Recording an Expense with Envelope Budget Check

The budget check is embedded atomically in the transaction itself. If the Groceries envelope is exhausted, the entire transaction is rejected at the database level — no application-level code needed.

Two linked transfers (T1 is the budget check, T2 is the financial reality):

```
T1 (linked): DEBIT Envelope: Groceries  $85.47   CREDIT BudgetSource  $85.47
T2:          DEBIT Expense: Groceries   $85.47   CREDIT Checking      $85.47
```

T1 fails (and takes T2 with it) if `Envelope: Groceries` has `debits_must_not_exceed_credits` set and the debit would exceed the allocated credits. This is enforced by TigerBeetle — not by a balance check in the API layer.

### 8. Allocating Budget for the Month

At the start of each month, fill the envelopes:

```
T1 (linked): DEBIT BudgetSource   CREDIT Envelope: Groceries    $400
T2 (linked): DEBIT BudgetSource   CREDIT Envelope: Restaurants  $200
T3:          DEBIT BudgetSource   CREDIT Envelope: Entertainment $100
```

BudgetSource is a control account with no balance constraint — it can go negative. It nets to zero over the period when all envelopes are spent and then closed.

### 9. Correcting a Mistake

Transfers are immutable. Corrections are new transfers in the opposite direction, tagged with a "correction" code:

```
Original:   DEBIT Expense: Restaurants  $45   CREDIT Checking $45
Correction: DEBIT Checking              $45   CREDIT Expense: Restaurants $45  [code=correction]
```

The full history is preserved. Your reporting layer can filter out matched correction pairs to show only the "net" picture.

### 10. Month-End Closing

Income and Expense accounts are zeroed into Net Worth (Equity). The balancing transfers are linked so the entire close is atomic:

```
T1 (linked): DEBIT Income: Salary     $3,000   CREDIT Net Worth  $3,000
T2 (linked): DEBIT Net Worth          $85.47   CREDIT Expense: Groceries  $85.47
T3 (linked): DEBIT Net Worth          $45.00   CREDIT Expense: Restaurants $45.00
... (one transfer per non-zero income/expense account)
```

After closing, Income and Expense accounts are at zero. Net Worth reflects the permanent gain.

For accounts with `flags.history` set, TigerBeetle retains balance snapshots, enabling "net worth over time" reports without any application-level aggregation.

---

## Why TigerBeetle Fits This Domain

### What TigerBeetle Gives for Free

| Feature | Where it applies in this app |
|---|---|
| `flags.debits_must_not_exceed_credits` | Envelope budget enforcement — no overspend, no application code |
| `flags.credits_must_not_exceed_debits` | Asset accounts can't go below zero (optional overdraft prevention) |
| `flags.linked` | Split transactions, budget-check + expense atomicity |
| `flags.pending` | Future/tentative transactions (upcoming rent, recurring payments) |
| `flags.history` | Net-worth-over-time without re-scanning transfers |
| Immutable transfers | Full audit trail; corrections via new transfers only |
| O(1) balance reads | Account balance is always `debits_posted − credits_posted` |

### Two-Phase Transfers: Upcoming and Tentative Payments

For a recurring expense like rent that you know is coming on the 1st:

1. On the 28th: create a **pending** transfer (rent reserved, balance locked)
2. On the 1st: **post** when the bank clears it, or **void** if it bounced

The funds are reserved before the payment clears, giving you an accurate "available balance" at all times.

### What TigerBeetle Cannot Store

TigerBeetle has no free-text fields. It stores numbers. Everything rich — merchant names, descriptions, tags, notes, institution names, categories by human-readable label — lives in PostgreSQL.

### Hybrid Data Architecture

```
TigerBeetle                         PostgreSQL (metadata)
────────────────────────            ──────────────────────────────────
Account { id, ledger,               financial_accounts { tb_id, user_id,
  code, flags, debits,                name, institution, type }
  credits, user_data }
                                    categories { id, user_id, name,
Transfer { id, debit_id,              code, type }
  credit_id, amount, code,
  user_data, timestamp }            transaction_meta { tb_group_id,
                                      user_id, description, merchant,
                                      tags, notes }

                                    envelope_meta { tb_id, user_id,
                                      name, category_code,
                                      monthly_target }
```

Every TigerBeetle transfer ID is also stored in `transaction_meta`. Every TigerBeetle account ID maps to a row in `financial_accounts` or `envelope_meta`. PostgreSQL is the query layer; TigerBeetle is the source of truth for all balances and money movements.

---

## Technical Specification

### Tech Stack

- **Runtime**: .NET 10
- **API**: ASP.NET Core minimal APIs
- **Financial DB**: TigerBeetle (via `tigerbeetle` NuGet package)
- **Metadata DB**: PostgreSQL via EF Core

### Projects

```
PersonalExpenseTracker/
  PersonalExpenseTracker.Api/       ASP.NET Core web API
  PersonalExpenseTracker.Domain/    Domain model, account/transfer builders, use cases
  PersonalExpenseTracker.Infra/     TigerBeetle client wrapper, PostgreSQL repositories
  PersonalExpenseTracker.Tests/     xUnit integration tests
```

### TigerBeetle Configuration

```
Cluster ID: 0
Ledger:     1  (USD, asset scale 2 — all amounts in cents)
```

### Account Code Ranges

```
1000–1999   Assets        (Checking=1001, Savings=1002, Cash=1003)
2000–2999   Liabilities   (CreditCard=2001, Loan=2002)
3000–3999   Income        (Salary=3001, Freelance=3002, Other=3099)
4000–4999   Expenses      (user-defined; codes assigned per category at creation)
5000–5999   Equity        (NetWorth=5001)
6000–6999   Envelopes     (user-defined; codes assigned per envelope at creation)
9000–9999   System        (BudgetSource=9001)
```

### Account Flags by Type

| Account type | TB flags |
|---|---|
| Asset (Checking, Savings) | `credits_must_not_exceed_debits` + `history` |
| Asset (Cash) | `credits_must_not_exceed_debits` |
| Liability (Credit Card) | `debits_must_not_exceed_credits` |
| Income | none |
| Expense | none |
| Net Worth | `history` (track equity over time) |
| Envelope | `debits_must_not_exceed_credits` (budget ceiling) |
| BudgetSource | none (control account; allowed to go negative) |

### user_data Usage

| Field | On Account | On Transfer |
|---|---|---|
| `user_data_128` | userId (link to app user) | Transaction group ID (links all legs of one logical event) |
| `user_data_64` | — | Real-world timestamp (when the event happened externally) |
| `user_data_32` | — | — |

### Amount Encoding

All amounts in **cents** (integer). API accepts and returns decimal strings.

```
$85.47  →  TigerBeetle amount: 8547
$3,000  →  TigerBeetle amount: 300000
```

### ID Strategy

Use TigerBeetle's time-based `ID.Create()` for all account and transfer IDs. Store these UInt128 values in PostgreSQL as `uuid` or `numeric`. Do not use separate GUIDs — the TigerBeetle ID is the canonical identifier throughout the system.

---

## API Contracts

All amounts are strings in decimal format: `"85.47"`, `"3000.00"`.

---

### Financial Accounts

#### `POST /accounts`

Creates a financial account (checking, savings, credit card, cash).

**Request:**
```json
{
  "name": "Chase Checking",
  "type": "checking",
  "institution": "Chase Bank",
  "openingBalance": "1500.00"
}
```

`type` values: `checking`, `savings`, `cash`, `credit_card`, `loan`

**Response `201`:**
```json
{
  "id": "01JXXX...",
  "name": "Chase Checking",
  "type": "checking",
  "institution": "Chase Bank",
  "balance": "1500.00",
  "createdAt": "2026-04-14T10:00:00Z"
}
```

If `openingBalance` is non-zero, a transfer is created from a system "Opening Balances" equity account to establish the starting balance.

---

#### `GET /accounts`

**Response `200`:**
```json
{
  "accounts": [
    {
      "id": "01JXXX...",
      "name": "Chase Checking",
      "type": "checking",
      "balance": "1414.53",
      "institution": "Chase Bank"
    },
    {
      "id": "01JYYY...",
      "name": "Visa Infinite",
      "type": "credit_card",
      "balance": "320.00",
      "institution": "TD Bank"
    }
  ]
}
```

`balance` on a liability is the amount owed (positive number).

---

#### `GET /accounts/{id}/transactions`

Returns the transfer history for this account, newest first.

**Query params:** `limit` (default 50), `before` (cursor timestamp)

**Response `200`:**
```json
{
  "transactions": [
    {
      "id": "01JZZZ...",
      "date": "2026-04-12",
      "description": "Whole Foods",
      "amount": "-85.47",
      "runningBalance": "1414.53"
    }
  ],
  "cursor": "01JZZZ..."
}
```

Amounts are signed: negative = money left this account, positive = money arrived.

---

### Transactions

#### `POST /transactions/income`

Records income arriving into a financial account.

**Request:**
```json
{
  "date": "2026-04-15",
  "description": "April paycheck",
  "amount": "3000.00",
  "destinationAccountId": "01JXXX...",
  "categoryCode": 3001
}
```

**TigerBeetle transfers created:**
```
T1: DEBIT destinationAccount, CREDIT Income[categoryCode], amount=300000
```

**Response `201`:**
```json
{
  "transactionId": "01JAAA...",
  "date": "2026-04-15",
  "description": "April paycheck",
  "amount": "3000.00"
}
```

---

#### `POST /transactions/expense`

Records a direct expense (debit card, cash).

**Request:**
```json
{
  "date": "2026-04-14",
  "description": "Whole Foods",
  "merchant": "Whole Foods Market",
  "amount": "85.47",
  "sourceAccountId": "01JXXX...",
  "categoryCode": 4002,
  "envelopeId": "01JENV..."
}
```

`envelopeId` is optional. When provided, the envelope budget is checked atomically.

**TigerBeetle transfers created (with envelope):**
```
T1 (linked): DEBIT Envelope, CREDIT BudgetSource, amount=8547
T2:          DEBIT Expense[categoryCode], CREDIT sourceAccount, amount=8547
```

**TigerBeetle transfers created (without envelope):**
```
T1: DEBIT Expense[categoryCode], CREDIT sourceAccount, amount=8547
```

**Response `201`:**
```json
{
  "transactionId": "01JAAA...",
  "date": "2026-04-14",
  "description": "Whole Foods",
  "amount": "85.47"
}
```

**Response `422` (envelope exhausted):**
```json
{
  "error": "envelope_exceeded",
  "message": "Groceries envelope has $12.30 remaining, cannot record $85.47"
}
```

---

#### `POST /transactions/expense/split`

Records one payment split across multiple categories.

**Request:**
```json
{
  "date": "2026-04-01",
  "description": "Monthly rent & utilities",
  "sourceAccountId": "01JXXX...",
  "lines": [
    { "categoryCode": 4001, "amount": "1700.00", "description": "Rent" },
    { "categoryCode": 4006, "amount": "300.00",  "description": "Utilities" }
  ]
}
```

**TigerBeetle transfers created (all linked):**
```
T1 (linked): DEBIT Expense[4001], CREDIT sourceAccount, amount=170000
T2:          DEBIT Expense[4006], CREDIT sourceAccount, amount=30000
```

**Response `201`:**
```json
{
  "transactionId": "01JAAA...",
  "totalAmount": "2000.00",
  "lines": [
    { "categoryCode": 4001, "amount": "1700.00", "description": "Rent" },
    { "categoryCode": 4006, "amount": "300.00",  "description": "Utilities" }
  ]
}
```

---

#### `POST /transactions/credit-card`

Records a credit card purchase. No asset account is touched.

**Request:**
```json
{
  "date": "2026-04-10",
  "description": "Dinner at Luca",
  "merchant": "Ristorante Luca",
  "amount": "45.00",
  "creditCardAccountId": "01JYYY...",
  "categoryCode": 4003
}
```

**TigerBeetle transfers created:**
```
T1: DEBIT Expense[4003], CREDIT CreditCardAccount, amount=4500
```

**Response `201`:** same shape as expense.

---

#### `POST /transactions/credit-card-payment`

Pays down a credit card balance from a bank account.

**Request:**
```json
{
  "date": "2026-04-20",
  "description": "Visa payment",
  "amount": "320.00",
  "sourceAccountId": "01JXXX...",
  "creditCardAccountId": "01JYYY..."
}
```

**TigerBeetle transfers created:**
```
T1: DEBIT CreditCardAccount, CREDIT sourceAccount, amount=32000
```

**Response `201`:** standard.

---

#### `POST /transactions/transfer`

Moves money between two of your own accounts (no income or expense recorded).

**Request:**
```json
{
  "date": "2026-04-14",
  "description": "Move to savings",
  "amount": "500.00",
  "sourceAccountId": "01JXXX...",
  "destinationAccountId": "01JSAV..."
}
```

**TigerBeetle transfers created:**
```
T1: DEBIT destinationAccount, CREDIT sourceAccount, amount=50000
```

---

#### `POST /transactions/{id}/correct`

Creates a correcting (reversing) transaction for a previous transaction.

**Request:**
```json
{
  "reason": "wrong category"
}
```

Creates mirror transfers for all legs of the original transaction in the opposite direction, tagged with `code = CorrectionCode`. Original transfers remain in history unchanged.

**Response `201`:**
```json
{
  "correctionTransactionId": "01JCOR...",
  "originalTransactionId": "01JAAA...",
  "reason": "wrong category"
}
```

---

### Budget Envelopes

#### `POST /budgets`

Creates a budget envelope.

**Request:**
```json
{
  "name": "Groceries",
  "categoryCode": 4002,
  "monthlyTarget": "400.00"
}
```

Creates a TigerBeetle account with `flags.debits_must_not_exceed_credits`.

**Response `201`:**
```json
{
  "id": "01JENV...",
  "name": "Groceries",
  "categoryCode": 4002,
  "monthlyTarget": "400.00",
  "allocated": "0.00",
  "spent": "0.00",
  "remaining": "0.00"
}
```

---

#### `POST /budgets/allocate`

Fills envelopes for the current period.

**Request:**
```json
{
  "allocations": [
    { "envelopeId": "01JENV...",  "amount": "400.00" },
    { "envelopeId": "01JENV2...", "amount": "200.00" }
  ]
}
```

**TigerBeetle transfers created (all linked):**
```
T1 (linked): DEBIT BudgetSource, CREDIT Envelope:Groceries,    amount=40000
T2:          DEBIT BudgetSource, CREDIT Envelope:Restaurants,  amount=20000
```

---

#### `GET /budgets`

**Response `200`:**
```json
{
  "period": "2026-04",
  "envelopes": [
    {
      "id": "01JENV...",
      "name": "Groceries",
      "monthlyTarget": "400.00",
      "allocated": "400.00",
      "spent": "85.47",
      "remaining": "314.53"
    }
  ]
}
```

`allocated` = `credits_posted`, `spent` = `debits_posted`, `remaining` = `credits_posted − debits_posted` — all read directly from TigerBeetle with no aggregation.

---

### Pending Transactions (Two-Phase)

#### `POST /transactions/pending`

Reserves funds for a known future expense.

**Request:**
```json
{
  "description": "Rent — May 1st",
  "amount": "2000.00",
  "sourceAccountId": "01JXXX...",
  "categoryCode": 4001,
  "expiresInSeconds": 259200
}
```

Creates a pending transfer with `flags.pending`. Funds are locked in `debits_pending` on the source account — they count against the available balance but are not yet posted.

**Response `201`:**
```json
{
  "pendingTransactionId": "01JPND...",
  "status": "pending",
  "expiresAt": "2026-05-01T00:00:00Z"
}
```

---

#### `POST /transactions/pending/{id}/post`

Confirms a pending transaction (bank clears the charge).

**Response `200`:** updated transaction with `status: "posted"`.

---

#### `POST /transactions/pending/{id}/void`

Cancels a pending transaction (payment bounced, subscription cancelled).

**Response `200`:** updated transaction with `status: "voided"`.

---

### Reports

#### `GET /reports/net-worth`

**Response `200`:**
```json
{
  "asOf": "2026-04-14T10:00:00Z",
  "totalAssets": "14500.00",
  "totalLiabilities": "320.00",
  "netWorth": "14180.00",
  "breakdown": {
    "assets": [
      { "name": "Chase Checking", "balance": "1414.53" },
      { "name": "Savings",        "balance": "13085.47" }
    ],
    "liabilities": [
      { "name": "Visa Infinite",  "balance": "320.00" }
    ]
  }
}
```

All balances read directly from TigerBeetle account fields — no aggregation.

---

#### `GET /reports/spending?from=2026-04-01&to=2026-04-30`

**Response `200`:**
```json
{
  "period": { "from": "2026-04-01", "to": "2026-04-30" },
  "totalSpending": "2430.47",
  "byCategory": [
    { "name": "Housing",     "code": 4001, "amount": "1700.00" },
    { "name": "Groceries",   "code": 4002, "amount": "385.47"  },
    { "name": "Restaurants", "code": 4003, "amount": "245.00"  },
    { "name": "Utilities",   "code": 4006, "amount": "100.00"  }
  ]
}
```

Uses `get_account_transfers` on each Expense account, filtered by timestamp range.

---

#### `GET /reports/income?from=2026-04-01&to=2026-04-30`

**Response `200`:**
```json
{
  "period": { "from": "2026-04-01", "to": "2026-04-30" },
  "totalIncome": "3000.00",
  "byCategory": [
    { "name": "Salary", "code": 3001, "amount": "3000.00" }
  ]
}
```

---

#### `GET /reports/net-worth-history?from=2026-01-01&to=2026-04-30`

Only works because the Net Worth account is created with `flags.history`. Returns balance snapshots at each closing entry.

**Response `200`:**
```json
{
  "history": [
    { "date": "2026-01-31", "netWorth": "11200.00" },
    { "date": "2026-02-28", "netWorth": "12450.00" },
    { "date": "2026-03-31", "netWorth": "13280.00" },
    { "date": "2026-04-14", "netWorth": "14180.00" }
  ]
}
```

Uses `get_account_balances` on the Net Worth account — a TigerBeetle-native operation.

---

### Period Close

#### `POST /periods/close`

Runs month-end closing entries. Zeros all Income and Expense accounts into Net Worth.

**Request:**
```json
{
  "period": "2026-04"
}
```

**What happens:**
1. Query all Income accounts with non-zero balances
2. Query all Expense accounts with non-zero balances
3. Build a linked chain of transfers moving each balance into Net Worth
4. Submit as one atomic batch — all succeed or all fail
5. Zero out all Envelope accounts back into BudgetSource (budget resets for next month)

**Response `200`:**
```json
{
  "period": "2026-04",
  "closedAt": "2026-04-30T23:59:59Z",
  "netIncome": "569.53",
  "transferCount": 8
}
```

---

## What This App Teaches

By building this, you will have implemented:

1. **A complete chart of accounts** — assets, liabilities, income, expenses, equity
2. **Credit card accounting** — the purchase and payment are separate events; expense is at swipe time
3. **Atomic multi-leg transactions** — split expenses via TigerBeetle `flags.linked`
4. **Database-level budget enforcement** — envelopes with `debits_must_not_exceed_credits`; no application balance check needed
5. **Two-phase transfers** — reserve-then-post for upcoming expenses
6. **Correcting transfers** — immutable history; mistakes corrected by addition, not deletion
7. **Month-end closing** — income/expense reset; net worth grows permanently
8. **Point-in-time balance history** — `flags.history` + `get_account_balances`
9. **Hybrid architecture** — TigerBeetle for financial state, PostgreSQL for metadata
