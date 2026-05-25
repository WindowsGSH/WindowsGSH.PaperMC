using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WindowsGSH.Core.Config;
using WindowsGSH.Core.Java;
using WindowsGSH.Core.Modules;
using WindowsGSH.Core.Rcon;
using WindowsGSH.Core.Servers;

namespace WindowsGSH.Modules.PaperMC;

public sealed partial class PaperMcModule : IGameServerModule, IManifestBackedModule, IModuleConsoleCommandCapability, IModuleUpdateCapability
{
    private static readonly Regex MemoryPattern = new("^[0-9]+[GgMm]$", RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, string> PropertiesMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["server.port"] = "server-port",
        ["server.levelName"] = "level-name",
        ["server.motd"] = "motd",
        ["server.onlineMode"] = "online-mode",
        ["server.maxPlayers"] = "max-players",
        ["rcon.enabled"] = "enable-rcon",
        ["rcon.port"] = "rcon.port",
        ["rcon.password"] = "rcon.password",
        ["query.enabled"] = "enable-query",
        ["query.port"] = "query.port"
    };

    private ModuleManifest? _manifest;
    private string _moduleDirectory = AppContext.BaseDirectory;

    private ModuleManifest Manifest => _manifest ??= ModuleManifest.Load(Path.Combine(_moduleDirectory, "module.json"));

    public string Id => Manifest.Id;

    public string Name => Manifest.Name;

    public string Version => Manifest.Version;

    public ModuleCapabilities Capabilities => Manifest.ToCapabilities(supportsQuery: false, supportsRcon: true) with { SupportsUpdate = true };

    public SteamInstallDefinition? SteamInstall => null;

    public ModuleRuntimeDefinition Runtime => Manifest.ToRuntime();

    public void Configure(ModuleManifest manifest, string moduleDirectory)
    {
        _manifest = manifest;
        _moduleDirectory = moduleDirectory;
    }

    public IReadOnlyList<ConfigFieldDefinition> GetConfigFields() => Manifest.ToConfigFields();

    public IReadOnlyList<ServerAddonDefinition> GetAddonDefinitions() => Manifest.ToAddons();

    public IReadOnlyList<ServerBackupTargetDefinition> GetBackupTargets() => Manifest.ToBackupTargets();

    public ServerAddonStatus GetAddonStatus(ServerInstance instance, string addonId)
    {
        return new ServerAddonStatus(addonId, IsInstalled: false, IsEnabled: false, StatusText: "No PaperMC addons defined.");
    }

    public Task InstallAddonAsync(ServerInstance instance, string addonId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("PaperMC module does not define addons yet.");
    }

    public Task RemoveAddonAsync(ServerInstance instance, string addonId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("PaperMC module does not define addons yet.");
    }

    public string GetServerName(IReadOnlyDictionary<string, object?> settings)
    {
        return GetSetting(settings, "server.name", "PaperMC Server");
    }

    public ServerDisplayInfo GetDisplayInfo(ServerInstance instance)
    {
        return new ServerDisplayInfo(
            IpAddress: "0.0.0.0",
            Port: GetSetting(instance, "server.port", "25565"),
            MaxPlayers: GetSetting(instance, "server.maxPlayers", "20"));
    }

