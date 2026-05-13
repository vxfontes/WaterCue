using System.Windows;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace WaterCueWindows.Views;

public partial class StatsWindow : Window
{
    public StatsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshStats();
    }

    private void RefreshStats()
    {
        var state = AppState.Shared;

        TodayLabel.Text = $"{state.CupsToday} / {state.Settings.DailyGoal}";
        TodayLabel.Foreground = state.CupsToday >= state.Settings.DailyGoal
            ? (Brush)FindResource("SuccessBrush")
            : (Brush)FindResource("TextPrimaryBrush");

        StreakLabel.Text = $"{state.CurrentStreak} dias";
        StreakLabel.Foreground = state.CurrentStreak > 0
            ? (Brush)FindResource("WarningBrush")
            : (Brush)FindResource("TextSecondaryBrush");

        BuildChart(state);
    }

    private void BuildChart(AppState state)
    {
        var weekly = LoadWeeklyCounts();
        if (weekly.Count == 0)
        {
            WeeklyChart.Visibility = Visibility.Collapsed;
            return;
        }

        var model = new PlotModel
        {
            Background = OxyColors.Transparent,
            PlotAreaBackground = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(0x5E, 0x65, 0x73),
            PlotAreaBorderThickness = new OxyThickness(0)
        };

        var categoryAxis = new CategoryAxis
        {
            Position = AxisPosition.Left,
            TextColor = OxyColor.FromRgb(0x5E, 0x65, 0x73),
            TicklineColor = OxyColors.Transparent,
            MajorGridlineColor = OxyColors.Transparent,
            MinorGridlineColor = OxyColors.Transparent,
            FontSize = 11,
            GapWidth = 0.55
        };

        var valueAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            TextColor = OxyColor.FromRgb(0x8A, 0x93, 0xA4),
            TicklineColor = OxyColors.Transparent,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromArgb(0x18, 0x5E, 0x65, 0x73),
            MinorGridlineColor = OxyColors.Transparent,
            Minimum = 0,
            IsAxisVisible = false
        };

        var series = new BarSeries
        {
            FillColor = OxyColor.FromRgb(0x0F, 0x6C, 0xBD),
            StrokeThickness = 0,
            BarWidth = 0.58
        };

        int dailyGoal = state.Settings.DailyGoal;
        foreach (var (day, count) in weekly)
        {
            categoryAxis.Labels.Add(FormatDay(day));
            series.Items.Add(new BarItem
            {
                Value = count,
                Color = count >= dailyGoal
                    ? OxyColor.FromRgb(0x0F, 0x6C, 0xBD)
                    : OxyColor.FromArgb(0x66, 0x0F, 0x6C, 0xBD)
            });
        }

        model.Axes.Add(categoryAxis);
        model.Axes.Add(valueAxis);
        model.Series.Add(series);

        WeeklyChart.Model = model;
        WeeklyChart.Visibility = Visibility.Visible;
    }

    private static List<(string day, int count)> LoadWeeklyCounts()
    {
        try
        {
            return Services.DatabaseService.Shared.WeeklyCounts()
                .Select(d => (d.Day, d.Count))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string FormatDay(string day)
    {
        if (DateTime.TryParse(day, out var date))
        {
            var formatter = new System.Globalization.CultureInfo("pt-BR");
            return date.ToString("ddd", formatter);
        }

        return day[^2..];
    }
}
