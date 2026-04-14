# Personal Expense Tracker — Domain Guide

This document explains what the app does, why it works the way it does, and what you will be able to accomplish with it. No accounting background required.

---

## What Will You Be Able to Do With This App?

Think of this app as your complete personal financial dashboard. Here is everything you will be able to do:

**Track where your money is**
- Add all your bank accounts (checking, savings, cash wallet)
- Add your credit cards
- See the current balance of each one at a glance
- See your total net worth (everything you own minus everything you owe) in real time

**Record what happens to your money**
- Log a paycheck arriving in your bank
- Log a grocery run paid with your debit card
- Log a dinner charged to your credit card
- Log paying off your credit card
- Split one payment across multiple categories (e.g. one rent payment that covers both rent and utilities)
- Move money between your own accounts (e.g. transfer from checking to savings)

**Keep a budget**
- Create "envelopes" — named spending buckets like Groceries ($400/month) or Restaurants ($200/month)
- Fill your envelopes at the start of each month
- When you record a grocery expense, the app checks your Groceries envelope automatically — if you've run out of budget, the transaction is rejected before it's even saved
- See how much of each envelope you've spent and how much is left

**Plan ahead**
- Mark a future payment as "pending" (e.g. rent due on the 1st) — your available balance immediately reflects the reserved amount
- When the payment clears, confirm it; if it doesn't go through, cancel it

**Fix mistakes**
- Corrected the wrong category? The app creates a reversing entry — nothing is ever deleted or edited, the full history is always there
- You can always see exactly what happened and when

**Run reports**
- How much did I spend on restaurants this month?
- What was my total income in Q1?
- What is my net worth today vs. 3 months ago?
- Which expense category is eating most of my budget?

**Close out each month**
- Run a month-end close: income and expense totals get swept into your permanent net worth, and the slate is wiped clean for next month

---

## The Core Idea: Every Money Event Touches Two Places

Most apps track your finances like a shopping list — just a list of amounts. This app is different. It uses a system called **double-entry bookkeeping**, which has been the standard way of tracking money for over 500 years.

The rule is simple:

> **Every time money moves, two things change simultaneously. What one account gains, another account loses.**

This sounds abstract, so here's a concrete example.

You receive a $3,000 paycheck into your checking account. Two things happened:
1. Your checking account got bigger (by $3,000)
2. You earned income (of $3,000)

Or: you spend $85 on groceries with your debit card. Two things happened:
1. Your checking account got smaller (by $85)
2. Your grocery expenses grew (by $85)

This might seem like extra work, but it gives you something powerful: **the numbers always add up perfectly**. You can always verify that nothing was lost, duplicated, or invented. And you get a complete picture — not just "I have $1,200 in my account" but "I have $1,200 in my account because I earned $3,000, spent $1,800, and transferred nothing."

---

## Account Types

Every financial "slot" in this app is called an **account**. There are five types. Understanding these five types is the key to understanding everything else.

---

### 1. Assets — What You Own

An asset account represents something of value that you own.

**Examples:** Checking account, savings account, cash in your wallet, investment portfolio.

**The balance** of an asset account tells you how much of that asset you currently hold.

**It grows** when money flows in (you deposit a paycheck, someone repays you).
**It shrinks** when money flows out (you pay a bill, you buy something).

**Important:** Your checking account showing $1,200 is an asset. The money is yours.

---

### 2. Liabilities — What You Owe

A liability account represents money you owe to someone else.

**Examples:** Credit card balance, student loan, mortgage, money borrowed from a friend.

**The balance** tells you how much you currently owe.

**It grows** when you use credit (you swipe your card, you take out a loan).
**It shrinks** when you pay it down (you make a credit card payment).

**Important:** Your credit card showing $320 is a liability. That money isn't yours — it's borrowed. You need to pay it back.

---

### 3. Income — Where Money Came From

An income account records the sources of money flowing into your life.

**Examples:** Salary, freelance income, rental income, interest earned, a gift.

**The balance** accumulates over the accounting period (usually a month). It tells you how much you earned from that source this month.

**It resets to zero** at month-end when you run the closing process.

---

### 4. Expenses — Where Money Went

An expense account records categories of spending.

