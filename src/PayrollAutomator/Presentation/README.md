# Presentation

The web application is the primary presentation layer. It is a .NET 10
Blazor Web App using interactive server rendering. The CLI remains available
for automation and compatibility.

The CLI supports read-only import and confirmed export:

```text
dotnet run --project src/PayrollAutomator -- import <workbook.xlsx>
dotnet run --project src/PayrollAutomator -- export <input.xlsx> <output.xlsx> --confirm <reviewer>
```

The Blazor web application provides workbook upload, persisted run history,
exception review, confirmation, export download and explicit run deletion.
The /healthz endpoint is available for container health checks.

The application does not provide user accounts in this release. A production
deployment should place the web endpoint behind an external identity boundary,
such as Cloudflare Access, before processing real payroll data.
