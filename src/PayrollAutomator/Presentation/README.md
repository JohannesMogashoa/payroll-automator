# Presentation

Command-line or web/API entry points will live here. The first MVP will use a simple command-line flow.

The current CLI supports read-only import and confirmed export:

```text
dotnet run --project src/PayrollAutomator -- import <workbook.xlsx>
dotnet run --project src/PayrollAutomator -- export <input.xlsx> <output.xlsx> --confirm <reviewer>
```
