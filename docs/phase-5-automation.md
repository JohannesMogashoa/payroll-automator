# Phase 5 - Automation plan

Phase 5 adds repeatable, observable automation around the existing payroll
workflow. The web application remains the primary operator experience, the CLI
remains useful for scripts and one-off processing, and both call the same
application services. Automation must not bypass review, audit, source-file
protection or configured payroll rules.

## Outcomes

At the end of Phase 5, an operator should be able to:

1. Configure one or more safe input and output locations without rebuilding
   the application.
2. Run a one-shot or scheduled job that discovers new `.xlsx` workbooks,
   imports and calculates them, and records a normal web-visible run.
3. See whether each discovered file is queued, processing, awaiting review,
   exported, failed, skipped as a duplicate, or quarantined.
4. Receive a notification when processing completes, when review is required,
   and when an operational failure needs attention.
5. Back up the SQLite database and run artifacts, remove expired data according
   to a documented policy, and audit both operations.
6. Verify all of the above in GitHub Actions without using real payroll data.

The default unattended behaviour is **import, calculate, review and notify**.
It does not silently confirm warnings or export payroll. Automatic export is a
separate, opt-in policy requiring an explicit service identity, a clean review,
and an audit event that explains why no human confirmation was used.

## Automation modes

### Web application

The web UI remains the control plane:

- display automation health, last successful scan, queue depth and failures;
- show automatically created runs in the existing run history;
- provide an operator-triggered "scan now" action;
- provide an operator-triggered retention cleanup action with a dry-run result;
- show the source file identity, automation trigger, job status and audit trail;
- retain the existing reviewer confirmation before export.

The UI should not contain its own payroll calculation path. It should call
application services and receive a result that can also be produced by the CLI
or a hosted background service.

### CLI

Extend the existing CLI with scriptable, non-interactive commands:

```text
payroll-automator import <input.xlsx>
payroll-automator export <input.xlsx> <output.xlsx> --confirm <reviewer>
payroll-automator process <input.xlsx> --output-dir <directory>
payroll-automator scan <input-dir> --output-dir <directory> [--once]
payroll-automator cleanup [--data-root <directory>] [--dry-run]
payroll-automator backup <backup-file> [--data-root <directory>]
payroll-automator status [--json]
```

CLI requirements:

- return non-zero exit codes for invalid input, processing errors, blocked
  review, failed notifications and incomplete backup operations;
- support `--json` for scheduler and monitoring integrations;
- never print employee names, hours, rates or pay amounts by default;
- use stable error and result codes rather than parsing human messages;
- never overwrite a source file or an existing output file unless an explicit
  safe replacement policy is later approved;
- use the same `PayrollRunService` and automation services as the web app.

The original `import` command remains read-only. The existing confirmed
`export` command remains compatible and continues to require `--confirm`.

### Watched folder and scheduled processing

Implement scheduled scanning before a continuously watching process. A scan is
easier to operate in Docker, easier to retry, and works with Windows Task
Scheduler, cron, systemd timers, Kubernetes CronJobs and GitHub Actions.

The first implementation should support:

- periodic scan of a configured inbox;
- atomic claim of a candidate file before processing;
- stability detection (file size and last-write time unchanged for a configured
  interval) to avoid reading a file while it is being copied;
- extension and size checks before opening the workbook;
- deterministic duplicate detection using a SHA-256 content hash plus source
  identity metadata;
- move or copy successful inputs to configured `processed` storage;
- move invalid or failed inputs to a configured `quarantine` location;
- leave the original source untouched when configured for copy mode;
- bounded retries with backoff and a final failed state;
- a per-scan summary suitable for logs, UI and notifications.

A file watcher may be added later as a low-latency trigger, but it must enqueue
the same scan job and must not process directly from a filesystem event.

## Proposed application boundaries

Keep the domain rules unchanged. Add the following application concepts around
the current run service:

- `AutomationOptions`: enabled flag, scan interval, stability delay, maximum
  concurrency, retry count, automatic-export policy and notification policy;
- `AutomationPathOptions`: inbox, processing, processed, quarantine, output,
  backup and temporary paths, all resolved beneath an approved root unless an
  operator explicitly configures otherwise;
- `AutomationJobRecord`: job ID, source path, source hash, trigger, status,
  attempt count, timestamps, linked payroll run ID and safe failure code;
- `IAutomationJobRepository`: durable job claiming, idempotency and status
  transitions;
