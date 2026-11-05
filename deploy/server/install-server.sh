#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  printf 'run this installer with sudo\n' >&2
  exit 1
fi

readonly SOURCE_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
readonly PROJECT_DIR=/opt/payroll-automator
readonly DATA_DIR=/srv/payroll-automator/data
readonly BACKUP_DIR=/srv/payroll-automator/backups
readonly DEPLOY_SCRIPT=/usr/local/sbin/payroll-automator-deploy
readonly BACKUP_SCRIPT=/usr/local/sbin/payroll-automator-backup
readonly RESTORE_SCRIPT=/usr/local/sbin/payroll-automator-restore-rehearsal
readonly BACKUP_SERVICE=/etc/systemd/system/payroll-automator-backup.service
readonly BACKUP_TIMER=/etc/systemd/system/payroll-automator-backup.timer
readonly SUDOERS_FILE=/etc/sudoers.d/payroll-automator-deploy

if ! id payroll-deploy >/dev/null 2>&1; then
  useradd --create-home --shell /bin/bash --comment 'Payroll Automator deployment account' payroll-deploy
fi
passwd --lock payroll-deploy >/dev/null

install --directory --owner root --group root --mode 0750 "$PROJECT_DIR"
install --directory --owner 1654 --group 1654 --mode 0700 "$DATA_DIR"
install --directory --owner 1654 --group 1654 --mode 0700 "$BACKUP_DIR"

install --owner root --group root --mode 0640 \
  "$SOURCE_DIR/compose.yaml" "$PROJECT_DIR/compose.yaml"
install --owner root --group root --mode 0750 \
  "$SOURCE_DIR/payroll-automator-deploy.example.sh" "$DEPLOY_SCRIPT"
install --owner root --group root --mode 0750 \
  "$SOURCE_DIR/payroll-automator-backup.example.sh" "$BACKUP_SCRIPT"
install --owner root --group root --mode 0750 \
  "$SOURCE_DIR/payroll-automator-restore-rehearsal.example.sh" "$RESTORE_SCRIPT"
install --owner root --group root --mode 0644 \
  "$SOURCE_DIR/payroll-automator-backup.service" "$BACKUP_SERVICE"
install --owner root --group root --mode 0644 \
  "$SOURCE_DIR/payroll-automator-backup.timer" "$BACKUP_TIMER"
systemctl daemon-reload

sudoers_temp=$(mktemp)
trap 'rm -f -- "$sudoers_temp"' EXIT
printf '%s\n' \
  'Defaults!/usr/local/sbin/payroll-automator-deploy !setenv' \
  'payroll-deploy ALL=(root) NOPASSWD: /usr/local/sbin/payroll-automator-deploy *' \
  > "$sudoers_temp"
install --owner root --group root --mode 0440 "$sudoers_temp" "$SUDOERS_FILE"
visudo --check --file "$SUDOERS_FILE"

printf 'installed payroll-deploy deployment baseline\n'
printf 'project: %s\n' "$PROJECT_DIR"
printf 'data: %s\n' "$DATA_DIR"
printf 'backups: %s\n' "$BACKUP_DIR"
printf 'deployment script: %s\n' "$DEPLOY_SCRIPT"
printf 'backup script: %s\n' "$BACKUP_SCRIPT"
printf 'restore rehearsal: %s\n' "$RESTORE_SCRIPT"
printf 'backup timer (not enabled): %s\n' "$BACKUP_TIMER"
printf 'sudoers: %s\n' "$SUDOERS_FILE"

