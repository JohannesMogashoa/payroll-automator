# Status and decisions

This document records the evidence gathered and the decisions made for the
home-server deployment. It is the place to update deployment assumptions before
implementation changes are made.

## Initial home-server check (2026-10-28)

| Check | Result | Deployment implication |
| --- | --- | --- |
| Host / OS / architecture | `bravo`, Ubuntu 24.04, `x86_64` | Suitable Linux target for the Docker image and a Linux Actions runner. |
| Root disk | 76 GB free of 98 GB (20% used) | Ample space for an initial image and app data; monitor image growth and backups. |
| Docker | 29.1.3; daemon access confirmed | Container runtime is installed and usable by the connected account. |
| Compose | 2.37.1 | Compose-based local management is available. |
| `cloudflared` binary | `/usr/local/bin/cloudflared` | Installed; inspect and repair the existing host-managed service before adding another tunnel process. |
| Tunnel systemd state | `activating` during the initial check | Do not count the tunnel as healthy until systemd reports `active/running` and the route is tested. |
| GHCR and GitHub HTTPS | Connection failed on port 443 | `bravo` cannot depend on server-side registry pulls; use the Tailscale transfer path unless egress is repaired and reverified. |
| Tailscale | Now connected on `bravo` and the Windows workstation | MagicDNS and SSH over Tailscale have been tested successfully. |

The initial `systemctl is-active ... || echo inactive-or-no-systemd` check could
print `activating` and return non-zero. A later state check should use:

```sh
systemctl show cloudflared -p LoadState -p ActiveState -p SubState -p Result --no-pager
```

Do not share unit command lines, environment values, tokens or URL query values.

## Resolved decisions (2026-10-30)

- Use GitHub-hosted Actions runners and an ephemeral Tailscale node authenticated
  with workload identity federation.
- Use `tag:ci-deploy` for the ephemeral Actions node and `tag:payroll-server`
  for `bravo`.
- Use Tailscale SSH with a dedicated `payroll-deploy` account. Existing OpenSSH
  remains a recovery path.
- Transfer the exact workflow-built image over Tailscale because `bravo` cannot
  currently reach outbound HTTPS. Server-side GHCR pulls are not a runtime
  dependency.
- Keep the GHCR package private. Images must never contain payroll workbooks,
  employee data, credentials or other sensitive material.
- Keep the existing host-managed `cloudflared` service separate from the app.
- Use `payroll.yngstln.dev` as the reviewer-facing hostname.
- Use Cloudflare Access Free with email OTP for exactly two named users: the
  operator and the product owner.
- Require approval through a protected GitHub `production` environment for every
  production deployment. Pull requests never deploy.
- Store backups initially on the same server disk in a separate directory, run
  them daily, retain them for 30 days, and verify a manifest/checksum. The
  operator owns backup and restore operations.
- Retain LAN access as a fallback if Tailscale or Cloudflare is unavailable.
  Add a router-side DHCP reservation for `bravo`.

## Remaining implementation questions

- Finalize the tailnet policy and workload-federation claims for this repository
  and the deployment workflow.
- Create the root-owned deployment script and the narrowly scoped privilege rule
  for `payroll-deploy`.
- Confirm the final server paths and rehearse restore to a temporary location.
- Add and verify the Cloudflare Tunnel route and Access application.
- Document recovery if the server disk, LAN, Tailscale or Cloudflare service is
  unavailable.
