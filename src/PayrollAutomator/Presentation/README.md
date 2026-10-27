# Presentation

The web application is the primary presentation layer. It is a .NET 10
Blazor Web App using interactive server rendering. The CLI remains available
for automation and compatibility.

The CLI supports read-only import and confirmed export:

```text
dotnet run --project src/PayrollAutomator -- import <workbook.xlsx>
dotnet run --project src/PayrollAutomator -- export <input.xlsx> <output.xlsx> --confirm <reviewer>
```

It also supports one-shot automation:

```text
dotnet run --project src/PayrollAutomator -- process <workbook.xlsx> [--data-root <directory>]
dotnet run --project src/PayrollAutomator -- scan <input-directory> --once [--data-root <directory>]
```

`process` and `scan --once` create persisted reviewable runs, use source
path/content-hash idempotency, quarantine invalid scan candidates and never
export without explicit confirmation.

The Blazor web application provides workbook upload, persisted run history,
exception review, confirmation, export download and explicit run deletion.
The /healthz endpoint is available for container health checks.

The application does not provide user accounts in this release. A production
deployment should place the web endpoint behind an external identity boundary,
such as Cloudflare Access, before processing real payroll data.
