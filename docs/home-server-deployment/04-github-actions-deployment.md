# GitHub Actions deployment

## Build and image policy

Pull requests continue to run restore, build, tests, Docker image build and the
container health smoke test. A trusted push to protected `master` may build and
publish a private GHCR image tagged with the commit SHA and optionally a
human-readable release tag. Never rely on a mutable `latest` tag as the only
identifier.

The image must contain no payroll workbooks, employee data, credentials or
other sensitive configuration. GHCR is an image archive and rollback source,
not a place for application data.

## Deployment path

`bravo` currently cannot reach outbound HTTPS, including GHCR and GitHub. The
initial deployment path is therefore:

1. GitHub Actions builds and tests the image.
2. The workflow records the immutable commit SHA tag.
3. The workflow joins the tailnet as an ephemeral `tag:ci-deploy` node using
   workload identity federation.
4. The workflow transfers the exact image over Tailscale SSH to `bravo`.
5. The server loads the image and runs the root-owned deployment script through
   the dedicated `payroll-deploy` account.
6. Compose replaces the app container without removing `/data`.
7. The workflow waits for `/healthz` and fails clearly if the check does not
   pass.
8. The last known-good image remains available for rollback.

If server egress is repaired later, a server-side pull may be considered after
retesting DNS, HTTPS and registry authentication. Do not silently change the
registry or deployment path.

If Tailscale SSH cannot reliably run the fixed deployment operation, use a
dedicated environment-scoped SSH deploy key over Tailscale as the fallback. Do
not expose SSH outside the tailnet or use a broad reusable key. A persistent
self-hosted runner is a separate alternative only for a concrete local
dependency.

## Release controls

- Deploy only from protected `master`.
- Do not run deployment for pull requests.
- Require approval through the protected GitHub `production` environment.
- Scope `id-token: write` to the deployment job.
- Scope Tailscale federation claims to this repository and workflow.
- Grant the ephemeral tag access only to `bravo` on TCP 22.
- Treat Docker access as root-equivalent.
- Retain enough redacted logs to diagnose startup without exposing payroll data.

## Rollback

The deployment script must retain the previous known-good SHA, report a failed
health check as a failed workflow, and provide an explicit rollback operation.
Rollback must not delete or recreate the persistent `/data` volume.
