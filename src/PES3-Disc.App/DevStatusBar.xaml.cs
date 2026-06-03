using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PES3Disc.BugReports;

namespace PES3Disc.App;

public partial class DevStatusBar : UserControl
{
    public DevStatusBar() => InitializeComponent();

    public void Apply(DevStatusResponse? status)
    {
        if (status is null || !DevStatusKindExtensions.TryParse(status.Effective, out var kind))
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(DevStatusLogic.GetDotColor(kind))!);
        StatusLabel.Text = status.Label;
        ToolTip = status.IsAutoSchedule
            ? $"{status.Label} (automatic schedule, Eastern Time)"
            : $"{status.Label} (set manually by developer)";
    }
}
