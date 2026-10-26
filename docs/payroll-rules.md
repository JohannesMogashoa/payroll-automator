# Payroll calculation rules

These rules record the decisions agreed for [GitHub issue #1](https://github.com/JohannesMogashoa/payroll-automator/issues/1). They apply until a later, explicitly agreed rule replaces them.

## Normal hours

- Supplied normal hours are authoritative.
- The application does not derive normal hours from start and end times.
- Break deductions and overnight-shift handling must already be reflected in the supplied normal hours.
- When workbook import is implemented, a missing employee, date or normal-hours value will make the source row invalid. Invalid rows must be reported for review and must not be silently discarded.

## Minute conversion

Convert the minute remainder of an hour into payroll hours using these explicit buckets:

| Minutes | Payroll hours |
| --- | ---: |
| 0–24 | 0 |
| 25–45 | 0.5 |
| 46–59 | 1 |

Minute values below `0` or greater than `59` are invalid. Composing whole hours with the converted minute remainder belongs to the later calculation-engine phase.

## Hourly rate and payable amount

- The default hourly rate is `R195`.
- A caller may supply a different positive hourly rate.
- If no rate is supplied, the calculation uses `R195`.
- Calculated payroll hours may be zero but cannot be negative. Raw workbook category values may be negative when they identify a valid Prorata week; those weeks use rosters instead of actual hours and must remain distinguishable during import.
- The payable amount is `payroll hours × hourly rate`.
- Round only the final payable amount to two decimal places using midpoint rounding away from zero.

Examples:

| Payroll hours | Rate | Payable amount |
| ---: | ---: | ---: |
| 0 | R195 | R0.00 |
| 0.5 | R195 | R97.50 |
| 1 | R195 | R195.00 |
| 2.5 | R100 | R250.00 |

## Special days and deferred rules

- Weekend and public-holiday hours currently use the configured hourly rate without a premium.
- Overtime categories, premiums and reconciliation belong to the calculation-engine phase and must not be inferred from these rules.
- The exact workbook layout remains unresolved and will be decided during workbook import work.

## Calculation-engine rules

- Excel values with an `h:mm`-style number format are interpreted as Excel day fractions and converted to hours plus the agreed minute bucket.
- Other numeric category values are interpreted as decimal hours and use the same minute bucket for their fractional hour.
- Values that do not represent a whole minute are calculation errors; the engine does not silently coerce them.
- Blank category values contribute zero hours and zero amount.
- Negative category values identify a Prorata row. The raw value is preserved, but the row is excluded from ordinary payable totals until roster hours are supplied.
- The base hourly rate defaults to `R195`.
- Category multipliers are supplied through an explicit rate policy. The engine configures Normal as `1.0` by default and does not infer multipliers from category names.
- A row total is the sum of its configured category results. Employee totals include only complete, non-Prorata rows and report incomplete rows as diagnostics.
