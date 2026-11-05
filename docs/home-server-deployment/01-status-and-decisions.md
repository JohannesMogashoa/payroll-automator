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

## Live pre-deployment verification (2026-11-05)

The server-side read-only checks were completed through the SSH MCP:

| Check | Result | Follow-up |
| --- | --- | --- |
| SSH operator access | Connected as the existing `stallion` account | Keep this as recovery access; create `payroll-deploy` separately. |
| `cloudflared` systemd state | `loaded`, `active`, `running`, `success` | Tunnel route and Access policy still need an end-to-end test after the app is deployed. |
| Tailscale | Connected on `bravo` and the Windows workstation; client version `1.104.4` | Tailnet policy and Tailscale SSH permissions remain to be configured and verified. |
| LAN route | Default route uses `enp3s0` via `192.168.10.1`, source `192.168.10.191`; direct operator SSH succeeded via `192.168.3.7` on the workstation’s LAN | Reserve the working LAN address `192.168.3.7` for `bravo` in the router, using the matching `enp3s0` interface. Keep the separate `192.168.10.191` route documented; do not reserve the Tailscale address. |
| Docker / Compose | Docker `29.1.3`; Compose `2.40.3` | Current versions are suitable; the earlier Compose `2.37.1` observation is stale. |
| Payroll app listener | Nothing is listening on `127.0.0.1:8080` yet | Expected until the first application deployment. |
| Other host listeners | Existing services bind `0.0.0.0:3000` and `0.0.0.0:5000` | Outside this project’s scope; review separately before relying on host exposure controls. |
| Tailscale SSH | Enabled (`RunSSH: true`); `bravo` advertises `tag:payroll-server` | The policy is now applied; end-to-end access remains pending creation of the `payroll-deploy` OS account. |

The router DHCP reservation is still outstanding. The direct LAN SSH fallback
test succeeded. The only server-side change so far is enabling the Tailscale
SSH service; the tailnet policy, deploy account and application have not been
changed.

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

## Repository preparation started (2026-11-05)

The repository now contains a manual-only production deployment workflow and
server templates. They are intentionally not considered a live deployment:

- `.github/workflows/deploy-production.yml` runs only from `master`, requires
  the protected `production` environment, publishes the exact commit SHA image,
  and transfers that image over Tailscale SSH.
- `deploy/server/compose.yaml` binds the application to `127.0.0.1:8080` and
  mounts `/srv/payroll-automator/data` at `/data`.
- `deploy/server/payroll-automator-deploy.example.sh` provides explicit
  deploy and rollback commands while preserving `/data`.

The next external gate remains outstanding: reserve `bravo`'s LAN address,
enable and test the backup timer, complete the restore rehearsal, verify the
Cloudflare route and Access policy, and rehearse the recovery paths.

## First deployment verification (2026-11-05)

The protected manual deployment workflow completed successfully for commit
`b255576`. It built and tested the application, published the private immutable
GHCR image, joined the tailnet using workload identity federation, transferred
the image with Tailscale SSH, and completed the server health check.

| Check | Result |
| --- | --- |
| Application image | `ghcr.io/johannesmogashoa/payroll-automator:b255576e4294c8316c52403a0b475745534d909d` |
| Container | `payroll-automator`, running with loopback port `127.0.0.1:8080` |
| Persistent mounts | `/srv/payroll-automator/data:/data` and `/srv/payroll-automator/backups:/data/automation/backups` |
| Health endpoint | `http://127.0.0.1:8080/healthz` returned `Healthy` |
| Deployment path | GitHub Actions → ephemeral `tag:ci-deploy` → Tailscale SSH → `payroll-deploy` |

The backup timer remains intentionally disabled until the first healthy
deployment has been verified by the operator. The remaining deployment gates
are the stable LAN DHCP reservation, timer/restore rehearsal, Cloudflare route
and Access verification, and the full recovery rehearsal.
