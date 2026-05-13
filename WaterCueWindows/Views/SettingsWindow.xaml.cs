using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WaterCueWindows;
using WaterCueWindows.Models;
using WaterCueWindows.Services;

namespace WaterCueWindows.Views;

public partial class SettingsWindow : Window
{
    private HydrationSettings _draft;
    private string _apiKey = string.Empty;

    private TextBlock? _intervalValueLabel;
    private TextBlock? _warningValueLabel;
    private TextBlock? _emergencyValueLabel;
    private TextBlock? _dailyGoalValueLabel;
    private ComboBox? _cameraCombo;
    private CheckBox? _warnToggle;
    private CheckBox? _autostartToggle;
    private PasswordBox? _apiKeyBox;
    private TextBlock? _apiKeySavedLabel;

    public SettingsWindow()
    {
        _draft = HydrationSettings.Load();
        InitializeComponent();
        BuildSections();
        LoadCurrentApiKey();
    }

    private void BuildSections()
    {
        SettingsPanel.Children.Add(Section("Intervalo", BuildIntervalSection()));
        SettingsPanel.Children.Add(Section("Câmera", BuildCameraSection()));
        SettingsPanel.Children.Add(Section("Groq API", BuildApiSection()));
        SettingsPanel.Children.Add(Section("Sistema", BuildSystemSection()));
        SettingsPanel.Children.Add(Section("Avançado", BuildAdvancedSection()));
    }

