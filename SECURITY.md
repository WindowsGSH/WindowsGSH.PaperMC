# Security policy

## Security and trust

The PaperMC module executes with the same Windows permissions as WindowsGSH. WindowsGSH cannot guarantee arbitrary third-party or modified module packages. Review executable module source before loading it.

## Download modules safely

Obtain this module from the official WindowsGSH.PaperMC repository or another source you explicitly trust. Verify the repository, release origin and changed files before installing updates. Do not load unexpected compiled files or source changes.

## Protect credentials and server data

RCON passwords, player information, server properties, logs and backup archives may be sensitive. Keep RCON private, restrict filesystem access, and do not publish unredacted configuration, logs or support bundles. Paper requires its RCON password in `server.properties` when RCON is enabled.

## Report a vulnerability

Use the [private repository advisory page](https://github.com/WindowsGSH/WindowsGSH.PaperMC/security/advisories/new) before opening a public issue. Do not include working exploits, secrets or private server data in a public ticket.

## Include in a report

Include the affected module and WindowsGSH versions, Minecraft/Paper and Java versions, the source of the module package, reproduction steps, expected and observed behavior, and sanitized diagnostics. Remove passwords, tokens, public addresses and personal data.

## Supported versions

Security fixes are provided for the current module release unless the repository explicitly states otherwise. Upgrade to the latest supported WindowsGSH and module versions before reporting a resolved issue.
