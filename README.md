# Payroll Automator

Payroll-hours automation for importing, validating, calculating and reviewing payroll hours.

## Current direction

- C# / .NET 10 single application
- Blazor Web App with interactive server rendering as the primary user interface
- Folder-separated components instead of multiple class libraries
- Docker-ready deployment with SQLite and mounted artifact storage
- Explicit review and audit trail before payroll output is finalised

The web application is the primary workflow. The CLI remains available for
read-only imports, confirmed exports and automation compatibility.

## Payroll workflow

1. Upload a payroll-hours workbook or invoke the CLI importer.
2. Validate rows and identify exceptions.
3. Convert time values into the agreed decimal-hour rules.
4. Apply normal-hours, overtime and rounding rules.
5. Review calculated rows, employee totals and diagnostics.
6. Confirm the export with an explicit reviewer name.
7. Download the new payroll workbook; the source workbook is never overwritten.
8. Reopen or explicitly delete the persisted run from the web run history.

## Development

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet run --project src/PayrollAutomator
```

The project targets .NET 10. See [the payroll calculation rules](docs/payroll-rules.md) for the currently agreed business rules and boundaries.

Workbook import currently supports the payroll-category layouts documented in [the workbook import contract](docs/workbook-import.md). The source workbook is read without being overwritten. The Phase 4 output format is documented in [the workbook export contract](docs/workbook-export.md).

To import a workbook from the command line:

```bash
dotnet run --project src/PayrollAutomator -- import path/to/payroll-hours.xlsx
```

The command reports imported row counts and row/sheet diagnostics without printing payroll values.

To review and export a calculated workbook, provide a separate output path and explicit reviewer confirmation:

```bash
dotnet run --project src/PayrollAutomator -- export input.xlsx output.xlsx --confirm "Fictional Reviewer"
```

Errors block export. Warnings are included in the review sheet and require the explicit confirmation argument.

To process one workbook into the persisted review workflow without exporting it:

```bash
dotnet run --project src/PayrollAutomator -- process path/to/payroll-hours.xlsx
```

To scan an inbox once, quarantine invalid candidates and create reviewable runs:

```bash
dotnet run --project src/PayrollAutomator -- scan path/to/inbox --once
```

Both automation commands support `--data-root <directory>`. Processing is
idempotent for the same source path and content hash, and automated processing
does not confirm or export payroll.

To run the web application locally:

```bash
dotnet run --project src/PayrollAutomator
```

The web application provides the guided upload, review, confirmation and export workflow. It stores the SQLite database and run artifacts under the configured data root.

The **Automation** page provides job counts, an explicit scan-now action,
retention dry-run/apply controls and a consistent backup action. It does not
confirm or export payroll.

Configuration uses the Payroll__ environment-variable prefix:

| Variable | Local default | Purpose |
| --- | --- | --- |
| Payroll__DataRoot | data | SQLite database, data-protection keys and run artifacts |
| Payroll__MaxUploadBytes | 26214400 | Maximum workbook upload size |
| Payroll__RetentionDays | 90 | Age used to identify runs for cleanup |
| Payroll__Automation__Enabled | false | Enables the hosted scheduled inbox worker |
| Payroll__Automation__NotificationProvider | Console | Console, Webhook or None |
| Payroll__Automation__NotificationEndpoint | empty | Required only for webhook notifications |

To build and run the Docker container:

```bash
docker build -t payroll-automator:local .
docker run -d --name payroll-automator --restart unless-stopped -p 8080:8080 -v /srv/payroll-automator:/data payroll-automator:local
```

The container listens on port 8080; persistent storage must be mounted at
/data. The /healthz endpoint can be used for a basic availability check.
See [the Docker deployment guide](docs/docker-deployment.md) and
[the web application guide](docs/web-application.md) for operational details.
Cloudflare Access and tunnel deployment values are intentionally kept outside
the repository; see [the Cloudflare deployment checklist](docs/cloudflare-deployment.md).

## Project structure

```text
src/PayrollAutomator/
|-- Application/     # Use cases and orchestration
|-- Components/      # Blazor pages, layout and routing
|-- Domain/          # Payroll rules and models
|-- Infrastructure/  # File, spreadsheet and persistence adapters
|-- Presentation/    # CLI entry-point documentation
|-- wwwroot/         # Web assets
+-- Program.cs

tests/PayrollAutomator.Tests/  # Automated domain and application tests
```

See [TODO.md](TODO.md) for the implementation sequence and current backlog.
