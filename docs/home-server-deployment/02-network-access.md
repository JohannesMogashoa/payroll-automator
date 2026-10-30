# Network and private access

## Topology

Use Tailscale as the private administration and deployment plane:

```text
GitHub-hosted Actions runner
        |
        | ephemeral Tailscale node: tag:ci-deploy
        v
Tailscale tailnet  --->  bravo: tag:payroll-server
                              |
                              +-- Tailscale SSH: payroll-deploy
                              +-- loopback app port only
```

Cloudflare Tunnel is a separate reviewer-facing path. Do not use Tailscale
Funnel, and do not expose Docker or SSH to the public internet.

If a private browser route is needed in addition to SSH, Tailscale Serve may
proxy a deliberately loopback-published app endpoint to the tailnet. It is
optional and must not replace the Cloudflare Access reviewer path.

## Tailscale policy

The policy should allow the following and deny other deployment traffic:

- `tag:ci-deploy` to `tag:payroll-server` on TCP 22 only.
- Tailscale SSH from `tag:ci-deploy` to the `payroll-deploy` OS account only.
- Named operator identities to `bravo` for administration.

The GitHub federation trust must match this repository and the deployment
workflow, and must issue only the `tag:ci-deploy` identity. Keep `id-token: write`
limited to the deployment job.

An always-on self-hosted runner is an alternative only if deployment needs
local hardware or services unavailable to GitHub-hosted runners. It adds a
maintained trusted agent on the home server and is not the default.

Tailscale SSH requires both a network permission and an SSH permission in the
tailnet policy. Enable it on `bravo` only after the existing LAN/SSH recovery
path is confirmed:

```sh
sudo tailscale set --ssh
```

References: [Tailscale SSH](https://tailscale.com/docs/features/tailscale-ssh),
[access control and grants](https://tailscale.com/docs/features/access-control),
[GitHub workload identity federation](https://tailscale.com/docs/features/workload-identity-federation).

## LAN recovery and DHCP reservation

Both operator devices are on the same home Wi-Fi network. Keep LAN access
available while Tailscale SSH and Cloudflare Tunnel are configured.

On `bravo`, identify the current LAN address, interface and gateway:

```sh
ip -4 route get 1.1.1.1
ip -4 addr
ip link
```

Open the router administration page using the default gateway address. In the
DHCP client or connected-device list, select `bravo` and reserve its current LAN
address for the matching interface MAC address. Use a router-side reservation
rather than choosing an arbitrary static address in Ubuntu, and never reserve
or reuse the Tailscale address.

Verify from Windows:

```powershell
ping <bravo-lan-ip>
ssh <existing-linux-user>@<bravo-lan-ip>
```

## Security notes

An account that can control Docker is root-equivalent in practice. Protect the
`payroll-deploy` identity, its Tailscale policy and the deployment workflow as
privileged production credentials. A fixed root-owned deployment script reduces
mistakes but is not a security boundary against an identity that can control
Docker.
