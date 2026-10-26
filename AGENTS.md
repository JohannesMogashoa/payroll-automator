# AGENTS.md

## Project overview

Payroll Automator is a payroll-hours processing application for importing, validating, calculating, reviewing and exporting payroll information.

The project is intentionally designed as a simple C#/.NET application. Keep the solution easy to understand and deploy. Prefer folders within one application over multiple class-library projects unless there is a clear, documented reason to split the solution.

## Technology direction

- Primary language: C#
- Runtime: .NET 10
- Architecture: single application with separated folders
- Initial interface: command line
- Primary input/output: payroll-hours workbooks
- Source control: GitHub
- Tests: automated unit tests for payroll rules and calculations

Expected folders:

    src/PayrollAutomator/
    ├── Application/       # Use cases and orchestration
    ├── Domain/            # Payroll models and business rules
    ├── Infrastructure/    # Spreadsheet, file-system and external adapters
    ├── Presentation/      # CLI/API entry points
    └── Program.cs

## Important payroll context

The current confirmed assumptions are:

- Default hourly rate: R195
- For the agreed minute conversion rule, 25–45 minutes becomes 0.5 payroll hours.
- The source workbook must not be overwritten.
- Every calculation should be reviewable and auditable.
- Sensitive or real payroll data must not be committed to the repository.

These are working assumptions, not permission to invent unresolved payroll rules. If a rule is unclear, record it as an explicit open question and avoid silently choosing behaviour.

## Business-rule guidelines

- Keep payroll rules in Domain/.
- Keep spreadsheet-specific code out of the domain.
- Use explicit types and descriptive names.
- Avoid hidden rounding or implicit coercion.
- Make rounding behaviour visible in code and tests.
- Preserve source row numbers when importing workbook data.
- Treat invalid or ambiguous rows as exceptions.
- Never silently discard invalid data.
- Use decimal arithmetic for currency and payable amounts.
- Add tests before changing an established payroll rule.

## Data and privacy

- Never commit real employee names, bank details, salaries, payslips or payroll workbooks.
- Use synthetic fixtures with clearly fictional data.
- Do not log sensitive payroll values unnecessarily.
- Do not overwrite source files during processing.
- Export files to a separate output location.
- Keep audit information sufficient to explain a result without exposing unnecessary personal data.
- Review .gitignore before adding any new payroll-related file type.

## Implementation workflow

For every task:

1. Read README.md and TODO.md.
2. Inspect the relevant existing code before editing.
3. Identify the business rule or user-visible behaviour being changed.
4. Update or create tests alongside the implementation.
5. Keep changes focused and small.
6. Update documentation when behaviour, assumptions or commands change.
7. Run formatting, build and tests where the environment supports them.
8. Review the diff for accidental sensitive data.
9. Report what changed, what was verified and any remaining limitation.

## Issue and branch workflow

- Use one focused branch or pull request per meaningful change.
- Use issue-driven work and reference the issue in the pull request.
- Keep issue titles action-oriented.
- Include acceptance criteria in issues.
- Do not close an issue until its acceptance criteria are met.
- Prefer pull requests for changes once the project has more than one contributor.
- Keep commits small and descriptive.

## Testing expectations

At minimum, payroll rule tests should cover:

- 0 minutes
- Values below 25 minutes
- 25 minutes
- Values between 25 and 45 minutes
- 45 minutes
- Values above 45 minutes
- 59 minutes
- Invalid negative minutes
- Invalid values of 60 minutes or more
- Currency calculations using the R195 rate
- Rounding and boundary behaviour once confirmed

Tests must not depend on real payroll files or external services.

## Build and verification commands

From the repository root:

    dotnet restore
    dotnet build --configuration Release
    dotnet test --configuration Release
    dotnet run --project src/PayrollAutomator

If a command cannot be run because the required SDK or dependency is unavailable, state that clearly rather than claiming verification succeeded.

## Current implementation sequence

Follow TODO.md in order:

1. Formalise and test payroll rules.
2. Choose and implement workbook import.
3. Build the calculation engine.
4. Add review and export.
5. Add automation and CI improvements.

Do not jump to AI features, scheduling or commercial features before the deterministic payroll workflow is reliable and tested.

## Definition of done

A change is complete when:

- The requested behaviour is implemented.
- Relevant tests are added or updated.
- No real payroll data is included.
- Documentation and TODO tracking are current.
- Build and tests pass, or limitations are explicitly reported.
- The change is small enough to review confidently.
