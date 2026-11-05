# Server deployment templates

These files are templates for the separately managed deployment directory on
`bravo`. They are not production configuration and must not be copied into the
image or used as a substitute for the server installation checklist.

Expected server paths:

```text
/opt/payroll-automator/compose.yaml
/opt/payroll-automator/current.env
/opt/payroll-automator/previous.env
/srv/payroll-automator/data/
/srv/payroll-automator/backups/
/usr/local/sbin/payroll-automator-deploy
/usr/local/sbin/payroll-automator-backup
/usr/local/sbin/payroll-automator-restore-rehearsal
```

Install `compose.yaml` from this directory as the root-owned Compose project
file. Install the example script as the root-owned deployment script and
review it before enabling the `payroll-deploy` sudo rule. The script accepts a
gzip-compressed Docker image archive on standard input for `deploy`; its
`rollback` command does not read or modify `/data`.

`install-server.sh` is the reviewed baseline installer. Run it interactively
on `bravo` with `sudo` after reviewing the staged files. It creates the locked
`payroll-deploy` account, installs the root-owned files and sudoers rule, and
creates the persistent data and backup directories. The application container
uses UID/GID `1654` for the `/data` directory.

The installer also installs the root-owned backup and restore-rehearsal scripts,
plus the systemd service and timer unit. The timer is deliberately not enabled
by the installer; enable it only after the first healthy application deployment:

```sh
sudo systemctl enable --now payroll-automator-backup.timer
sudo systemctl start payroll-automator-backup.service
sudo /usr/local/sbin/payroll-automator-restore-rehearsal
```

The backup mount is separate from the live data mount. Backups contain the
SQLite database, data-protection keys and run artifacts, with a manifest and
SHA-256 checksums. The restore rehearsal uses a disposable data directory and
does not replace the live data.

The workflow expects these protected `production` environment values:

- `PAYROLL_DEPLOY_HOST`: Tailscale MagicDNS name or approved Tailscale address
  for `bravo`.
- `PAYROLL_DEPLOY_HOST_KEY`: the complete SSH host-key line for that host.

It expects these protected secrets:

- `TS_OAUTH_CLIENT_ID`: Tailscale workload-federation client ID.
- `TS_AUDIENCE`: Tailscale-workload-federation audience.

The Tailscale federated identity must be restricted to this repository and
workflow, issue only `tag:ci-deploy`, and have the `auth_keys` scope required by
the Tailscale GitHub Action. The `production` environment must require manual
approval. Do not store tunnel tokens, payroll data or server credentials in
the repository.

