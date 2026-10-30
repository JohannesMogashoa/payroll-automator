# Cloudflare Tunnel and Access

## Origin topology

Keep `cloudflared` separate from the application process and use the existing
host-managed service:

```text
https://payroll.yngstln.dev
        |
        v
existing host-managed cloudflared service
        |
        v
http://127.0.0.1:8080
        |
        v
Payroll Automator container
```

The hostname route for `payroll.yngstln.dev` still needs to be added to the
existing tunnel. First confirm the systemd service reaches `active/running`:

```sh
systemctl show cloudflared -p LoadState -p ActiveState -p SubState -p Result --no-pager
```

Inspect service logs locally if it remains stuck, and redact tokens, credentials,
hostnames and URL query values before sharing output.

## Access policy

Use the Cloudflare Zero Trust Free plan with email One-Time PIN authentication.
Allow exactly two named email addresses: the operator and the product owner.
Keep those addresses and all Cloudflare configuration outside Git.

Do not create an allow rule that includes the One-Time PIN login method by
itself; that can allow any email address. Restrict the allow policy to the two
named addresses and require the OTP login method. Test both an allowed and an
unauthorized address.

References: [Cloudflare One-Time PIN](https://developers.cloudflare.com/cloudflare-one/integrations/identity-providers/one-time-pin/),
[Access policies](https://developers.cloudflare.com/cloudflare-one/access-controls/policies/).

## Verification

Before handling real payroll data:

1. Confirm the tunnel service is healthy.
2. Confirm `payroll.yngstln.dev` reaches only the loopback origin.
3. Confirm an unauthorized request is denied.
4. Confirm both named users receive an OTP and complete the full review/export
   flow.
5. Confirm no host port other than the intended loopback binding exposes the
   application.

Cloudflare Tunnel provides connectivity, not application authorization. Keep
the Access policy enabled and keep Tailscale as the administration path.
