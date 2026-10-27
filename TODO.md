# Payroll Automator - step-by-step TODO

## Phase 0 - Foundation

- [x] Create the GitHub repository payroll-automator.
- [x] Establish the single-project C# architecture.
- [x] Add a baseline README and implementation sequence.
- [x] Push the initial foundation files.
- [ ] Add GitHub issue labels and project board.

## Phase 1 - Payroll rules

- [x] Document the agreed normal-hours calculation.
- [x] Document the agreed decimal conversion, including 25-45 minutes = 0.5.
- [x] Document the confirmed hourly rate of 195.
- [x] Define rounding, missing-value and overnight-shift behaviour.
- [x] Add unit tests for every rule before building the workbook importer.

## Phase 2 - Workbook import

- [x] Choose ClosedXML 0.105.1 and record the decision.
- [x] Read recognized source sheets without changing the original.
- [x] Map weekly and consolidated payroll-category columns from the reference workbook.
- [x] Report invalid rows with sheet, row, cell and actionable messages.
- [x] Add synthetic workbook fixtures with non-sensitive data.

The reference workbook does not contain start/end-time columns. Phase 2
preserves raw category values and number formats; category unit conversion and
payroll calculations remain in Phase 3.

## Phase 3 - Calculation engine

- [x] Convert source time values into a consistent internal representation.
- [x] Calculate ordinary hours, category hours and payable hours.
- [x] Apply the configured hourly rate and explicit category multipliers.
- [x] Produce per-row and per-employee totals.
- [x] Add internal category-sum reconciliation checks; external imported-total reconciliation remains unavailable because the source workbook has no authoritative payable-total column.

## Phase 4 - Review and export

- [x] Add a review summary for exceptions.
- [x] Require explicit confirmation before final export.
- [x] Export a clean payroll workbook.
- [x] Preserve calculation metadata and rule versions.
- [x] Add an audit log.

## Phase 5 - Automation

- [ ] Follow the complete [Phase 5 automation plan](docs/phase-5-automation.md).
- [x] Add automation contracts: options, job states, stable result codes, audit events and path validation.
- [x] Add durable automation job records, source-hash identity, leases and worker-owned outcome updates.
- [ ] Add one-shot `process` and `scan --once` commands with idempotency and quarantine handling.
- [ ] Add configurable inbox, processing, processed, quarantine, output and backup locations.
- [ ] Add web-visible automation status, scan-now and cleanup dry-run operations.
- [ ] Add durable scheduled processing with leases, retries, bounded concurrency and graceful shutdown.
- [ ] Add redacted notifications for completed runs, review-required runs and failures.
- [ ] Add consistent backup, manifest/checksum verification and tested restore procedure.
- [ ] Add explicit, auditable retention cleanup with dry-run and apply modes.
- [ ] Add GitHub Actions restore, build, test, Docker build and container smoke workflow.
- [ ] Add automated tests for discovery, stability, duplicates, retries, notifications, backup and retention.
- [ ] Implement Phase 5 using the dependency-ordered branching strategy in [docs/phase-5-automation.md](docs/phase-5-automation.md).

## Phase 6 - Web application and Docker

- [x] Convert the single application to a Blazor Web App with interactive server rendering.
- [x] Add SQLite run persistence and mounted filesystem artifact storage.
- [x] Add upload, review, confirmation, export download and explicit deletion pages.
- [x] Add health checking, persisted data-protection keys and configurable storage limits.
- [x] Add a single-container Dockerfile and Cloudflare Tunnel deployment guidance.
- [ ] Add automated Blazor component tests.
- [x] Validate the Docker image with a running Docker daemon and restart persistence smoke test.

## Current next priorities

- [ ] Add automated Blazor component tests for the upload, review, confirmation and deletion flows.
- [ ] Start Phase 5 with automation contracts and one-shot processing; see [docs/phase-5-automation.md](docs/phase-5-automation.md).
- [ ] Add a retention-cleanup operation with an explicit operator trigger and audit event.
- [ ] Add a GitHub Actions workflow covering restore, build, tests, Docker image build and health smoke test.
- [ ] Document deployment-specific Cloudflare Access and tunnel values outside the repository.
- [ ] Revisit continuous watched-folder processing after scheduled scan and restart recovery are stable.

## Open questions to confirm with the client

- [x] Normal hours are supplied and treated as authoritative.
- [x] Minute remainders use 0-24 = 0, 25-45 = 0.5, and 46-59 = 1.
- [x] Breaks and overnight shifts must already be reflected in supplied normal hours.
- [x] Public holidays and weekends currently use the configured rate without a premium.
- [x] 195 is the default hourly rate and callers may supply another positive rate.
- [x] Define the initial exported workbook layout in [docs/workbook-export.md](docs/workbook-export.md).
