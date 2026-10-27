# Workbook export contract

Phase 4 exports a new workbook and never modifies the imported source workbook. An existing output path is rejected rather than overwritten.

The version 1 export contains these sheets:

## \`Payroll\`

One row is written for each source row and payroll category. The sheet preserves the source sheet and row number, employee identity, period, source value, normalized payroll hours, payable amount, configuration status and Prorata/Excel-time flags.

## \`Employee Totals\`

One row is written for each employee, followed by a \`TOTAL\` row. Totals include only complete, non-Prorata rows as defined by the calculation engine. Included-row and Prorata-row counts remain visible for review.

## \`Review\`

The review summary reports calculated, complete, incomplete and Prorata rows, plus error and warning counts. Every import or calculation issue is listed with severity, code, message, worksheet, source row, cell and category where available.

Errors prevent confirmation and export. Warnings may be exported only after an explicit reviewer confirmation.

## \`Metadata\`

The metadata sheet records the application version, payroll rule-set version, hourly rate, configured category multipliers, source workbook name and SHA-256 fingerprint, output workbook name, reviewer, confirmation time and review counts.

## \`Audit\`

The audit sheet records the review confirmation and successful export events. It intentionally does not include unnecessary employee-level payroll values.

The exported employee totals must reconcile with the calculation report before the workbook is written. The output path must differ from the source path.
