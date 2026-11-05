#!/usr/bin/env bash
set -euo pipefail

# Run this as root. It verifies the selected backup, restores it into a
# disposable directory, and starts the exact deployed image against that copy.

readonly PROJECT_DIR=/opt/payroll-automator
readonly CURRENT_ENV="$PROJECT_DIR/current.env"
readonly BACKUP_ROOT=/srv/payroll-automator/backups
readonly RESTORE_ROOT=/srv/payroll-automator/restore-rehearsal
readonly RESTORE_PORT=18080

die() {
  printf 'restore rehearsal failed: %s\n' "$1" >&2
  exit 1
}

[[ "${EUID}" -eq 0 ]] || die 'run as root'
[[ -r "$CURRENT_ENV" ]] || die "missing $CURRENT_ENV"
[[ -d "$BACKUP_ROOT" ]] || die "missing $BACKUP_ROOT"

set -a
# shellcheck disable=SC1091
. "$CURRENT_ENV"
set +a
[[ "${PAYROLL_IMAGE:-}" =~ ^ghcr\.io/[a-z0-9_.-]+/[a-z0-9_.-]+:[0-9a-f]{40}$ ]] \
  || die 'current.env does not contain a valid image reference'

backup_directory="${1:-}"
if [[ -z "$backup_directory" ]]; then
  backup_directory=$(find "$BACKUP_ROOT" -mindepth 1 -maxdepth 1 -type d -name '20*' -printf '%T@ %p\n' \
    | sort -nr | sed -n '1s/^[^ ]* //p')
fi
[[ -n "$backup_directory" && -d "$backup_directory" ]] \
  || die 'provide an existing backup directory or create one first'
backup_directory=$(readlink -f -- "$backup_directory")
[[ "$backup_directory" == "$BACKUP_ROOT"/* ]] \
  || die 'backup must be inside the configured backup root'

docker run --rm --user 1654:1654 \
  --volume "$backup_directory:/backup:ro" \
  "$PAYROLL_IMAGE" backup verify /backup

restore_directory=$(mktemp -d "$RESTORE_ROOT.XXXXXX")
container_name="payroll-automator-restore-rehearsal-$(date +%s)"
cleanup() {
  docker rm --force "$container_name" >/dev/null 2>&1 || true
  rm -rf -- "$restore_directory"
}
trap cleanup EXIT

cp -a "$backup_directory/payroll.db" "$restore_directory/payroll.db"
for directory in keys runs; do
  if [[ -d "$backup_directory/$directory" ]]; then
    cp -a "$backup_directory/$directory" "$restore_directory/$directory"
  fi
done
chown -R 1654:1654 "$restore_directory"

docker run --detach --rm --name "$container_name" --user 1654:1654 \
  --publish "127.0.0.1:${RESTORE_PORT}:8080" \
  --volume "$restore_directory:/data" \
  "$PAYROLL_IMAGE" >/dev/null

for attempt in {1..30}; do
  if curl --fail --silent "http://127.0.0.1:${RESTORE_PORT}/healthz" >/dev/null; then
    printf 'restore rehearsal passed: %s\n' "$backup_directory"
    exit 0
  fi
  sleep 2
done

die 'restored application did not become healthy'
