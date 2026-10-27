# Web application

Payroll Automator is a .NET 10 Blazor Web App using interactive server rendering. The existing payroll domain and application services are reused by the web UI and CLI.

## Workflow

1. Open the runs page and choose **Start new run**.
2. Upload an `.xlsx` payroll-hours workbook.
3. Inspect calculated rows, employee totals and every import/calculation exception.
4. Resolve blocking errors or acknowledge warnings.
5. Enter the reviewer name and confirm the export.
6. Download the clean payroll workbook.
7. Reopen or explicitly delete the persisted run from the run history.

## Automation control

Open **Automation** from the navigation to view durable automation job counts,
including queued, processing, needs-review, failed and quarantined jobs.

The page provides explicit operator actions:

- **Scan now** scans the configured automation inbox once. Stable `.xlsx`
  files become persisted reviewable runs; invalid candidates are quarantined.
- **Retention dry run** reports the number of old runs and terminal automation
  jobs that would be eligible for cleanup. It never deletes anything.
- **Apply retention cleanup** deletes only old runs that are not awaiting review;
  each deletion uses the existing run tombstone and audit entry. Needs-review
  runs and non-terminal jobs remain protected.
- **Create backup** creates a consistent SQLite backup plus copied run artifacts
  under the configured backup location. A manifest records file lengths and
  SHA-256 checksums, and the backup is verified before it is reported complete.

Scheduled automation remains disabled by default. Scan now does not confirm or
export payroll, and exports still require the existing reviewer confirmation.

Errors cannot be confirmed. Warnings can be exported only after confirmation.

## Storage

The application stores a SQLite database and run artifacts below the configured data root:

```text
data/
├── payroll.db
├── keys/
└── runs/
    └── {run-id}/
        ├── source/
        └── export/
```

The default local data root is `data`. Docker overrides it to `/data`. Payroll files and calculation snapshots are excluded from source control.

The first release uses a 25 MB upload limit and a 90-day retention age. Expired
data is not removed automatically; cleanup is an explicit operator action.

The CLI exposes the same maintenance operations:

```text
dotnet run --project src/PayrollAutomator -- cleanup --dry-run
dotnet run --project src/PayrollAutomator -- cleanup --apply
dotnet run --project src/PayrollAutomator -- backup
```

Scheduled automation is opt-in with `Payroll:Automation:Enabled=true`. It polls
the configured inbox, uses the durable SQLite job lease and retry state, and
stops cleanly on application shutdown. `NotificationProvider` supports
`Console`, `Webhook` and `None`; webhook payloads contain operational identifiers
and outcomes only.

## Access

The application does not contain user accounts in this release. When deployed through Cloudflare Tunnel, protect the payroll subdomain with Cloudflare Access. Reviewer identity is entered at the confirmation step and recorded in the run audit history.
