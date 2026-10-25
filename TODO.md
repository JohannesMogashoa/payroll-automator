# Payroll Automator — step-by-step TODO

## Phase 0 — Foundation

- [x] Create the GitHub repository `payroll-automator`.
- [x] Establish the single-project C# architecture.
- [x] Add a baseline README and implementation sequence.
- [x] Push the initial foundation files.
- [ ] Add GitHub issue labels and project board.

## Phase 1 — Payroll rules

- [ ] Document the agreed normal-hours calculation.
- [ ] Document the agreed decimal conversion, including `25–45 minutes = 0.5`.
- [ ] Document the confirmed hourly rate of `195`.
- [ ] Define rounding, missing-value and overnight-shift behaviour.
- [ ] Add unit tests for every rule before building the workbook importer.

## Phase 2 — Workbook import

- [ ] Choose the spreadsheet library and record the decision.
- [ ] Read the source workbook without changing the original.
- [ ] Map employee, date, start time, end time and normal-hours columns.
- [ ] Report invalid rows with row numbers and actionable messages.
- [ ] Add a sample workbook fixture with non-sensitive data.

## Phase 3 — Calculation engine

- [ ] Convert source time values into a consistent internal representation.
- [ ] Calculate ordinary hours, overtime and payable hours.
- [ ] Apply the agreed hourly rate.
- [ ] Produce per-row and per-employee totals.
- [ ] Add reconciliation checks against imported totals.

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

- [ ] Are normal hours a fixed daily value or derived from scheduled hours?
- [ ] How should minutes other than the agreed half-hour rule be rounded?
- [ ] How are breaks represented and deducted?
- [ ] How should public holidays, weekends and night work be treated?
- [ ] Is `195` the hourly rate for every employee and every payroll period?
- [ ] What exact workbook layout must the exported file follow?
