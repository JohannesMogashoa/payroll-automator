# Payroll Automator — step-by-step TODO

## Phase 0 — Foundation

- [x] Create the GitHub repository `payroll-automator`.
- [x] Establish the single-project C# architecture.
- [x] Add a baseline README and implementation sequence.
- [x] Push the initial foundation files.
- [ ] Add GitHub issue labels and project board.

## Phase 1 — Payroll rules

- [x] Document the agreed normal-hours calculation.
- [x] Document the agreed decimal conversion, including `25–45 minutes = 0.5`.
- [x] Document the confirmed hourly rate of `195`.
- [x] Define rounding, missing-value and overnight-shift behaviour.
- [x] Add unit tests for every rule before building the workbook importer.

## Phase 2 — Workbook import

- [x] Choose ClosedXML `0.105.1` and record the decision.
- [x] Read recognized source sheets without changing the original.
- [x] Map weekly and consolidated payroll-category columns from the reference workbook.
- [x] Report invalid rows with sheet, row, cell and actionable messages.
- [x] Add synthetic workbook fixtures with non-sensitive data.

The reference workbook does not contain start/end-time columns. Phase 2 preserves raw category values and number formats; category unit conversion and payroll calculations remain in Phase 3.

## Phase 3 — Calculation engine

- [x] Convert source time values into a consistent internal representation.
- [x] Calculate ordinary hours, category hours and payable hours.
- [x] Apply the configured hourly rate and explicit category multipliers.
- [x] Produce per-row and per-employee totals.
- [x] Add internal category-sum reconciliation checks; external imported-total reconciliation remains unavailable because the source workbook has no authoritative payable-total column.

## Phase 4 — Review and export

- [ ] Add a review summary for exceptions.
- [ ] Require explicit confirmation before final export.
- [ ] Export a clean payroll workbook.
- [ ] Preserve calculation metadata and rule versions.
- [ ] Add an audit log.

## Phase 5 — Automation

- [ ] Add watched-folder or scheduled processing.
- [ ] Add configurable input/output locations.
- [ ] Add notifications for completed runs and exceptions.
- [ ] Add backup and retention rules.
- [ ] Add GitHub Actions build and test workflow.

## Open questions to confirm with the client

- [x] Normal hours are supplied and treated as authoritative.
- [x] Minute remainders use `0–24 = 0`, `25–45 = 0.5`, and `46–59 = 1`.
- [x] Breaks and overnight shifts must already be reflected in supplied normal hours.
- [x] Public holidays and weekends currently use the configured rate without a premium.
- [x] `195` is the default hourly rate and callers may supply another positive rate.
- [ ] What exact workbook layout must the exported file follow?
