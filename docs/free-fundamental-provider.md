# BUILD 03.3 — Free Fundamental Data Provider

## Why CSV is the free provider

Dividend Guardian only tracks a small watchlist. A local CSV provider gives us a zero-API-cost ingestion path while keeping the quant engine independent from any vendor.

The templates are:

- `database/seed/fundamentals/fundamentals.csv`
- `database/seed/fundamentals/dividends.csv`

Populate them from a permitted source such as company financial reports, IDX disclosures, or another data source whose terms allow the intended use.

## Configuration

Set:

```text
FUNDAMENTALS_PROVIDER=csv
FUNDAMENTALS_CSV_DIRECTORY=database/seed/fundamentals
FUNDAMENTALS_CSV_FILE=fundamentals.csv
DIVIDENDS_CSV_FILE=dividends.csv
```

The directory may also be an absolute path.

## Fundamental CSV

Required columns:

```text
ticker
period_end          # yyyy-MM-dd
revenue
net_income
eps
free_cash_flow
equity
debt
cash
shares_outstanding
```

Values use invariant decimal notation, for example `1234.56`.

## Dividend CSV

Required columns:

```text
ticker
fiscal_year
dps
payment_date       # optional, yyyy-MM-dd
payout_ratio       # optional; Dividend Guardian can calculate its own ratio
```

## Safety behavior

The provider:

- filters by ticker and requested date range;
- rejects missing required fields;
- rejects malformed numbers and dates;
- supports quoted CSV fields;
- deduplicates the same ticker/year-period by taking the last row;
- does not silently turn missing fundamental values into zero.

## Source policy

StockAnalysis is useful for human research, and its IDX financial pages expose multi-year financials, cash flow, dividends and valuation data. However, StockAnalysis states that it does not offer programmatic API access and that it licenses data from third-party providers. Therefore this build does **not** scrape or reverse-engineer StockAnalysis.

Use StockAnalysis as a manual reference only unless its terms or an authorized export/API later permits automated use.

## Next step

Once the CSV path is validated, we can add an authorized automated provider without changing the quant engine. The next useful build is persistence + analysis orchestration so the normalized data produces `quant_scores`, valuations and fair-value inputs.
