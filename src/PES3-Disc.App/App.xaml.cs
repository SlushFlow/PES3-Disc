using System.Windows;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Services = AppServices.Load();
        Services.Initialize();

        if (!Services.Config.SetupComplete || !Services.Config.IsRpcs3Configured)
        {
            var setup = new SetupWindow();
            if (setup.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        var main = new MainWindow();
        main.Show();
    }
}

public sealed class AppServices
{
    public Pes3Config Config { get; private set; } = new();
    public Pes3Paths Paths { get; private set; } = null!;
    public Rpcs3Launcher Launcher { get; private set; } = null!;
    public DiscDecryptor Decryptor { get; } = new();
    public PromptedStore Prompted { get; private set; } = null!;
    public string ConfigPath { get; private set; } = "";

    public static AppServices Load()
    {
        var configPath = Pes3Config.GetDefaultConfigPath();
        var config = Pes3Config.Load(configPath);
        var paths = new Pes3Paths(config);
        return new AppServices
        {
            Config = config,
            ConfigPath = configPath,
            Paths = paths,
            Launcher = new Rpcs3Launcher(config, paths),
            Prompted = new PromptedStore(paths),
        };
    }

    public void Initialize()
    {
        Paths.EnsurePes3Folders();
        Pes3Log.SetPath(Paths.LogPath);
    }

    public void SaveConfig()
    {
        Config.Save(ConfigPath);
        Paths = new Pes3Paths(Config);
        Launcher = new Rpcs3Launcher(Config, Paths);
        Prompted = new PromptedStore(Paths);
        Initialize();
    }
}
