# Payroll Automator

Payroll-hours automation for importing, validating, calculating and reviewing payroll hours.

## Current direction

- C# / .NET single application
- Folder-separated components instead of multiple class libraries
- Spreadsheet-first MVP, with room for later integrations
- Explicit review and audit trail before payroll output is finalised

## Planned workflow

1. Import a payroll-hours workbook.
2. Validate rows and identify exceptions.
3. Convert time values into the agreed decimal-hour rules.
4. Apply normal-hours, overtime and rounding rules.
5. Produce a reviewable calculation report.
6. Export the final payroll workbook.

## Development

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet run --project src/PayrollAutomator
```

The project targets .NET 10. See [the payroll calculation rules](docs/payroll-rules.md) for the currently agreed business rules and boundaries.

## Project structure

```text
src/PayrollAutomator/
├── Application/     # Use cases and orchestration
├── Domain/          # Payroll rules and models
├── Infrastructure/  # File and spreadsheet adapters
├── Presentation/    # CLI/API entry points
└── Program.cs

tests/PayrollAutomator.Tests/  # Automated domain-rule tests
```

See [TODO.md](TODO.md) for the implementation sequence.
