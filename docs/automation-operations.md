# Automation operations

The supported watched-folder mode is a durable scheduled poll. The hosted
worker scans the configured inbox, checks workbook stability, records a SQLite
job lease, and processes each candidate through the same deterministic pipeline
as the CLI. This is preferred over an in-memory `FileSystemWatcher` because it
survives application restarts and does not lose events while the process is
offline.

Configure the worker explicitly:

```text
Payroll__Automation__Enabled=true
Payroll__Automation__ScanInterval=00:05:00
Payroll__Automation__FileStabilityDelay=00:00:30
Payroll__Automation__MaxConcurrentJobs=1
```

The inbox, processing, processed, quarantine, output and backup paths are
relative to `Payroll__DataRoot` and the configured automation root. The worker
creates all configured directories at startup. Invalid workbooks are moved to
quarantine; valid workbooks become reviewable runs and are never auto-exported.

The implementation deliberately does not add a second continuous watcher. If
future volume requires lower latency, add it as another discovery adapter that
enqueues the existing durable job record rather than bypassing leases,
stability checks or review confirmation.
