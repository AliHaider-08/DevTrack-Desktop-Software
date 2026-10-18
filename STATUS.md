# Devzone Business Management System - Build Status

## Verified so far (.NET 8)
**Devzone.Domain** - no external packages, TreatWarningsAsErrors on.
- RoundingPolicy: decimal money, line-level rounding, away-from-zero midpoints.
- InvoiceCalculator: line discounts, invoice discount (largest-remainder allocation, no lost cents),
  tax exclusive/inclusive, charges, outstanding balance, overpayment rejection.
- JournalEntry: double-entry, balance enforced at Post(), immutable once posted,
  reversal creates a linked mirror entry and preserves the original, idempotency key per source document.
- SalePostingBuilder: Dr Cash/Receivable, Cr Revenue/Tax, Dr COGS/Cr Inventory.
- WeightedAverageStock: stores total value (no drift), blocks negative stock unless enabled,
  returns re-enter at original cost.

Run the checks: `dotnet run --project tools/Devzone.CoreVerification` -> 38 passed, 0 failed
(includes 5,000 randomized invoices checking allocation, totals and posting balance).

## Phase 1 progress
**Devzone.Application** defines contracts for company profiles, authentication, permissions, and audit events.
**Devzone.Desktop** builds as the branded `DevTrack.exe` native WPF app, with centralized theme resources
using #ECE7D1 and #E2E8CE. Its navigation now contains Dashboard, Products, Categories, Stock Movements,
Purchases, and Customers. These screens load data from the authenticated company database.
**Devzone.Infrastructure** now defines SQLite EF Core entities and context for companies, users, and audit
events, safe LocalAppData database placement, initial database creation, audit immutability triggers, and
PBKDF2 password hashing.
Infrastructure also implements company persistence, user account persistence, password verification,
permission lookup, audit appends, and dependency registration. Desktop startup now initializes a fresh local
database, requires a first-run company and owner setup, then requires successful owner sign-in before opening
the shell. Setup writes the company, owner, permissions, and audit event in one transaction. Login attempts
are audited and the access screen slows and locks after five failures for that process session.

Catalog functionality includes company-scoped category and product creation, product search/edit/archive,
non-negative stock adjustments with reasons, movement history, low/out-of-stock counts, and inventory value
derived from saved stock movements. Customer functionality includes company-scoped customer search/create/edit,
opening balances, receipts, charges, and a customer ledger; ledger entries are append-only. Purchase receiving
records supplier/reference, product, quantity, and cost, then atomically updates on-hand stock and weighted
average cost. Purchase receipts and stock movements are audited. Product, stock, and customer changes are
audited. `dotnet build Devzone.BusinessManagement.sln --no-restore` succeeds with 0 warnings and 0 errors.
Startup keeps the application alive while the modal sign-in window closes and then opens the main shell.

## NOT yet built
Sales, supplier directory and payables, accounting, permission assignment UI, EF Core migrations,
MVVM infrastructure, reports, printing/PDF, backup/restore, cloud API and sync, installer. These features
are not represented as complete.

## Not yet verified
The WPF app was launched interactively through first-run setup and the sign-in window; DevTrack now opens
with catalog and customer screens wired to persistence. Customer workflows have been compiled but not yet
verified interactively. Login lockout is
process-local and resets when the application closes. Existing database schema upgrades are not implemented;
the current setup creates the initial schema and requires migrations before distributing future upgrades.
The verification harness has not yet been ported to xUnit.

## Next: Phase 1 remainder
EF Core migrations, persistent login throttling/session timeout, MVVM wiring, and permission assignment UI.
Then implement business modules against the now-available local persistence and authorization services.
