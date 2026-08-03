# PaperMC Dedicated Server

[![WindowsGSH](.github/assets/windowsgsh-badge.svg)](https://windowsgsh.com)
[![Status](https://img.shields.io/badge/status-beta_candidate-22C55E)](#status)
[![Module version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FWindowsGSH%2FWindowsGSH.PaperMC%2Fmain%2FPaperMC.mod%2Fmodule.json&query=%24.version&prefix=v&label=module&color=0F766E)](PaperMC.mod/module.json)
[![Requires WindowsGSH](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FWindowsGSH%2FWindowsGSH.PaperMC%2Fmain%2FPaperMC.mod%2Fmodule.json%3Fbadge%3Dminimum&query=%24.minimumWindowsGshVersion&prefix=v&label=requires%20WindowsGSH&color=2563EB)](PaperMC.mod/module.json)
[![Licence](https://img.shields.io/badge/licence-MIT-64748B)](LICENSE.md)

This WindowsGSH module downloads, configures, launches, updates, supervises and backs up Paper Minecraft servers.

## Status

**BETA CANDIDATE.** Live startup and PID attachment are proven. Readiness Check validates the server JAR, working Java runtime, and recorded Minecraft EULA acceptance. Update, remote administration, backup/restore and application-restart reattachment still require end-to-end beta testing.

## Installation

Paper is downloaded from PaperMC's official downloads service when WindowsGSH runs Install or Update; it is not installed through SteamCMD and is not bundled with this module.

1. Import `PaperMC.mod` through WindowsGSH Module Management.
2. Create a PaperMC server and choose the Minecraft version and Paper build (`latest` selects the latest stable build).
3. Configure a compatible 64-bit Java runtime in WindowsGSH.
4. Accept Mojang's Minecraft EULA only after reading and agreeing to it.
5. Run Install/Update, then start the server.

### Import an existing server

Select either a normal Paper server folder containing a Paper JAR or a WindowsGSM server folder whose `serverfiles` directory contains one. WindowsGSH previews supported `server.properties` values and can copy the installation or adopt it in place. Review missing/defaulted values before completing the import.

## Configuration

The module manages `server.properties` for the server port, world name, MOTD, online mode, player limit, RCON and Minecraft Query settings. It also writes `eula.txt` from the explicit EULA choice. Unmanaged properties are preserved.

Java runtime selection, minimum/maximum memory and JVM arguments use WindowsGSH's shared per-server Java settings. Paper versions through Minecraft `1.21.11` require Java 21; Paper `26.1` and newer require Java 25. The module validates that distinction before launch.

## Networking

| Purpose | Default | Protocol | Exposure |
|---|---:|---|---|
| Player/game connection | 25565 | TCP | Public when hosting remote players; eligible for manual forwarding or UPnP |
| Minecraft Query | 25565 | UDP | Optional; private/no automatic forwarding by default |
| RCON administration | 25575 | TCP | Optional administration endpoint; private/no automatic forwarding |

The host port model currently lists the optional Query and RCON endpoints even when their feature switches are off. They are non-required, excluded from automatic forwarding and do not trigger listener warnings. Never expose RCON directly to the internet; use a firewall-restricted management network, VPN or equivalent protected path.

## Query, console, and administration

The server card currently uses process status and does not implement Minecraft status/query player counts. `query.enabled` only configures Minecraft's GameSpy4-compatible Query service for an external compatible client.

WindowsGSH sends embedded-console and scheduled commands through the tracked Java process's standard input. Normal Stop sends `stop` so Paper can save cleanly. When enabled, WindowsGSH can send Source-style RCON commands to the configured local endpoint; keep the password secret and the port private.

## Files and backups

The managed server JAR and `server.properties`, `eula.txt`, logs and world data live in the selected installation directory. Backup targets include the default Overworld, Nether and End folders, properties/EULA, operator/allow/ban lists, plugins and configuration directories.

Custom world paths or plugin-owned data outside those targets require separate backup coverage. Stop the server before restoring and test a full restore before relying on the archive.

## Known limitations

- Player counts are not available because WindowsGSH does not yet implement Minecraft status/query as a card provider.
- Optional ports cannot currently disappear conditionally from the host's port list; safe non-public declarations are used instead.
- Java compatibility is inferred from the configured Minecraft version and should be rechecked when Paper changes its supported runtime matrix.
- Verify Files is represented by downloading the selected stable Paper build again rather than Steam validation.

## Beta verification checklist

- [ ] Install and update a current Paper build using the official downloads service and confirm the selected JAR is retained correctly.
- [ ] Save settings, confirm managed `server.properties` values change and unrelated properties survive.
- [ ] Start, attach the Java PID/card, restart WindowsGSH while it runs, execute console commands and stop gracefully.
- [ ] Test local/remote joining, optional Minecraft Query, RCON from a protected client and UPnP/manual-forwarding behavior.
- [ ] Exercise crash detection, world/plugin backup, restore and update with an existing world.
- [ ] Import both a direct Paper folder and a WindowsGSM `serverfiles` layout in Copy and Adopt modes.

## Support

Report module problems through the [WindowsGSH.PaperMC issue tracker](https://github.com/WindowsGSH/WindowsGSH.PaperMC/issues). Include the module and WindowsGSH versions, Minecraft/Paper and Java versions, the operation performed and sanitized logs. Never publish RCON passwords, player addresses or unredacted configuration/support archives.

## Support development

If you like the work I do and would like to support continued WindowsGSH and module development, you can contribute here:

- [Ko-fi](https://ko-fi.com/shenniko)
- [PayPal](https://paypal.me/shenniko)

## Trust and source

Modules execute with the same Windows permissions as WindowsGSH. Download releases only from a source you trust and review `PaperMC.mod/module.json`, `PaperMcModule.cs` and `PaperDownloadClient.cs` before loading modified packages. See [SECURITY.md](SECURITY.md) for safe reporting and credential guidance. Paper itself is downloaded from PaperMC and remains governed by its own terms.
