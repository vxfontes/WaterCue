using System.Windows;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using WaterCueWindows;
using WaterCueWindows.Services;

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

        TodayLabel.Text = $"{state.CupsToday} / {state.Settings.DailyGoal} copos";
        TodayLabel.Foreground = state.CupsToday >= state.Settings.DailyGoal
            ? new SolidColorBrush(Color.FromRgb(0x30, 0xC0, 0x60))
            : Brushes.White;

        StreakLabel.Text = $"{state.CurrentStreak} dias";
        StreakLabel.Foreground = state.CurrentStreak > 0
            ? new SolidColorBrush(Color.FromRgb(0xE8, 0xA0, 0x20))
            : new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF));

        BuildChart(state);
    }

    private void BuildChart(AppState state)
    {
        var weekly = LoadWeeklyCounts(state);
        if (weekly.Count == 0)
        {
            WeeklyChart.Visibility = Visibility.Collapsed;
            return;
        }

        var model = new PlotModel
        {
            Background = OxyColors.Transparent,
            PlotAreaBackground = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(0x8A, 0x9B, 0xBF),
            PlotAreaBorderThickness = new OxyThickness(0)
        };

        // CategoryAxis on Bottom + LinearAxis on Left = vertical column chart
        var xAxis = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            TextColor = OxyColor.FromRgb(0x8A, 0x9B, 0xBF),
            TicklineColor = OxyColors.Transparent,
            MajorGridlineColor = OxyColors.Transparent,
            MinorGridlineColor = OxyColors.Transparent,
            FontSize = 11,
            GapWidth = 0.4
        };

        var yAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            TextColor = OxyColor.FromRgb(0x8A, 0x9B, 0xBF),
            TicklineColor = OxyColors.Transparent,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromArgb(0x20, 0x8A, 0x9B, 0xBF),
            MinorGridlineColor = OxyColors.Transparent,
            Minimum = 0,
            MajorStep = 2,
            FontSize = 11
        };

        // Use BarSeries + CategoryAxis on Left for horizontal chart
        // and swap orientation for vertical display using CategoryAxis(Bottom)
        var series = new BarSeries
        {
            FillColor = OxyColor.FromRgb(0x27, 0xB7, 0xF5),
            StrokeThickness = 0,
            IsStacked = false
        };

        int dailyGoal = state.Settings.DailyGoal;
        // OxyPlot BarSeries is horizontal — category axis is on Y (Left)
        // Swap: put CategoryAxis on Left for the BarSeries horizontal chart
        xAxis.Position = AxisPosition.Left;
        yAxis.Position = AxisPosition.Bottom;
        yAxis.IsAxisVisible = false;

        foreach (var (day, count) in weekly)
        {
            xAxis.Labels.Add(FormatDay(day));
            series.Items.Add(new BarItem
            {
                Value = count,
                Color = count >= dailyGoal
                    ? OxyColor.FromRgb(0x27, 0xB7, 0xF5)
                    : OxyColor.FromArgb(0x66, 0x27, 0xB7, 0xF5)
            });
        }

        model.Axes.Add(xAxis);
        model.Axes.Add(yAxis);
        model.Series.Add(series);

        WeeklyChart.Model = model;
        WeeklyChart.Visibility = Visibility.Visible;
    }

    private static List<(string day, int count)> LoadWeeklyCounts(AppState state)
    {
        try
        {
            return DatabaseService.Shared.WeeklyCounts()
                .Select(d => (d.Day, d.Count))
                .ToList();
        }
        catch { return []; }
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
