# Workbook import smoke-test report

## Run details

- Run date: 2026-10-26.
- Command: `dotnet run --project src/PayrollAutomator --configuration Release --no-restore -- import "docs/October 2026 Payroll Hours.xlsx"`
- Source: `docs/October 2026 Payroll Hours.xlsx`.
- Source SHA-256 after the run: `51F14429E2EAEA0FDFB5B2BBDDEB1D57C8BAAF9ADDC599D1802F183A2F673D89`.
- Source unchanged: confirmed by comparing SHA-256 before and after import.
- No source values or employee-level data were copied into the repository or test fixtures.

## Result summary

| Result | Count |
| --- | ---: |
| Imported rows | 1,131 |
| Errors | 1 |
| Warnings | 137 |
| Unsupported worksheet warnings | 1 |
| Negative payroll-value warnings | 136 |

Warnings do not exclude rows. The one row with an error was excluded from the imported result.

## Diagnostics

### Negative payroll values (valid Prorata inputs)

The importer identified each negative raw value as a `NegativePayrollValue` warning. Negative inputs are valid because they indicate that the employee did not work those hours. This occurs in a Prorata week, where rosters are used instead of actual hours. The negative source value is preserved and the row remains imported; the importer does not calculate roster hours.

| Worksheet | Field | Count | Warning cells |
| --- | --- | ---: | --- |
| `2026-09-21 - 2026-09-27` | `Normal` | 19 | `D2`, `D3`, `D13`, `D14`, `D24`, `D32`, `D33`, `D34`, `D37`, `D41`, `D42`, `D50`, `D53`, `D56`, `D58`, `D60`, `D65`, `D73`, `D77` |
| `2026-09-21 - 2026-09-27` | `Sunday 0.5` | 21 | `F8`, `F9`, `F10`, `F11`, `F13`, `F15`, `F23`, `F26`, `F27`, `F31`, `F33`, `F34`, `F58`, `F59`, `F63`, `F65`, `F66`, `F69`, `F88`, `F96`, `F107` |
| `2026-09-21 - 2026-09-27` | `PH` | 29 | `G8`, `G11`, `G12`, `G16`, `G23`, `G40`, `G41`, `G42`, `G43`, `G44`, `G45`, `G49`, `G50`, `G52`, `G58`, `G71`, `G73`, `G81`, `G82`, `G88`, `G95`, `G96`, `G97`, `G99`, `G101`, `G105`, `G106`, `G107`, `G108` |
| `Olivedale Employees` | `Normal` | 19 | `E2`, `E9`, `E79`, `E86`, `E156`, `E212`, `E219`, `E226`, `E247`, `E275`, `E282`, `E338`, `E359`, `E380`, `E394`, `E408`, `E443`, `E499`, `E527` |
| `Olivedale Employees` | `Sunday 0.5` | 21 | `G44`, `G51`, `G58`, `G65`, `G79`, `G93`, `G149`, `G170`, `G177`, `G205`, `G219`, `G226`, `G394`, `G401`, `G429`, `G443`, `G450`, `G471`, `G604`, `G660`, `G746` |
| `Olivedale Employees` | `PH` | 27 | `H44`, `H65`, `H72`, `H100`, `H149`, `H268`, `H275`, `H282`, `H289`, `H296`, `H303`, `H331`, `H338`, `H394`, `H485`, `H499`, `H555`, `H562`, `H604`, `H653`, `H660`, `H667`, `H681`, `H695`, `H723`, `H739`, `H746` |

### Unsupported worksheet

| Worksheet | Code | Count | Action |
| --- | --- | ---: | --- |
| `Sheet4` | `UnsupportedWorksheetLayout` | 1 | Skipped because it does not contain the required payroll headers in row 1. |

## Invalid rows

Negative-value rows are valid and are not included in the invalid-row list. They are fully inventoried above as warnings.

| Worksheet | Source row | Cell | Code | Diagnostic | Action |
| --- | ---: | --- | --- | --- | --- |
| `2026-09-28 - 2026-10-04` | 126 | `A126` | `InvalidPeriod` | `Date` must be a valid period in `yyyy-MM-dd - yyyy-MM-dd` format. | Row excluded from imported results. |

The complete negative-value warning inventory is listed above. Only row 126 is excluded for its invalid period.

The error row contains other row content but no valid period value in `A126`; the importer reports the row rather than guessing its period.

## Follow-up

- Preserve the Prorata-week signal through Phase 3 review and calculation design; do not reinterpret negative source values as ordinary actual hours.
- Confirm the intended handling of the incomplete row 126 in the `2026-09-28 - 2026-10-04` worksheet.
- Decide in Phase 3 whether values formatted as `h:mm` represent Excel day fractions or another source-specific unit before converting them into payable hours.
