using WindowsGSH.Core.Java;
using WindowsGSH.Core.Modules;
using WindowsGSH.Core.Servers;
using WindowsGSH.Modules.PaperMC;
using Xunit;

namespace WindowsGSH.Modules.PaperMC.Tests;

public sealed class PaperMcLaunchTests
{
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
}
