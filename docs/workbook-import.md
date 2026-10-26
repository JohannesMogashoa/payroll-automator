# Workbook import contract

Phase 2 imports payroll-category workbooks represented by the October 2026 reference workbook. The reference workbook is not a test fixture and must not be committed because it contains employee-level payroll data.

## Recognized layouts

The importer recognizes worksheets with row 1 containing these headers:

`Date`, `Employee Number`, `Employee Name`, `Normal`, `Overtime 1.5`, `Sunday 0.5`, `PH`, `Night Shift`, `Sick Leave`, and `Comment` or `Comments`.

The weekly layout places `Employee Name` in column C. The consolidated layout may contain a blank spacer column and places `Employee Name` in column D. Column positions are resolved from the headers after trimming whitespace, while the header names remain part of the source contract.

Worksheets without the required header signature are skipped with an `UnsupportedWorksheetLayout` warning. No column positions or values are guessed.

## Imported values

- The source period label is preserved and must match `yyyy-MM-dd - yyyy-MM-dd`.
- The period start and end dates are parsed into `DateOnly` values.
- Employee numbers and names are required text values.
- Payroll-category cells are preserved as raw decimal values plus their original number format.
- Blank category cells remain blank; zero remains zero.
- Negative category values are valid source inputs that identify a Prorata week, where rosters are used instead of actual hours; they are reported as review warnings and remain imported.
- The importer preserves the negative values and does not infer or calculate roster hours from them. A dedicated roster import remains outside the current workbook contract.
- Category values formatted as `h:mm` are not converted during import. Unit conversion belongs to the calculation-engine phase after the workbook meaning is confirmed.
- Comments are optional and preserved when present.

Rows with invalid required identity, period or category values are excluded from imported rows and returned as row-level errors with sheet, row and cell context. Valid rows and Prorata warnings are still returned.

## Library decision

ClosedXML `0.105.1` is used because it provides a straightforward .NET API for reading `.xlsx` workbooks without requiring Excel. It remains isolated in `Infrastructure` so the application contract and later payroll calculations do not depend on spreadsheet types.

## Smoke test

The read-only smoke test against the October 2026 reference workbook is documented in [workbook-import-smoke-test.md](workbook-import-smoke-test.md), including the complete warning-cell inventory, invalid row, result counts and source-integrity check.
