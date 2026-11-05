# Tailscale network policy template

`policy.hujson.example` is a reviewable starting point for the tailnet policy
described in [the network guide](../../docs/home-server-deployment/02-network-access.md).
It is not live configuration. It currently grants operator access only to
`JohannesMogashoa@github`; add the product owner later only if they need
deployment or server administration access. Preserve any unrelated existing
tailnet rules.

Required external steps:

1. Define or review `tag:payroll-server` and `tag:ci-deploy` ownership.
2. Apply the two TCP 22 grants only between the intended sources and
   `tag:payroll-server`.
3. Add the matching Tailscale SSH rule for `payroll-deploy` only.
4. Configure GitHub OIDC workload federation for this repository and the
   protected `production` deployment workflow. Issue only `tag:ci-deploy` and
   grant the `auth_keys` scope required by the GitHub Action.
5. After the LAN fallback is confirmed and the `payroll-deploy` OS account
   exists, run interactively on `bravo`:

   ```sh
   sudo tailscale set --ssh
   ```

6. Verify that Tailscale SSH can reach `payroll-deploy` and cannot reach other
   OS accounts or unrelated ports.

Do not place Tailscale client secrets, OIDC credentials, real operator
identities, or recovery keys in this repository.

