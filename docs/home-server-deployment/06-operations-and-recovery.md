# Operations and recovery

## Readiness gates

### Gate 0: Current state

- Tailscale connectivity and MagicDNS are confirmed.
- The Cloudflare service state and hostname route are understood.
- The GHCR failure is documented and the Tailscale transfer path is selected.

### Gate 1: Private operator access

- The operator can reach `bravo` through Tailscale SSH.
- The operator can reach `bravo` through the LAN fallback.
- The tailnet policy limits deployment access to the intended tags and port.

### Gate 2: Local container baseline

- The Compose project is separate from payroll data.
- `/data` survives container restarts and updates.
- The app binds to loopback only.
- Backup creation, checksum verification and restore have been tested.

### Gate 3: Protected deployment

- A protected workflow deploys a known SHA-tagged image.
- The workflow waits for `/healthz` and reports failure accurately.
- Rollback works without risking `/data`.

### Gate 4: Operational readiness

- Daily backups and 30-day retention are active.
- The operator can restore data and roll back an image.
- Recovery steps exist for Cloudflare, Tailscale, LAN, Docker and disk issues.
- The full flow has been tested with synthetic data.

## Routine checks

```sh
docker compose ps
docker compose logs --tail=100 payroll-automator
curl -fsS http://127.0.0.1:8080/healthz
```

Do not include payroll values in logs or CI output. Keep image cleanup, backup
verification and retention cleanup explicit and auditable.

## Recovery order

1. If the public UI is unavailable, check the host-managed `cloudflared`
   service and the Cloudflare route.
2. If administration is unavailable, use the reserved LAN address or local
   console to inspect Tailscale and Docker.
3. If the app is unhealthy after deployment, stop promotion and roll back to the
   last known-good image.
4. If application data is damaged, stop the app, preserve the current data for
   investigation, and restore a verified backup into a temporary location before
   replacing the live data.
5. If the disk or server is lost, the same-disk backup is not sufficient; use
   the future external backup plan or reconstruct from a separately retained
   backup.

## Security and privacy

Keep tunnel tokens, Access details, Tailscale federation credentials, SSH
material, GHCR credentials and server paths outside Git. Never place real
employee data in GitHub Actions artifacts, container layers, runner workspaces
or CI logs.
