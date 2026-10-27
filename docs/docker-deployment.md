# Docker deployment

Payroll Automator runs as a single ASP.NET Core container. SQLite and workbook artifacts are stored under `/data`, which must be mounted to persistent host storage.

## Build

From the repository root:

```bash
docker build -t payroll-automator:local .
```

## Run

```bash
docker run -d \
  --name payroll-automator \
  --restart unless-stopped \
  -p 8080:8080 \
  -v /srv/payroll-automator:/data \
  payroll-automator:local
```

The application is available on the host at `http://localhost:8080`. The `/healthz` endpoint returns a successful response when the web host and SQLite database are available.

The first startup creates `/data/payroll.db`. The mounted directory also contains run-specific source and export files. Do not mount a repository directory containing source code or test fixtures as `/data`.

Configuration can be supplied with environment variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `Payroll__DataRoot` | `/data` | SQLite and workbook storage root |
| `Payroll__MaxUploadBytes` | `26214400` | Maximum workbook upload size |
| `Payroll__RetentionDays` | `90` | Age used to identify runs for cleanup |

The application does not automatically delete expired payroll artifacts in this release. Deletion is an explicit operator action in the web UI.

## Cloudflare Tunnel

Run `cloudflared` on the home server separately from the application container and route the selected subdomain to `http://localhost:8080`. Protect that hostname with a Cloudflare Access application. Tunnel tokens, Access configuration and credentials must remain outside the repository.

See [cloudflare-deployment.md](./cloudflare-deployment.md) for the external
deployment checklist and [automation-operations.md](./automation-operations.md)
for the durable scheduled inbox behavior.