**Examples:** Housing, Groceries, Restaurants, Transportation, Entertainment, Healthcare, Utilities.

**The balance** accumulates over the accounting period. It tells you how much you spent in that category this month.

**It resets to zero** at month-end as well.

---

### 5. Equity (Net Worth) — What You're Actually Worth

Equity is the difference between everything you own and everything you owe. In personal finance, this is simply called **net worth**.

```
Net Worth = Total Assets − Total Liabilities
```

**Example:** You have $14,500 in bank accounts (assets) and $320 on your credit card (liability). Your net worth is $14,180.

**Unlike income and expenses, net worth never resets.** It accumulates permanently over time. Every month-end close, the month's net income (income minus expenses) gets added to net worth. This is how you build wealth over time — your net worth should trend upward month after month.

---

## Two Special System Accounts

Beyond the five standard types, this app uses two internal accounts that you won't interact with directly:

**Envelope** — A budget bucket. When you create a Groceries budget of $400/month, the app creates an Envelope account for it. When you allocate $400 to it at month start, that $400 sits in the envelope. Every grocery expense draws from it. When the envelope is empty, further grocery expenses are blocked automatically — the database itself rejects them, before your application code even runs.

**BudgetSource** — A behind-the-scenes control account. When you fill envelopes, the money comes "from" BudgetSource. When expenses draw from envelopes, the absorbed budget flows back into BudgetSource. It's a bookkeeping tool that keeps everything balanced — you never set it up or touch it directly.

---

## How Money Actually Moves: The Journal Entries

Below is every operation the app supports, shown as the accounting entries that happen behind the scenes. Each entry has a **debit side** (left) and a **credit side** (right). They are always equal.

You don't need to think about debits and credits when using the app — you just pick "record grocery expense" and fill in the amount. But understanding what happens underneath is what this project is about.

---

### Receiving a Paycheck

**What you do:** Log income of $3,000 arriving in your checking account.

```
DEBIT   Checking Account   +$3,000   ← your bank balance goes up
CREDIT  Income: Salary     +$3,000   ← this month's salary tally goes up
```

---

### Paying for Groceries (Debit Card)

**What you do:** Log an $85.47 grocery purchase paid from checking.

```
DEBIT   Expense: Groceries  +$85.47  ← this month's grocery tally goes up
CREDIT  Checking Account    −$85.47  ← your bank balance goes down
```

---

### Buying Dinner on Your Credit Card

**What you do:** Log a $45 restaurant charge on your Visa.

```
DEBIT   Expense: Restaurants  +$45   ← this month's restaurant tally goes up
CREDIT  Liability: Visa        +$45   ← what you owe to Visa goes up
```

Notice: **your checking account is not touched at all.** The expense is recorded the moment you swipe, not when you eventually pay the bill. This is how it should work — you incurred the expense on the day you ate dinner.

---

### Paying Off Your Credit Card

**What you do:** Send $320 from checking to pay your Visa bill.

```
DEBIT   Liability: Visa      −$320   ← what you owe to Visa goes down
CREDIT  Checking Account     −$320   ← your bank balance goes down
```

Notice: **no expense is recorded here.** The expense was already recorded when you swiped the card. This is just moving money from your bank to your creditor — a balance sheet shuffle, not new spending.

This is one of the most important things to understand: **spending happens when you buy something, not when you pay the credit card bill.** If you track it at payment time, you're double-counting (or worse, not counting at all in the month you actually spent it).

---

### Transferring Between Your Own Accounts

**What you do:** Move $500 from checking to savings.

```
DEBIT   Savings Account    +$500   ← savings goes up
CREDIT  Checking Account   −$500   ← checking goes down
```

Your total assets are unchanged. No income, no expense — just reorganization.

---

### Splitting a Payment Across Two Categories

**What you do:** Pay $2,000 rent that covers both housing ($1,700) and utilities ($300).

```
DEBIT   Expense: Housing    +$1,700  ← (linked)
CREDIT  Checking Account    −$1,700

DEBIT   Expense: Utilities  +$300    ← both happen atomically
CREDIT  Checking Account    −$300
```

Both entries are **linked** — they either both succeed or both fail together. If something goes wrong with the second entry, the first one is automatically rolled back. You never end up in a half-recorded state.