- `IWorkbookDiscovery`: candidate enumeration and stability checks;
- `IPayrollAutomationProcessor`: discover, claim, process, route and report;
- `INotificationSender`: email/webhook/console implementations behind one
  interface;
- `IRetentionService`: dry-run and apply modes for runs, artifacts, jobs and
  audit records;
- `IBackupService`: consistent database backup plus artifact manifest and
  checksum verification.

The existing `IPayrollRunRepository` should gain only the queries needed for
automation and retention, or be complemented by focused interfaces. Avoid
putting folder scanning, notification delivery or backup code in the domain.

## Job lifecycle and idempotency

Use an explicit state machine:

```text
Discovered -> WaitingForStableFile -> Queued -> Processing
    -> NeedsReview -> Exported
    -> SkippedDuplicate
    -> Quarantined
    -> Failed (retryable or terminal)
```

Required invariants:

- a source hash and configured source identity make a job idempotent;
- only one worker can claim a job at a time;
- a crashed worker can be reclaimed after a lease timeout;
- a payroll run is linked to the job before completion is reported;
- a failed notification never changes a successful payroll result to failed;
- a notification retry is itself idempotent or has a recorded delivery key;
- all state changes have UTC timestamps and audit events;
- no job can export a run with calculation errors;
- automatic export, if ever enabled, cannot export warnings unless a separate
  policy explicitly allows and records their acknowledgement.

Use a database-backed queue first. A hosted `BackgroundService` may poll it in
the web process, but the process must tolerate restarts and multiple replicas.
If multiple replicas become a requirement, use database leases or move the
worker to a separate process without changing the application interfaces.

## Configuration contract

Use the existing `Payroll__` configuration prefix. Proposed settings:

```text
Payroll__Automation__Enabled=false
Payroll__Automation__Mode=ScheduledScan
Payroll__Automation__ScanInterval=00:05:00
Payroll__Automation__FileStabilityDelay=00:00:30
Payroll__Automation__MaxConcurrentJobs=1
Payroll__Automation__MaxAttempts=3
Payroll__Automation__AutoExportEnabled=false
Payroll__Automation__AutoExportRequireNoWarnings=true
Payroll__Automation__ServiceReviewer=
Payroll__Automation__NotifyOn=Completed,NeedsReview,Failed
Payroll__Automation__NotificationProvider=Console
Payroll__Automation__NotificationEndpoint=
Payroll__Paths__Root=data/automation
Payroll__Paths__Inbox=inbox
Payroll__Paths__Processing=processing
Payroll__Paths__Processed=processed
Payroll__Paths__Quarantine=quarantine
Payroll__Paths__Output=output
Payroll__Paths__Backup=backups
Payroll__RetentionDays=90
Payroll__Retention__SweepAuditAfterDays=365
Payroll__Backup__Enabled=false
Payroll__Backup__MinimumFreeSpaceBytes=1073741824
```

Configuration rules:

- validate settings at startup and fail clearly on unsafe or contradictory
  values;
- do not place credentials, webhook secrets or access tokens in JSON files;
- support environment variables and a secret store in deployment docs;
- create required directories with restrictive permissions where supported;
- make paths, intervals, limits and policies visible on the status page;
- keep automation disabled by default until an operator enables it.

## Notifications

Start with a console/log notification provider for local and CI verification,
then add a generic webhook provider. Email can follow once the deployment
environment and credential handling are confirmed.

Notification payloads must contain only operational information:

- job ID and payroll run ID;
- original filename or a safe identifier;
- status, error/warning counts and next action;
- timestamps, attempt number and a link to the protected web run when known.

Do not include employee names, bank details, workbook contents, rates or total
pay amounts in notifications. Record delivery attempts, response status,
redacted endpoint identity and a correlation ID in the audit log.

## Backup and retention

Backups must be explicit, testable and separate from retention cleanup.

Backup requirements:

- create a consistent SQLite backup using SQLite's backup mechanism or an
  application-safe equivalent, not a live byte copy during writes;
- include source/export artifacts and a manifest containing relative path,
  size, SHA-256 and creation time;
- write to a temporary backup location and atomically promote only after
  checksum and completeness verification;
- never back up secrets or deployment credentials;
- expose last-successful-backup, last-failure and verification status;
- document restore steps and test a restore into a disposable data root.

Retention requirements:

- define age separately for active runs, deleted runs, job records, audit
  records, backups and quarantined files;
- never delete a run that is processing or under review;
- preserve a minimal tombstone/audit record when payroll artifacts are removed;
- provide dry-run output before deletion;
- require an explicit CLI or web operator action initially;
- make automatic cleanup opt-in and auditable only after the explicit flow is
  proven;
