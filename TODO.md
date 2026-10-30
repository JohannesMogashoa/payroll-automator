# Payroll Automator - step-by-step TODO

## Phase 0 - Foundation

- [x] Create the GitHub repository payroll-automator.
- [x] Establish the single-project C# architecture.
- [x] Add a baseline README and implementation sequence.
- [x] Push the initial foundation files.
- [x] Add GitHub issue labels.

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

- [x] Follow the complete [Phase 5 automation plan](docs/phase-5-automation.md).
- [x] Add automation contracts: options, job states, stable result codes, audit events and path validation.
- [x] Add durable automation job records, source-hash identity, leases and worker-owned outcome updates.
- [x] Add one-shot `process` and `scan --once` commands with idempotency, stability checks and quarantine handling.
- [x] Add configurable inbox, processing, processed, quarantine, output and backup locations.
- [x] Add web-visible automation status, scan-now and cleanup dry-run operations.
- [x] Add durable scheduled processing with leases, retries, bounded concurrency and graceful shutdown.
- [x] Add redacted notifications for completed runs, review-required runs and failures.
- [x] Add consistent backup, manifest/checksum verification and tested restore procedure.
- [x] Add explicit, auditable retention cleanup with dry-run and apply modes.
- [x] Add GitHub Actions restore, build, test, Docker build and container smoke workflow.
- [x] Add automated tests for discovery, stability, duplicates, retries, notifications, backup and retention.
- [x] Implement Phase 5 using the dependency-ordered branching strategy in [docs/phase-5-automation.md](docs/phase-5-automation.md).

## Phase 6 - Web application and Docker

- [x] Convert the single application to a Blazor Web App with interactive server rendering.
- [x] Add SQLite run persistence and mounted filesystem artifact storage.
- [x] Add upload, review, confirmation, export download and explicit deletion pages.
- [x] Add health checking, persisted data-protection keys and configurable storage limits.
- [x] Add a single-container Dockerfile and Cloudflare Tunnel deployment guidance.
- [x] Add automated Blazor component tests.
- [x] Validate the Docker image with a running Docker daemon and restart persistence smoke test.

## Current next priorities

- [x] Add automated Blazor component tests for the upload, review, confirmation and deletion flows.
- [x] Start Phase 5 with automation contracts and one-shot processing; see [docs/phase-5-automation.md](docs/phase-5-automation.md).
- [x] Add the web automation status, scan-now and retention dry-run controls.
- [x] Add a retention-cleanup operation with an explicit operator trigger and audit event.
- [x] Add a GitHub Actions workflow covering restore, build, tests, Docker image build and health smoke test.
- [x] Document deployment-specific Cloudflare Access and tunnel values outside the repository.
- [x] Revisit continuous watched-folder processing after scheduled scan and restart recovery are stable.
- [ ] Implement the home-server deployment documented in [the deployment investigation](docs/home-server-deployment-investigation.md).
  - [ ] Complete the pre-deployment checks in the [status guide](docs/home-server-deployment/01-status-and-decisions.md): confirm `cloudflared` is healthy, reserve a stable LAN address for `bravo`, and verify the selected recovery path.
  - [ ] Create the least-privilege Tailscale policy in the [network guide](docs/home-server-deployment/02-network-access.md): tag `bravo` as `tag:payroll-server`, use `tag:ci-deploy` for ephemeral Actions nodes, restrict deployment traffic to TCP 22, and allow Tailscale SSH only as `payroll-deploy`.
  - [ ] Configure Tailscale workload identity federation for this repository and deployment workflow; grant `id-token: write` only to the deployment job.
  - [ ] Create the dedicated `payroll-deploy` server account and a root-owned deployment script with the narrowly scoped privilege needed to load an image, update Compose and perform health checks.
  - [ ] Add the server-side Compose project and persistent paths from the [container and storage guide](docs/home-server-deployment/03-container-and-storage.md); bind the app to `127.0.0.1:8080` and preserve `/data` across updates.
  - [ ] Implement daily same-disk backups with 30-day retention, manifest/checksum verification and a tested restore procedure; document the same-disk limitation.
  - [ ] Extend GitHub Actions using the [deployment guide](docs/home-server-deployment/04-github-actions-deployment.md): keep PR checks, publish a private SHA-tagged GHCR image, transfer the exact image over Tailscale because `bravo` cannot currently reach outbound HTTPS, wait for `/healthz`, and retain the last known-good image for rollback.
  - [ ] Protect the GitHub `production` environment and require manual approval; deploy only from protected `master` and never from pull requests.
  - [ ] Add the `payroll.yngstln.dev` route to the existing host-managed Cloudflare Tunnel and configure Cloudflare Access Free with OTP restricted to the two named users; verify both authorized and unauthorized access as described in the [Cloudflare guide](docs/home-server-deployment/05-cloudflare-access.md).
  - [ ] Rehearse container restart persistence, image rollback, backup restore, Cloudflare failure recovery, Tailscale failure recovery and LAN recovery with synthetic data; complete the [operations readiness gates](docs/home-server-deployment/06-operations-and-recovery.md).

## Open questions to confirm with the client

- [x] Normal hours are supplied and treated as authoritative.
- [x] Minute remainders use 0-24 = 0, 25-45 = 0.5, and 46-59 = 1.
- [x] Breaks and overnight shifts must already be reflected in supplied normal hours.
- [x] Public holidays and weekends currently use the configured rate without a premium.
- [x] 195 is the default hourly rate and callers may supply another positive rate.
- [x] Define the initial exported workbook layout in [docs/workbook-export.md](docs/workbook-export.md).