---

### Expense With Envelope Check

**What you do:** Log an $85.47 grocery purchase, with your Groceries envelope enabled.

```
DEBIT   Envelope: Groceries  −$85.47  ← (checked first, linked)
CREDIT  BudgetSource         +$85.47

DEBIT   Expense: Groceries   +$85.47  ← (only runs if envelope check passed)
CREDIT  Checking Account     −$85.47
```

The envelope check runs first. If the Groceries envelope has less than $85.47 remaining, the whole thing is rejected at the database level before any money movement is recorded. Your checking account is untouched. This is not a warning — it's a hard stop.

---

### Marking a Future Expense as Pending

**What you do:** Mark your upcoming $2,000 rent (due May 1st) as pending today.

```
PENDING DEBIT   Checking Account    (funds reserved, not yet moved)
PENDING CREDIT  Expense: Housing    (not yet posted)
```

Your checking account shows $2,000 less "available" immediately, even though the money hasn't moved yet. When the payment clears on the 1st, you confirm it and the pending entry becomes a real posted entry. If it bounces, you void it and the reserved funds are released.

---

### Correcting a Mistake

**What you do:** You logged a dinner as "Groceries" instead of "Restaurants". You correct it.

The app does **not** edit or delete the original entry. Instead it creates a reversing entry:

```
Original (stays forever):
  DEBIT   Expense: Groceries   +$45   CREDIT Checking   −$45

Correction (new entry, tagged as correction):
  DEBIT   Checking             +$45   CREDIT Expense: Groceries   −$45   [correction]

New correct entry:
  DEBIT   Expense: Restaurants +$45   CREDIT Checking   −$45
```

The full history is preserved. An auditor can see the original mistake, the correction, and when each happened. Your reports show the net result: $45 in Restaurants, $0 net in Groceries from this incident.

---

### Month-End Close

**What you do:** Run the close for April. The app does the rest.

The app looks at every Income and Expense account. For each one with a non-zero balance, it creates a sweeping entry into Net Worth:

```
DEBIT   Income: Salary       −$3,000  CREDIT Net Worth  +$3,000
DEBIT   Net Worth            −$85.47  CREDIT Expense: Groceries  −$85.47
DEBIT   Net Worth            −$45.00  CREDIT Expense: Restaurants −$45.00
... (one entry per account)
```

All of these run as one atomic batch — either everything closes, or nothing does. After the close:
- Income: Salary = $0 (ready for May)
- Expense: Groceries = $0 (ready for May)
- Net Worth grew by your net income for the month

The envelope balances are also zeroed back to $0, ready to be re-allocated for May.

---

## The Fundamental Equation

Every single operation above — no matter how complex — preserves this equation:

```
Assets = Liabilities + Net Worth
```

Or rearranged:

```
Net Worth = Assets − Liabilities
```

This isn't just a formula. It's a guarantee. If the equation ever breaks, it means something was recorded incorrectly. In this app, TigerBeetle enforces this at the database level — an entry that would break the equation is rejected before it's ever saved.

---

## What You Cannot Do (By Design)

**Edit or delete a transaction.** Every recorded transfer is permanent. If you made a mistake, you correct it with a new reversing entry. This creates a complete, tamper-evident history.

**Overdraw an envelope.** If your Groceries envelope has $12 left and you try to log an $85 grocery run against it, the transaction is rejected. Not warned — rejected. You would need to either re-allocate more budget to the envelope or record the expense without an envelope.

**Have unbalanced books.** You cannot record a transaction that doesn't have matching debit and credit amounts. This is enforced by TigerBeetle — not just checked by the application.

---

## Summary

| Concept | Plain English |
|---|---|
| Asset | Something you own (bank account, cash) |
| Liability | Something you owe (credit card, loan) |
| Income | Where money came from this month |
| Expense | Where money went this month |
| Net Worth / Equity | Assets minus liabilities — your true financial position |
| Envelope | A named spending limit for a category |
| Double-entry | Every movement touches exactly two accounts, always balancing |
| Pending transfer | Money reserved for a known future payment |
| Correcting entry | A mistake fixed by addition, never by deletion |
| Month-end close | Sweeping income/expense totals into net worth and resetting for next month |
