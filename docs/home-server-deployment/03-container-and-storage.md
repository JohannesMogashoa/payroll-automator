# Container and storage

## Server layout

Use a dedicated server-side deployment directory separate from source code and
payroll data:

```text
/opt/payroll-automator/                 Compose project and deployment scripts
/srv/payroll-automator/data/            Persistent app data mounted at /data
/srv/payroll-automator/backups/         Daily local backup set
```

The final paths should be created with restricted ownership and permissions.
Do not place real payroll workbooks in the repository, image layers or CI
workspace.

## Compose topology

Use Docker Compose for repeatable start, stop, status, update and log commands.
Pin the application image to an immutable commit SHA tag. Do not rebuild from
source on `bravo` and do not use `docker compose down --volumes` during routine
updates.

Because the existing `cloudflared` service is host-managed, publish the app on
loopback only:

```text
127.0.0.1:8080:8080
```

The host tunnel will route to `http://127.0.0.1:8080`. Do not run a second
Cloudflare Tunnel process at the same time.

Useful operator commands:

```sh
docker compose pull
docker compose up -d
docker compose ps
docker compose logs --tail=100 payroll-automator
docker compose down
```

## Persistent data and backups

The application data under `/data` includes SQLite, data-protection keys and run
artifacts. Back it up before schema or storage changes. The initial policy is:

- Store backups on the same server disk in `/srv/payroll-automator/backups/`.
- Create a backup daily.
- Retain 30 days.
- Write a manifest and checksums, then verify them.
- Rehearse restoring into a temporary directory.
- The operator owns backup and restore operations.

Same-disk backups help with accidental deletion or application corruption but do
not protect against disk failure, theft or loss of the server. Move to an
external disk, NAS or cloud target before relying on the deployment for real
payroll operations.