    private static Border Section(string title, UIElement content)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(content);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x11, 0x1D, 0x33)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(0),
            Child = panel,
            Margin = new Thickness(0, 0, 0, 12)
        };
    }

    private UIElement BuildIntervalSection()
    {
        var panel = new StackPanel { Margin = new Thickness(16) };

        _warnToggle = new CheckBox
        {
            Content = new TextBlock { Text = "Aviso antes de travar", Foreground = Brushes.White, FontSize = 13 },
            IsChecked = _draft.WarnBeforeLock,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = Brushes.White
        };
        _warnToggle.Checked += (_, _) => _draft.WarnBeforeLock = true;
        _warnToggle.Unchecked += (_, _) => _draft.WarnBeforeLock = false;

        panel.Children.Add(MakeLabeledSlider("🔒  Travar a cada",
            15, 240, 15, _draft.IntervalSeconds / 60.0,
            val => FormatInterval((int)val),
            val => _draft.IntervalSeconds = (int)val * 60,
            out _intervalValueLabel));

        panel.Children.Add(_warnToggle);

        panel.Children.Add(MakeLabeledSlider("🔔  Aviso com antecedência",
            1, 10, 1, _draft.WarningSeconds / 60.0,
            val => $"{(int)val} min",
            val => _draft.WarningSeconds = (int)val * 60,
            out _warningValueLabel));

        panel.Children.Add(MakeLabeledSlider("⚠  Emergência após",
            10, 300, 10, _draft.EmergencyDelaySeconds,
            FormatSeconds,
            val => _draft.EmergencyDelaySeconds = (int)val,
            out _emergencyValueLabel));

        panel.Children.Add(MakeLabeledSlider("💧  Meta diária",
            4, 16, 1, _draft.DailyGoal,
            val => $"{(int)val} copos",
            val => _draft.DailyGoal = (int)val,
            out _dailyGoalValueLabel));

        return panel;
    }

    private UIElement BuildCameraSection()
    {
        var panel = new StackPanel { Margin = new Thickness(16) };

        _cameraCombo = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x29, 0x40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x6B, 0xE4)),
            BorderThickness = new Thickness(1)
        };

        _cameraCombo.Items.Add(new ComboBoxItem { Content = "Automático", Tag = string.Empty });

        foreach (var cam in CameraService.Shared.AvailableCameras())
            _cameraCombo.Items.Add(new ComboBoxItem { Content = cam.Name, Tag = cam.MonikerString });

        _cameraCombo.SelectedIndex = 0;
        for (int i = 0; i < _cameraCombo.Items.Count; i++)
        {
            if (((ComboBoxItem)_cameraCombo.Items[i]).Tag is string tag && tag == _draft.CameraDeviceId)
            { _cameraCombo.SelectedIndex = i; break; }
        }

        _cameraCombo.SelectionChanged += (_, _) =>
        {
            if (_cameraCombo.SelectedItem is ComboBoxItem item)
                _draft.CameraDeviceId = item.Tag as string ?? string.Empty;
        };

        panel.Children.Add(new TextBlock { Text = "Câmera", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF)), Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(_cameraCombo);

        return panel;
    }

    private UIElement BuildApiSection()
    {
        var panel = new StackPanel { Margin = new Thickness(16) };

        _apiKeyBox = new PasswordBox
        {
            Height = 36,
            FontSize = 13,
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x29, 0x40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x6B, 0xE4)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 8)
        };

        _apiKeySavedLabel = new TextBlock
        {
            Text = "✅  Chave salva com sucesso!",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0xC0, 0x60)),
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var saveKeyBtn = new Button
        {
            Content = new TextBlock { Text = "Salvar chave", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xB7, 0xF5)) },
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0xB7, 0xF5)),
            BorderThickness = new Thickness(1),
            Height = 30,
            Padding = new Thickness(12, 0, 12, 0)
        };
        saveKeyBtn.Click += (_, _) => SaveApiKeyNow();

        var modelCombo = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 12, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x29, 0x40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x6B, 0xE4)),
            BorderThickness = new Thickness(1)
        };
        var models = new[]
        {
            ("meta-llama/llama-4-scout-17b-16e-instruct", "Llama 4 Scout (rápido)"),
            ("meta-llama/llama-4-maverick-17b-128e-instruct", "Llama 4 Maverick (preciso)"),
            ("llama-3.2-90b-vision-preview", "Llama 3.2 90B Vision"),
            ("llama-3.2-11b-vision-preview", "Llama 3.2 11B Vision (econômico)")
        };
        foreach (var (id, label) in models)
            modelCombo.Items.Add(new ComboBoxItem { Content = label, Tag = id });

        modelCombo.SelectedIndex = 0;
        for (int i = 0; i < modelCombo.Items.Count; i++)
        {
            if (((ComboBoxItem)modelCombo.Items[i]).Tag is string tag && tag == _draft.GroqModel)
            { modelCombo.SelectedIndex = i; break; }
        }
        modelCombo.SelectionChanged += (_, _) =>
        {
            if (modelCombo.SelectedItem is ComboBoxItem item)
                _draft.GroqModel = item.Tag as string ?? _draft.GroqModel;
        };

        panel.Children.Add(new TextBlock { Text = "Groq API Key (gsk_...)", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF)), Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(_apiKeyBox);
        panel.Children.Add(_apiKeySavedLabel);
        panel.Children.Add(saveKeyBtn);
        panel.Children.Add(new TextBlock { Text = "Modelo", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF)), Margin = new Thickness(0, 12, 0, 6) });
        panel.Children.Add(modelCombo);

        return panel;
    }

    private UIElement BuildSystemSection()
    {
        var panel = new StackPanel { Margin = new Thickness(16) };

        _autostartToggle = new CheckBox
        {
            Content = new TextBlock { Text = "Iniciar com o Windows", Foreground = Brushes.White, FontSize = 13 },
            IsChecked = AutostartService.Shared.IsRegistered,
            Foreground = Brushes.White
        };

        panel.Children.Add(_autostartToggle);
        return panel;
    }

    private UIElement BuildAdvancedSection()
    {
        var panel = new StackPanel { Margin = new Thickness(16) };

        var resetBtn = new Button
        {
            Content = new TextBlock { Text = "Refazer onboarding", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xA0, 0x20)) },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        resetBtn.Click += (_, _) =>
        {
            var flag = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WaterCue", ".onboarding_complete");
            if (System.IO.File.Exists(flag)) System.IO.File.Delete(flag);
            Close();
            new OnboardingWindow().Show();
        };

        panel.Children.Add(resetBtn);
        return panel;
    }

    // ─────────────────────────── Slider helper ─────────────────────────────

    private static StackPanel MakeLabeledSlider(
        string title,
        double min, double max, double tick,
        double initialValue,
        Func<double, string> format,
        Action<double> onChange,
        out TextBlock valueLabel)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleLabel = new TextBlock { Text = title, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xFF)) };
        var vLabel = new TextBlock { Text = format(initialValue), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xB7, 0xF5)), FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(titleLabel, 0); Grid.SetColumn(vLabel, 1);
        row.Children.Add(titleLabel); row.Children.Add(vLabel);
        panel.Children.Add(row);

        var slider = new Slider
        {
            Minimum = min, Maximum = max,
            TickFrequency = tick, IsSnapToTickEnabled = true,
            Value = initialValue,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xB7, 0xF5))
        };
        slider.ValueChanged += (_, e) =>
        {
            vLabel.Text = format(e.NewValue);
            onChange(e.NewValue);
        };
        panel.Children.Add(slider);

        valueLabel = vLabel;
        return panel;
    }

    // ─────────────────────────── Actions ───────────────────────────────────

    private void LoadCurrentApiKey()
    {
        var key = SettingsService.Shared.LoadApiKey();
        if (_apiKeyBox != null && !string.IsNullOrEmpty(key))
            _apiKeyBox.Password = key;
    }

    private void SaveApiKeyNow()
    {
        if (_apiKeyBox == null) return;
        var key = _apiKeyBox.Password.Trim();
        if (key.Length < 8) return;

        SettingsService.Shared.SaveApiKey(key);
        if (_apiKeySavedLabel != null)
        {
            _apiKeySavedLabel.Visibility = Visibility.Visible;
            Task.Delay(2000).ContinueWith(_ =>
                Dispatcher.Invoke(() => { if (_apiKeySavedLabel != null) _apiKeySavedLabel.Visibility = Visibility.Collapsed; }));
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_autostartToggle != null)
        {
            bool launch = _autostartToggle.IsChecked == true;
            if (launch) AutostartService.Shared.Register();
            else AutostartService.Shared.Unregister();
            _draft.LaunchAtLogin = launch;
        }

        _draft.Save();
        AppState.Shared.Settings = _draft;
        TimerService.Shared.Schedule(_draft);

        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static string FormatInterval(int minutes)
    {
        if (minutes < 60) return $"{minutes} min";
        int h = minutes / 60, m = minutes % 60;
        return m == 0 ? $"{h}h" : $"{h}h{m}min";
    }

    private static string FormatSeconds(double s)
    {
        int sec = (int)s;
        if (sec < 60) return $"{sec}s";
        int m = sec / 60, rem = sec % 60;
        return rem == 0 ? $"{m}min" : $"{m}min{rem}s";
    }
}
