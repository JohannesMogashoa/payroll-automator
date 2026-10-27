# Cloudflare deployment checklist

This repository intentionally contains no deployment-specific Cloudflare
account IDs, tunnel tokens, Access policies or public hostnames. Store those
values in the deployment environment or a password manager.

## Required deployment values

Record the following outside Git:

- the Cloudflare account and zone identifiers;
- the tunnel name and token;
- the public payroll hostname;
- the Access application policy and allowed identities/groups;
- the origin URL, normally `http://localhost:8080` on the tunnel host.

## Tunnel configuration

Run `cloudflared` separately from the application container. The tunnel should
route only the selected payroll hostname to the local application origin:

```yaml
tunnel: <external-tunnel-id>
credentials-file: <external-credentials-path>

ingress:
  - hostname: payroll.example.invalid
    service: http://localhost:8080
  - service: http_status:404
```

Use the real hostname, tunnel ID and credentials only in the deployment host's
configuration. Do not commit this example with real values.

## Access and operational checks

1. Create a Cloudflare Access application for the payroll hostname.
2. Require the smallest practical group of named reviewers.
3. Keep the origin port private; expose it through the tunnel rather than a
   direct public firewall rule.
4. Confirm `/healthz` from the host before testing Access.
5. Confirm an unauthorised identity is denied and an authorised reviewer can
   complete upload, review, confirmation and download.
6. Store the tunnel token and Access configuration in the deployment secret
   store, never in `.env.example`, logs or issue comments.

See [docker-deployment.md](./docker-deployment.md) for the container and
persistent-volume configuration.
