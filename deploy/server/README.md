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
/usr/local/sbin/payroll-automator-deploy
```

Install `compose.yaml` from this directory as the root-owned Compose project
file. Install the example script as the root-owned deployment script and
review it before enabling the `payroll-deploy` sudo rule. The script accepts a
gzip-compressed Docker image archive on standard input for `deploy`; its
`rollback` command does not read or modify `/data`.

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

