#!/usr/bin/env bash
set -euo pipefail

# Install this as /usr/local/sbin/payroll-automator-deploy, owned by root and
# executable only by root. Configure sudoers so payroll-deploy may run this
# fixed path without a password. The workflow sends a gzip-compressed `docker
# save` archive on stdin.

readonly PROJECT_DIR=/opt/payroll-automator
readonly COMPOSE_FILE="$PROJECT_DIR/compose.yaml"
readonly CURRENT_ENV="$PROJECT_DIR/current.env"
readonly PREVIOUS_ENV="$PROJECT_DIR/previous.env"
readonly HEALTH_URL=http://127.0.0.1:8080/healthz

die() {
  printf 'deployment failed: %s\n' "$1" >&2
  exit 1
}

validate_image_ref() {
  [[ "$1" =~ ^ghcr\.io/[a-z0-9_.-]+/[a-z0-9_.-]+:[0-9a-f]{40}$ ]] \
    || die 'image reference must be a lowercase GHCR image tagged with a commit SHA'
}

health_check() {
  for attempt in {1..30}; do
    if curl --fail --silent --show-error "$HEALTH_URL" >/dev/null; then
      return 0
    fi
    sleep 2
  done
  return 1
}

compose_up() {
  local env_file="$1"
  docker compose --project-directory "$PROJECT_DIR" \
    --file "$COMPOSE_FILE" \
    --env-file "$env_file" \
    up --detach --no-build --force-recreate payroll-automator
}

deploy() {
  local image_ref="$1"
  validate_image_ref "$image_ref"

  gzip --decompress --stdout | docker load >/dev/null

  local candidate_env
  candidate_env=$(mktemp "$PROJECT_DIR/candidate.env.XXXXXX")
  printf 'PAYROLL_IMAGE=%s\n' "$image_ref" > "$candidate_env"
  chown root:root "$candidate_env"
  chmod 0600 "$candidate_env"

  compose_up "$candidate_env"
  if ! health_check; then
    if [[ -f "$CURRENT_ENV" ]]; then
      compose_up "$CURRENT_ENV" || true
    else
      docker compose --project-directory "$PROJECT_DIR" \
        --file "$COMPOSE_FILE" \
        --env-file "$candidate_env" \
        down || true
    fi
    rm -f "$candidate_env"
    die 'container did not become healthy; use rollback after investigation'
  fi

  if [[ -f "$CURRENT_ENV" ]]; then
    install -o root -g root -m 0600 "$CURRENT_ENV" "$PREVIOUS_ENV"
  fi
  mv --force "$candidate_env" "$CURRENT_ENV"
  printf 'deployed %s\n' "$image_ref"
}

rollback() {
  [[ -f "$PREVIOUS_ENV" ]] || die 'no previous image is available for rollback'
  [[ -f "$CURRENT_ENV" ]] || die 'current deployment state is missing'

  local failed_env
  failed_env=$(mktemp "$PROJECT_DIR/failed.env.XXXXXX")
  install -o root -g root -m 0600 "$CURRENT_ENV" "$failed_env"
  install -o root -g root -m 0600 "$PREVIOUS_ENV" "$CURRENT_ENV"
  install -o root -g root -m 0600 "$failed_env" "$PREVIOUS_ENV"
  rm -f "$failed_env"

  compose_up "$CURRENT_ENV"
  health_check || die 'rollback image did not become healthy'
  printf 'rolled back successfully\n'
}

case "${1:-}" in
  deploy)
    [[ $# -eq 2 ]] || die 'usage: deploy <sha-tagged-image>'
    deploy "$2"
    ;;
  rollback)
    [[ $# -eq 1 ]] || die 'usage: rollback'
    rollback
    ;;
  *)
    die 'usage: deploy <sha-tagged-image> | rollback'
    ;;
esac

