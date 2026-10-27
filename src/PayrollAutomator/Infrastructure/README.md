# Infrastructure

Adapters for reading and writing workbooks, storing run data and managing
filesystem artifacts live here. Keep spreadsheet-specific code out of the
domain rules so the calculation engine remains testable.

## Workbook import

ClosedXmlWorkbookImporter reads .xlsx files with ClosedXML 0.105.1 using
read-only file access. It recognizes the two layouts represented by the
October 2026 reference workbook:

- Weekly payroll sheets with row-1 headers and Employee Name in column C.
- Consolidated employee sheets with the same payroll-category headers and Employee Name in column D.

The importer preserves source sheet and row numbers, period labels and parsed
period dates, raw numeric payroll values, cell number formats and comments. It
does not convert h:mm values, calculate payroll hours or overwrite the source
workbook. Negative payroll values are valid source inputs that identify a
Prorata week, where rosters are used instead of actual hours; they produce
review warnings and remain imported. Unrecognized sheets produce diagnostics
and are skipped.

## Run persistence

PayrollDbContext stores run metadata and audit events in SQLite.
LocalPayrollFileStore keeps each source workbook and export under a run-specific
directory below the configured data root. The web application never uses an
uploaded path as a filesystem path and removes artifacts only through an
explicit run-deletion action.

For local development the data root defaults to data. In the container it is
/data, which should be backed by a persistent Docker volume or host mount.
The container runs as a non-root user.

## Workbook export

ClosedXmlPayrollWorkbookExporter writes a new versioned workbook containing
Payroll, Employee Totals, Review, Metadata and Audit sheets. It rejects an
existing output file and refuses to use the source path as the output path.
The export contract and reconciliation rules are documented in
[docs/workbook-export.md](../../../docs/workbook-export.md).