- ensure cleanup is idempotent and safe to retry after interruption.

## GitHub Actions

Add a workflow that runs on pull requests and pushes to the default branch:

1. checkout with a pinned or current supported action version;
2. install the .NET 10 SDK using a reproducible version policy;
3. restore dependencies;
4. build Release with warnings treated as errors;
5. run all tests in Release;
6. run format/style verification if introduced;
7. build the Docker image;
8. run a container smoke test against `/healthz` and stop the container;
9. publish test results and logs on failure;
10. optionally scan the image and dependencies using an approved tool.

The workflow must not require payroll secrets. Use synthetic fixtures only.
Image publishing and deployment should be a separate workflow with protected
environments, least-privilege credentials and manual approval until the
deployment process is established.

## Delivery sequence

### 5.1 Contracts and safety foundations

- Add options, status enums, stable result/error codes and audit event names.
- Define path validation, hash identity and source-file handling.
- Add unit tests for state transitions, duplicate detection, stability checks,
  retry/lease expiry and configuration validation.

### 5.2 One-shot automation

- Add `process` and `scan --once`.
- Reuse `PayrollRunService.CreateRunAsync`.
- Persist an automation job linked to the created run.
- Route successes, duplicates and failures to the correct folders.
- Add JSON output and safe logging.

### 5.3 Web operations

- Add status, job history, scan-now and cleanup dry-run pages/actions.
- Add audit entries for operator-triggered actions.
- Add component tests for enabled/disabled states, failure display and action
  confirmation.

### 5.4 Scheduled worker

- Add the hosted worker with leases, bounded concurrency, retries and graceful
  shutdown.
- Add deployment examples for Docker Compose/systemd/Task Scheduler as
  appropriate; keep the single-container deployment valid for the first
  version.
- Ensure a restart cannot duplicate a run or lose a claimed job.

### 5.5 Notifications

- Add console provider and notification event tests.
- Add webhook provider with timeout, retry, redaction and delivery audit.
- Add notification configuration and operational documentation.

### 5.6 Backup and retention

- Add dry-run cleanup, apply cleanup and audit events.
- Add consistent backup, manifest, checksum verification and restore test.
- Add backup/retention status to the web UI and CLI JSON output.

### 5.7 CI/CD verification

- Add GitHub Actions restore/build/test/Docker workflow.
- Add synthetic end-to-end automation fixtures.
- Add container smoke and restart persistence checks.
- Document required repository settings, branch protection and deployment
  secrets without committing their values.

## Implementation branching strategy

Implement Phase 5 as a sequence of small pull requests. Each branch should
leave the application buildable and its tests passing. Do not create one large
long-lived Phase 5 branch: the automation work crosses persistence, CLI, web,
background processing and operations, so smaller branches make safety review
and rollback practical.

### Branch rules

- `main` is always releasable and receives changes through pull requests.
- Create short-lived branches from the current `main` using the pattern
  `phase5/<slice>-<short-description>`.
- Keep one meaningful concern per branch and one pull request per branch.
- Do not mix payroll-rule changes, unrelated UI redesign or dependency cleanup
  into a Phase 5 branch.
- Add or update tests in the same branch as the behaviour they protect.
- Rebase or merge the current `main` before final review if the branch is
  behind, then rerun the complete relevant verification.
- Squash merge only when the commit history does not contain useful review
  checkpoints; otherwise preserve small descriptive commits.
- Delete merged branches and tag a release or milestone after a complete
  Phase 5 slice is accepted.

### Branch sequence

The recommended dependency order is:

| Branch | Scope | Must merge before |
| --- | --- | --- |
| `phase5/contracts-options` | Options, enums, stable result codes, audit event names, path validation and configuration tests | All automation branches |
| `phase5/job-persistence` | Automation job record, repository, state transitions, leases and idempotency constraints | Processing and worker branches |
| `phase5/one-shot-processing` | `process`, `scan --once`, discovery, stability checks, hashing, quarantine and safe logging | Web controls and scheduled worker |
| `phase5/web-automation-status` | Status page, job history, scan-now action and cleanup dry-run UI contracts | Operator-facing release |
| `phase5/cli-operations` | `cleanup`, `backup`, `status --json` and stable exit-code contract | Operations release |
| `phase5/scheduled-worker` | Hosted worker, retries, leases, bounded concurrency and graceful shutdown | Continuous automation release |
| `phase5/notifications` | Notification abstraction, console provider, webhook provider and redaction tests | Notification-enabled release |
| `phase5/backup-retention` | Consistent backup, manifests, checksums, restore verification and retention apply mode | Data-operations release |
| `phase5/blazor-component-tests` | Component coverage for upload, review, confirmation, deletion and automation controls | UI confidence gate |
| `phase5/github-actions` | Restore/build/test/Docker/health workflow and CI documentation | Phase 5 completion |

