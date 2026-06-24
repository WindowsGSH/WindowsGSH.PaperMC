using WindowsGSH.Core.Java;
using WindowsGSH.Core.Modules;
using WindowsGSH.Core.Servers;
using WindowsGSH.Modules.PaperMC;
using Xunit;

namespace WindowsGSH.Modules.PaperMC.Tests;

public sealed class PaperMcLaunchTests
{
    [Fact]
    public async Task Existing_server_import_detects_paper_jar_and_properties()
    {
        var root = Path.Combine(Path.GetTempPath(), "WindowsGSH.PaperMC.Tests", Guid.NewGuid().ToString("N"));
        var installPath = Path.Combine(root, "files");
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "paper-1.21.11-123.jar"), "jar");
        File.WriteAllText(Path.Combine(installPath, "server.properties"), """
server-port=25566
motd=Imported Paper
max-players=42
enable-rcon=true
rcon.port=25576
rcon.password=secret
enable-query=true
query.port=25566
""");
        try
        {
            var module = CreateConfiguredModule();

            Assert.True(module.CanImport(installPath));
            var preview = await module.PreviewImportAsync(installPath, CancellationToken.None);

            Assert.Equal(installPath, preview.InstallPath);
            Assert.Equal("Imported Paper", preview.SourceName);
            Assert.Equal("paper-1.21.11-123.jar", preview.Settings["server.jar"]);
            Assert.Equal(25566, preview.Settings["server.port"]);
            Assert.Equal(42, preview.Settings["server.maxPlayers"]);
            Assert.Empty(preview.Warnings);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Existing_server_import_accepts_windowsgsm_serverfiles_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "WindowsGSH.PaperMC.Tests", Guid.NewGuid().ToString("N"));
        var serverFiles = Path.Combine(root, "serverfiles");
        Directory.CreateDirectory(serverFiles);
        File.WriteAllText(Path.Combine(serverFiles, "paper.jar"), "jar");
        try
        {
            var module = CreateConfiguredModule();

            Assert.True(module.CanImport(root));
            var preview = await module.PreviewImportAsync(root, CancellationToken.None);

            Assert.Equal(serverFiles, preview.InstallPath);
            Assert.Equal("paper.jar", preview.Settings["server.jar"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateStartInfo_uses_shared_java_runtime_memory_and_additional_arguments()
    {
        var root = Path.Combine(Path.GetTempPath(), "WindowsGSH.PaperMC.Tests", Guid.NewGuid().ToString("N"));
        var installPath = Path.Combine(root, "files");
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "paper.jar"), "jar");
        try
        {
            var javaPath = @"C:\Java21\bin\java.exe";
            var module = new PaperMcModule(new JavaRuntimeLocator(
                fileExists: path => path == javaPath,
                getEnvironmentVariable: _ => null,
                runVersionCommand: _ => "openjdk version \"21.0.5\""));
            var settings = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["eula.accepted"] = true,
                ["minecraft.version"] = "1.21.11",
                ["server.jar"] = "paper.jar"
            };
            var java = ServerJavaSettings.Default with
            {
                RuntimePath = javaPath,
                InitialMemoryMb = 4096,
                MaximumMemoryMb = 8192,
                AdditionalJvmArguments = "-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions"
            };
            var instance = new ServerInstance(
                "paper-1",
                "Paper",
                "papermc",
                root,
                installPath,
                Path.Combine(root, "ServerConfig.json"),
                settings,
                ServerConfigAppSettings.Empty with { Java = java },
                new ServerModuleSettings(settings));

            var startInfo = await module.CreateStartInfoAsync(instance, CancellationToken.None);

            Assert.Equal(javaPath, startInfo.FileName);
            Assert.Contains("-Xms4096M -Xmx8192M", startInfo.Arguments);
            Assert.Contains("-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions", startInfo.Arguments);
            Assert.Contains("-jar", startInfo.Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PaperMcModule CreateConfiguredModule()
    {
        var moduleDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "PaperMC.mod"));
        var manifest = ModuleManifest.Read(Path.Combine(moduleDirectory, "module.json"));
        var module = new PaperMcModule();
        module.Configure(manifest, moduleDirectory);
        return module;
    }
}
