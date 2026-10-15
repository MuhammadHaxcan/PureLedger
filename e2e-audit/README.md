# Real-screen audit scripts (Playwright + real Chrome + real PostgreSQL)

Drives the actual React screens against the actual backend and PostgreSQL, then checks the database.
**Run only against a disposable copy** (`CREATE DATABASE x TEMPLATE pureledgerdb_local`), never the live database.

## Setup
1. Backend on http://localhost:5221 with `ConnectionStrings__LedgerDb` pointing at the disposable DB, `ASPNETCORE_ENVIRONMENT=Development`.
2. Frontend: `cd pureledger-frontend && npx vite --port 5173` (proxies `/api` to 5221).
3. A throwaway admin in that DB: username `audit_admin`, password `AuditPass#2026`
   (insert into `"Users"` with a bcrypt hash; `bcryptjs.hashSync('AuditPass#2026', 10)`).
4. `cd e2e-audit && npm install`. Uses installed Google Chrome (`channel: 'chrome'`).
5. Data used: project GHAZI TRADERS, customer BURHAN TRADER, products `18 BISCO L2PC 60PKT` /
   `12 SMART FOOD ROUND 4PC SET 75PC`, location FACTORY SITE (rename in the scripts for other data).

## Scenarios (run in this order; `state.json` / `state2.json` carry the voucher id)
| Script | What it does through the UI |
|---|---|
| `stepA_create.mjs` | Entry screen: create a product-sale voucher (writes `state.json`) |
| `stepB_processDC.mjs "Post All"` | Process Delivery Challan screen: process it (location + station) |
| `stepC_editQty.mjs 8` | Voucher Entry & Summary: edit the processed product's quantity (the reported bug path) |
| `stepB_processDC.mjs "Post All"` | Reprocess the DC; the row must land on the challan again |
| `stepD_docs.mjs <tag>` | Single Document report: open Invoice and Delivery Challan and print their text |
| `stepC2_editRateLoc.mjs` | Edit rate + location with quantity unchanged: stays processed, location updated |
| `stepA2_create3rows.mjs` | Create customer + two products (writes `state2.json`) |
| `stepE_deleteRow.mjs 0` (`STATE_FILE=state2.json`) | Delete the first product row; it and its challan row must go, the rest stay |

`PG_DB=<db> PGPASSWORD=... ./dbstate.sh <voucherId>` shows the database state after each step.
