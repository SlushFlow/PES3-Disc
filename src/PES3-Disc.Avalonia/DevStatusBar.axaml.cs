using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using PES3Disc.BugReports;

namespace PES3Disc.Avalonia;

public partial class DevStatusBar : UserControl
{
    public DevStatusBar() => InitializeComponent();

    public void Apply(DevStatusResponse? status)
    {
        if (status is null || !DevStatusKindExtensions.TryParse(status.Effective, out var kind))
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        StatusDot.Fill = Brush.Parse(DevStatusLogic.GetDotColor(kind));
        StatusLabel.Text = status.Label;
        ToolTip.SetTip(this, status.IsAutoSchedule
            ? $"{status.Label} (automatic schedule, Eastern Time)"
            : $"{status.Label} (set manually by developer)");
    }
}
