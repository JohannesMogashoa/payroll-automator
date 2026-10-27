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

The first release uses a 25 MB upload limit and a 90-day retention age. Expired data is not removed automatically; deletion is an explicit operator action.

## Access

The application does not contain user accounts in this release. When deployed through Cloudflare Tunnel, protect the payroll subdomain with Cloudflare Access. Reviewer identity is entered at the confirmation step and recorded in the run audit history.
