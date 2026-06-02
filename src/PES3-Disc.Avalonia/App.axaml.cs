using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PES3Disc.ViewModels;

namespace PES3Disc.Avalonia;

public partial class App : Application
{
    public static Pes3Services Services { get; private set; } = null!;
    public static Pes3AppController Controller { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = Pes3Services.Load();
        Services.Initialize();
        Controller = new Pes3AppController(Services);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!Services.Config.SetupComplete || !Services.Config.IsRpcs3Configured)
            {
                var setup = new SetupWindow();
                desktop.MainWindow = setup;
                setup.Closed += (_, _) =>
                {
                    if (!Services.Config.IsRpcs3Configured)
                        desktop.Shutdown();
                    else
                        desktop.MainWindow = new MainWindow();
                };
            }
            else
            {
                desktop.MainWindow = new MainWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
