#!/usr/bin/env bash
set -euo pipefail

# Install this as /usr/local/sbin/payroll-automator-backup, owned by root.

readonly CONTAINER_NAME=payroll-automator
readonly PROJECT_DIR=/opt/payroll-automator
readonly CURRENT_ENV="$PROJECT_DIR/current.env"
readonly BACKUP_DIR=/srv/payroll-automator/backups
readonly RETENTION_DAYS=30

die() {
  printf 'backup failed: %s\n' "$1" >&2
  exit 1
}

[[ "${EUID}" -eq 0 ]] || die 'run as root'
[[ -r "$CURRENT_ENV" ]] || die "missing $CURRENT_ENV"
[[ -d "$BACKUP_DIR" ]] || die "missing $BACKUP_DIR"

# current.env is root-owned and contains only the immutable image reference.
set -a
# shellcheck disable=SC1091
. "$CURRENT_ENV"
set +a
[[ "${PAYROLL_IMAGE:-}" =~ ^ghcr\.io/[a-z0-9_.-]+/[a-z0-9_.-]+:[0-9a-f]{40}$ ]] \
  || die 'current.env does not contain a valid image reference'

[[ "$(docker inspect --format '{{.State.Running}}' "$CONTAINER_NAME" 2>/dev/null || true)" == "true" ]] \
  || die 'payroll-automator container is not running'

# The application performs an SQLite-consistent backup, copies keys and run
# artifacts, writes a manifest and verifies the result before returning.
docker exec --user 1654:1654 "$CONTAINER_NAME" \
  dotnet /app/PayrollAutomator.dll backup

# Retention is intentionally separate from application run cleanup. Only
# timestamp-named directories directly below the known backup root qualify.
find "$BACKUP_DIR" -mindepth 1 -maxdepth 1 -type d -name '20*' \
  -mtime +"$RETENTION_DAYS" -print -exec rm -rf -- {} +

printf 'backup completed; retained %s days\n' "$RETENTION_DAYS"