The first two branches are foundational and should merge in order. After
`phase5/one-shot-processing` lands, the web, CLI, worker and notification
branches can proceed in parallel where they do not modify the same files. The
backup/retention branch should follow the final repository and audit shape, so
it does not need to be repeatedly rewritten as job states evolve.

### Pull request gates

Every Phase 5 pull request must include:

- a short problem statement and the relevant Phase 5 subsection;
- the branch's acceptance criteria and explicit non-goals;
- tests for success, failure, retry and privacy-sensitive paths where relevant;
- confirmation that source workbooks are not overwritten;
- confirmation that logs and notifications do not expose payroll values;
- `dotnet restore`, Release build and focused tests, with full tests before
  merge;
- documentation or configuration-example updates when operator behaviour
  changes;
- a note describing migration, restart and rollback behaviour if persistence
  or job state changes.

The reviewer should reject a branch if it introduces an implicit auto-export,
unbounded retry loop, non-idempotent scan, destructive cleanup without dry-run,
or a new credential committed to the repository.

### Integration and rollback

Use `main` as the integration branch. Avoid a separate integration branch
unless several dependent pull requests must be tested together temporarily.
For that case, use a clearly named short-lived branch such as
`phase5/integration-automation`, merge the component branches into it, run the
full verification suite, and delete it after the component pull requests are
merged.

Each release slice must have a rollback story:

- contracts and schema changes must be backward-compatible with existing runs;
- a disabled automation flag must stop new work without hiding existing runs;
- worker failures must leave claimed jobs recoverable after lease expiry;
- notification or backup failures must not corrupt payroll-run state;
- retention must not run automatically during the first deployment;
- auto-export must remain disabled until its policy and service identity are
  separately approved.

### Suggested issue and PR grouping

Create one issue for each branch above and link the pull request to it. Use
action-oriented titles such as `Add durable automation job leases` and include
acceptance criteria copied from this plan. Track the branch dependencies in the
project board so implementation does not begin on a web or worker branch
before its persistence contract is merged.

## Test plan

Add tests before each implementation slice. At minimum:

- a stable workbook is discovered once;
- a partially copied workbook is deferred;
- unsupported, oversized and corrupt files are quarantined with safe codes;
- identical content is not processed twice;
- different files with the same name are not incorrectly deduplicated;
- a job lease can be reclaimed after worker failure;
- retries stop at the configured maximum;
- a created run is visible in the existing web run history;
- errors block export and warnings require the existing confirmation policy;
- automatic export is disabled by default and cannot bypass policy validation;
- notification failures are retried and audited without corrupting run status;
- cleanup dry-run identifies exactly what apply mode would remove;
- cleanup preserves active/review runs and required tombstones;
- backup manifests and checksums verify successfully;
- restore recreates the database and artifacts in a clean data root;
- CLI exit codes and JSON output are stable;
- all tests use fictional data and temporary directories.

## Acceptance criteria

Phase 5 is complete when:

- the web app, CLI and scheduled worker share the same processing service;
- a configured inbox can be scanned repeatedly without duplicate payroll runs;
- every automated result is visible, linked to a job, and auditable;
- the default automation path never exports without the existing confirmation;
- operators can identify and recover from failures without inspecting raw
  payroll values in logs;
- notifications are redacted, retried and observable;
- backups have a documented and tested restore procedure;
- retention has dry-run, explicit apply and audit coverage;
- GitHub Actions passes restore, build, tests and Docker smoke verification;
- deployment documentation covers configuration, secrets, storage, backups,
  permissions, restart behaviour and failure recovery;
- no real payroll data, credentials or secrets are committed.

## Decisions still requiring confirmation

These should remain explicit before implementation:

- inbox processing mode: move, copy, or leave-in-place after success;
- scheduler host: web process, separate worker process, or external scheduler;
- notification destination and provider;
- backup destination, encryption and retention period;
- whether automatic export is allowed at all, and who may authorize it;
- identity source for automated jobs when the web app has no user accounts;
- whether audit records are retained longer than payroll artifacts;
- deployment topology if more than one application instance is required.

