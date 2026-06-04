using System.Windows;
using PES3Disc.Core;
using PES3Disc.ViewModels;

namespace PES3Disc.App;

public partial class App : Application
{
    public static Pes3Services Services { get; private set; } = null!;
    public static Pes3AppController Controller { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        PerformanceTuning.ApplyRuntimeDefaults();
        Services = Pes3Services.Load();
        Services.Initialize();
        Controller = new Pes3AppController(Services);

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
