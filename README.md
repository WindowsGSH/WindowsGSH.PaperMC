# PaperMC

WindowsGSH module for PaperMC Minecraft servers.

## Module Layout

```text
WindowsGSH.PaperMC/
  README.md
  LICENSE.md
  PaperMC.mod/
    module.json
    PaperMcModule.cs
    PaperDownloadClient.cs
    author.png
```

Import `PaperMC.mod` directly, or import the repository root and let WindowsGSH discover the nested module folder.

## Current Status

- Downloads Paper through PaperMC's downloads service when WindowsGSH runs Update.
- WindowsGSH does not bundle Paper.
- Requires explicit Minecraft EULA acceptance through `eula.accepted`.
- Builds a Java start command for the configured Paper jar.
- Sends console commands through the running server stdin.
- Stops gracefully by sending `stop`.
- Can write RCON settings to `server.properties`.
- Backs up worlds, properties, EULA, operator/ban/whitelist files, plugins, and config.
- Supports WindowsGSH existing-server import and WindowsGSM-style `serverfiles` imports.

## Quick Start

1. Import the module in WindowsGSH Module Management.
2. Create a new PaperMC server.
3. Install Java 21+ for current Paper builds.
4. Set `minecraft.version` and leave `paper.build` as `latest` for the latest stable build.
5. Tick `Accept Minecraft EULA` only if you accept Mojang's Minecraft EULA.
6. Press Install or Update to download Paper.
7. Start the server.

## Important Settings

- `minecraft.version`: Minecraft version to download Paper for.
- `paper.build`: Paper build number, or `latest`.
- Java runtime, memory, and additional JVM arguments use WindowsGSH's shared per-server Java settings.
- `server.jar`: jar file name, normally `paper.jar`.
- `server.port`: Minecraft server port.
- `server.levelName`: world folder name.
- `server.motd`: message of the day.
- `eula.accepted`: confirms you accept Mojang's Minecraft EULA.
- `server.additionalServerArgs`: optional extra server arguments.

## Console, Query, And RCON

- Scheduled console commands are available from the Schedules tab.
- Useful console commands include `say`, `save-all`, `list`, `whitelist reload`, and `stop`.
- RCON settings are written to `server.properties`.
- When `rcon.enabled` is true, WindowsGSH can send manual and scheduled RCON commands through the configured local RCON port.
- Query can be enabled through `query.enabled` and `query.port`.

## Existing Server Import

Choose **Import Existing** and select either:

- a PaperMC server folder containing a Paper jar; or
- a WindowsGSM server folder containing `serverfiles` with a Paper jar.

WindowsGSH detects `paper.jar`, `paper*.jar`, or another jar in the selected install folder and previews values from `server.properties` when present.

## Trust Note

C# modules run code on the user's machine. WindowsGSH does not create, own, review, sign, or guarantee third-party modules.
