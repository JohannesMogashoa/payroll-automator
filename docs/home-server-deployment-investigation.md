# Home server deployment investigation

This is the index for the home-server deployment plan. The deployment is not
live: no server, Cloudflare account, registry or GitHub secrets are configured
by this repository.

The plan targets the existing Ubuntu 24.04 host `bravo` and keeps the payroll
application as a single Docker container. GitHub Actions builds and tests the
image; a protected production deployment reaches `bravo` through Tailscale.
Cloudflare Tunnel and Access remain the reviewer-facing path.

## Current decisions

| Area | Decision |
| --- | --- |
| Private administration | Tailscale, with MagicDNS and Tailscale SSH |
| Actions runner | GitHub-hosted runner with an ephemeral Tailscale node |
| Deployment identity | Tailscale workload identity federation |
| Tailscale tags | `tag:ci-deploy` and `tag:payroll-server` |
| Server deploy account | Dedicated `payroll-deploy` account |
| Image delivery | Transfer the workflow-built image over Tailscale; `bravo` cannot currently reach outbound HTTPS |
| GHCR | Private package, used as an image archive/rollback source |
| Cloudflare | Existing host-managed `cloudflared` service |
| Reviewer hostname | `payroll.yngstln.dev` |
| Reviewer authentication | Cloudflare Access Free plan, email OTP, exactly two named users |
| Production release | Protected GitHub `production` environment with manual approval |
| Backups | Same server disk, daily, 30-day retention, checksum verification |
| Backup owner | Operator |
| Recovery fallback | LAN access, with a router DHCP reservation for `bravo` |

Email addresses, tokens, account identifiers, tunnel configuration and other
credentials must remain outside Git.

## Focused guides

1. [Status and decisions](home-server-deployment/01-status-and-decisions.md)
2. [Network and private access](home-server-deployment/02-network-access.md)
3. [Container and storage](home-server-deployment/03-container-and-storage.md)
4. [GitHub Actions deployment](home-server-deployment/04-github-actions-deployment.md)
5. [Cloudflare Tunnel and Access](home-server-deployment/05-cloudflare-access.md)
6. [Operations and recovery](home-server-deployment/06-operations-and-recovery.md)

## Implementation order

1. Complete the remaining checks in the [status guide](home-server-deployment/01-status-and-decisions.md).
2. Establish the tagged Tailscale policy and dedicated deploy account.
3. Add the server-side Compose project, persistent paths and backup procedure.
4. Add the protected GHCR build and Tailscale image-transfer workflow.
5. Add and verify the Cloudflare hostname route and Access policy.
6. Rehearse health checks, rollback, restore and recovery using synthetic data.