    public Task<IReadOnlyDictionary<string, object?>> ReadConfigFileSettingsAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        var path = GetPropertiesPath(instance);
        if (!File.Exists(path))
        {
            return Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>());
        }

        var properties = PropertiesFile.Load(path);
        var settings = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["server.port"] = properties.GetInt32("server-port", 25565),
            ["server.levelName"] = properties.GetString("level-name", "world"),
            ["server.motd"] = properties.GetString("motd", "A PaperMC Server"),
            ["server.onlineMode"] = properties.GetBoolean("online-mode", true),
            ["server.maxPlayers"] = properties.GetInt32("max-players", 20),
            ["rcon.enabled"] = properties.GetBoolean("enable-rcon"),
            ["rcon.port"] = properties.GetInt32("rcon.port", 25575),
            ["rcon.password"] = properties.GetString("rcon.password"),
            ["query.enabled"] = properties.GetBoolean("enable-query"),
            ["query.port"] = properties.GetInt32("query.port", 25565)
        };

        return Task.FromResult<IReadOnlyDictionary<string, object?>>(settings);
    }

    public Task WriteConfigFileSettingsAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        WriteServerProperties(instance);
        if (ReadBool(instance, "eula.accepted", defaultValue: false))
        {
            File.WriteAllText(Path.Combine(instance.InstallPath, "eula.txt"), "eula=true" + Environment.NewLine);
        }

        return Task.CompletedTask;
    }

    public Task<InstallPlan> CreateInstallPlanAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("PaperMC downloads are not implemented yet. Add paper.jar to the server files folder manually for now.");
    }

    public async Task<ModuleUpdateResult> UpdateAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        var version = GetSetting(instance, "minecraft.version", string.Empty);
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("Minecraft Version is required before downloading PaperMC.");
        }

        using var client = new PaperDownloadClient();
        var requestedBuild = GetSetting(instance, "paper.build", "latest");
        var build = await client.ResolveBuildAsync(version, requestedBuild, cancellationToken);
        var fileName = $"paper-{build.Version}-{build.BuildId}.jar";
        var targetPath = Path.Combine(instance.InstallPath, fileName);
        Directory.CreateDirectory(instance.InstallPath);
        await client.DownloadAsync(build.DownloadUrl, targetPath, cancellationToken);
        UpdateServerJarSetting(instance.ConfigPath, fileName);

        return new ModuleUpdateResult(
            Updated: true,
            Message: $"PaperMC {build.Version} build {build.BuildId} downloaded as {fileName}.",
            InstalledBuildId: build.BuildId,
            DownloadedFileName: fileName);
    }

    public Task<ProcessStartInfo> CreateStartInfoAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        ValidateStart(instance, out var java, out var jarPath);
        WriteConfigFileSettingsAsync(instance, cancellationToken).GetAwaiter().GetResult();

        var startInfo = new ProcessStartInfo
        {
            FileName = java.ExecutablePath,
            WorkingDirectory = instance.InstallPath,
            Arguments = BuildArguments(instance, jarPath),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        return Task.FromResult(startInfo);
    }

    public async Task<Process?> StartAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = await CreateStartInfoAsync(instance, cancellationToken),
            EnableRaisingEvents = true
        };

        process.Start();
        return process;
    }

    public Task<IReadOnlyList<Process>> StartAddonProcessesAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Process>>([]);
    }

    public async Task StopAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        try
        {
            ServerConsoleService.Add(instance.Id, "> stop");
            ServerConsoleService.SendCommand(instance.Id, "stop");
        }
        catch (Exception ex)
        {
            ServerConsoleService.Add(instance.Id, "Graceful stop via console failed: " + ex.Message);
        }

        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        foreach (var process in ServerProcessLocator.FindProcesses(this, instance.InstallPath))
        {
            using (process)
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }

                if (!process.HasExited)
                {
                    ServerConsoleService.Add(instance.Id, $"PaperMC process {process.Id} did not exit after stop; forcing it closed.");
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken);
                }
            }
        }
    }

    public bool IsInstallValid(ServerInstance instance)
    {
        return File.Exists(ResolveJarPath(instance));
    }

    public string? GetConsoleLogPath(ServerInstance instance)
    {
        return Path.Combine(instance.InstallPath, "logs", "latest.log");
    }

    public Task<string> ExecuteConsoleCommandAsync(ServerInstance instance, string command, CancellationToken cancellationToken)
    {
        ServerConsoleService.SendCommand(instance.Id, command);
        return Task.FromResult("Console command sent.");
    }

    public async Task<string> ExecuteRconCommandAsync(ServerInstance instance, string command, CancellationToken cancellationToken)
    {
        if (!ReadBool(instance, "rcon.enabled", defaultValue: false))
        {
            throw new InvalidOperationException("Enable RCON in this server's PaperMC settings before sending RCON commands.");
        }

        var password = GetSetting(instance, "rcon.password", string.Empty);
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Set an RCON password before sending RCON commands.");
        }

        var portText = GetSetting(instance, "rcon.port", "25575");
        if (!int.TryParse(portText, out var port) || port < 1 || port > 65535)
        {
            throw new InvalidOperationException("RCON Port must be between 1 and 65535.");
        }

        var client = new SourceRconClient();
        return await client.ExecuteAsync("127.0.0.1", port, password, command, cancellationToken);
    }

    public Task<QueryResult> QueryAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        var status = ServerProcessLocator.IsRunning(this, instance.InstallPath)
            ? ModuleServerStatus.Online
            : ModuleServerStatus.Offline;
        return Task.FromResult(new QueryResult(status, Message: "Process status only."));
    }

    private static void ValidateStart(ServerInstance instance, out JavaRuntimeInfo java, out string jarPath)
    {
        java = new JavaRuntimeLocator().Locate(GetSetting(instance, "java.path", string.Empty));
        jarPath = PaperMcStartValidator.Validate(instance, java).JarPath;
    }

    private static void WriteServerProperties(ServerInstance instance)
    {
        var properties = PropertiesFile.Load(GetPropertiesPath(instance));
        foreach (var (settingKey, propertyKey) in PropertiesMap)
        {
            if (!instance.Settings.TryGetValue(settingKey, out var value) || value == null)
            {
                continue;
            }

            properties.Set(propertyKey, ToPropertyValue(value));
        }

        properties.Save(GetPropertiesPath(instance));
    }

    private static void UpdateServerJarSetting(string configPath, string fileName)
    {
        var root = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject()
            ?? throw new InvalidOperationException("Server config is not a JSON object.");
        var settings = root["settings"] as JsonObject ?? [];
        root["settings"] = settings;
        settings["server.jar"] = fileName;
        File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string GetPropertiesPath(ServerInstance instance)
    {
        return Path.Combine(instance.InstallPath, "server.properties");
    }

    private static string ToPropertyValue(object value)
    {
        return value switch
        {
            bool boolean => boolean ? "true" : "false",
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    private static string BuildArguments(ServerInstance instance, string jarPath)
    {
        return PaperMcLaunchBuilder.Build(new PaperMcLaunchOptions(
            Xms: GetSetting(instance, "java.xms", "1G"),
            Xmx: GetSetting(instance, "java.xmx", "4G"),
            JarPath: jarPath,
            AdditionalJvmArgs: GetSetting(instance, "server.additionalJvmArgs", string.Empty),
            AdditionalServerArgs: GetSetting(instance, "server.additionalServerArgs", string.Empty)));
    }

}

public sealed record PaperMcLaunchOptions(
    string Xms,
    string Xmx,
    string JarPath,
    string AdditionalJvmArgs,
    string AdditionalServerArgs);

public static class PaperMcLaunchBuilder
{
    public static string Build(PaperMcLaunchOptions options)
    {
        var parts = new List<string>
        {
            "-Xms" + options.Xms,
            "-Xmx" + options.Xmx
        };

        AddRaw(parts, options.AdditionalJvmArgs);
        parts.Add("-jar");
        parts.Add(WindowsCommandLineEscaper.Quote(options.JarPath));
        parts.Add("--nogui");
        AddRaw(parts, options.AdditionalServerArgs);
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static void AddRaw(List<string> parts, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
    }
}

public sealed record PaperMcStartValidation(JavaRuntimeInfo Java, string JarPath);

public static class PaperMcStartValidator
{
    private static readonly Regex MemoryPattern = new("^[0-9]+[GgMm]$", RegexOptions.Compiled);

    public static PaperMcStartValidation Validate(ServerInstance instance, JavaRuntimeInfo java)
    {
        if (!ReadBool(instance, "eula.accepted", defaultValue: false))
        {
            throw new InvalidOperationException("Accept Minecraft's EULA before starting this PaperMC server.");
        }

        ValidateMemory(GetSetting(instance, "java.xms", "1G"), "Initial Memory");
        ValidateMemory(GetSetting(instance, "java.xmx", "4G"), "Maximum Memory");

        if (!java.Found)
        {
            throw new InvalidOperationException($"Java was not found. {java.ErrorMessage}");
        }

        var requiredJava = GetRequiredJavaMajor(instance);
        if (!java.MajorVersion.HasValue)
        {
            throw new InvalidOperationException("Java was found, but its version could not be parsed from java -version.");
        }

        if (java.MajorVersion.Value < requiredJava)
        {
            throw new InvalidOperationException($"PaperMC for this Minecraft version requires Java {requiredJava}+.");
        }

        var jarPath = ResolveJarPath(instance);
        if (!File.Exists(jarPath))
        {
            throw new FileNotFoundException("PaperMC server jar was not found.", jarPath);
        }

        return new PaperMcStartValidation(java, jarPath);
    }

    public static string ResolveJarPath(ServerInstance instance)
    {
        var jar = GetSetting(instance, "server.jar", "paper.jar");
        var candidate = Path.IsPathRooted(jar)
            ? Path.GetFullPath(jar)
            : Path.GetFullPath(Path.Combine(instance.InstallPath, jar));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(instance.InstallPath));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Server Jar must stay inside the server files folder.");
        }

        return candidate;
    }

    private static int GetRequiredJavaMajor(ServerInstance instance)
    {
        var minecraftVersion = GetSetting(instance, "minecraft.version", string.Empty);
        return minecraftVersion.StartsWith("26.", StringComparison.OrdinalIgnoreCase) ? 25 : 21;
    }

    private static void ValidateMemory(string value, string label)
    {
        if (!MemoryPattern.IsMatch(value))
        {
            throw new InvalidOperationException($"{label} must look like 1G or 1024M.");
        }
    }

    private static string GetSetting(ServerInstance instance, string key, string fallback)
    {
        return GetSetting(instance.Settings, key, fallback);
    }

    private static string GetSetting(IReadOnlyDictionary<string, object?> settings, string key, string fallback)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())
            ? value.ToString()!.Trim()
            : fallback;
    }

    private static bool ReadBool(ServerInstance instance, string key, bool defaultValue)
    {
        if (!instance.Settings.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value switch
        {
            bool boolean => boolean,
            string text => bool.TryParse(text, out var parsed) ? parsed : defaultValue,
            _ => defaultValue
        };
    }
}

public sealed partial class PaperMcModule
{
    private static string ResolveJarPath(ServerInstance instance)
    {
        return PaperMcStartValidator.ResolveJarPath(instance);
    }

    private static int GetRequiredJavaMajor(ServerInstance instance)
    {
        var minecraftVersion = GetSetting(instance, "minecraft.version", string.Empty);
        return minecraftVersion.StartsWith("26.", StringComparison.OrdinalIgnoreCase) ? 25 : 21;
    }

    private static void ValidateMemory(string value, string label)
    {
        if (!MemoryPattern.IsMatch(value))
        {
            throw new InvalidOperationException($"{label} must look like 1G or 1024M.");
        }
    }

    private static string GetSetting(ServerInstance instance, string key, string fallback)
    {
        return GetSetting(instance.Settings, key, fallback);
    }

    private static string GetSetting(IReadOnlyDictionary<string, object?> settings, string key, string fallback)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())
            ? value.ToString()!.Trim()
            : fallback;
    }

    private static bool ReadBool(ServerInstance instance, string key, bool defaultValue)
    {
        if (!instance.Settings.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value switch
        {
            bool boolean => boolean,
            string text => bool.TryParse(text, out var parsed) ? parsed : defaultValue,
            _ => defaultValue
        };
    }
}
