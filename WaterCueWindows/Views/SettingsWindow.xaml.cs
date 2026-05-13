using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WaterCueWindows.Models;
using WaterCueWindows.Services;

namespace WaterCueWindows.Views;

public partial class SettingsWindow : Window
{
    private readonly HydrationSettings _draft;

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
        SettingsPanel.Children.Add(Section("Camera", BuildCameraSection()));
        SettingsPanel.Children.Add(Section("Groq API", BuildApiSection()));
        SettingsPanel.Children.Add(Section("Sistema", BuildSystemSection()));
        SettingsPanel.Children.Add(Section("Avancado", BuildAdvancedSection()));
    }

    private Border Section(string title, UIElement content)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            Style = (Style)FindResource("SectionCaptionTextStyle"),
            Margin = new Thickness(0, 0, 0, 10)
        });
        panel.Children.Add(content);

        return new Border
        {
            Style = (Style)FindResource("CardBorderStyle"),
            Padding = new Thickness(16),
            Child = panel,
            Margin = new Thickness(0, 0, 0, 14)
        };
    }

    private UIElement BuildIntervalSection()
    {
        var panel = new StackPanel();

        panel.Children.Add(MakeLabeledSlider(
            "Bloquear a cada",
            15, 240, 15, _draft.IntervalSeconds / 60.0,
            val => FormatInterval((int)val),
            val => _draft.IntervalSeconds = (int)val * 60));

        _warnToggle = new CheckBox
        {
            Content = "Mostrar aviso antes de travar",
            Style = (Style)FindResource("PanelCheckBoxStyle"),
            IsChecked = _draft.WarnBeforeLock,
            Margin = new Thickness(0, 0, 0, 14)
        };
        _warnToggle.Checked += (_, _) => _draft.WarnBeforeLock = true;
        _warnToggle.Unchecked += (_, _) => _draft.WarnBeforeLock = false;
        panel.Children.Add(_warnToggle);

        panel.Children.Add(MakeLabeledSlider(
            "Avisar com antecedencia",
            1, 10, 1, _draft.WarningSeconds / 60.0,
            val => $"{(int)val} min",
            val => _draft.WarningSeconds = (int)val * 60));

        panel.Children.Add(MakeLabeledSlider(
            "Liberar emergencia apos",
            10, 300, 10, _draft.EmergencyDelaySeconds,
            FormatSeconds,
            val => _draft.EmergencyDelaySeconds = (int)val));

        panel.Children.Add(MakeLabeledSlider(
            "Meta diaria",
            4, 16, 1, _draft.DailyGoal,
            val => $"{(int)val} copos",
            val => _draft.DailyGoal = (int)val));

        return panel;
    }

    private UIElement BuildCameraSection()
    {
        var panel = new StackPanel();

        panel.Children.Add(FieldLabel("Camera usada para a validacao"));

        _cameraCombo = new ComboBox
        {
            Style = (Style)FindResource("InputComboBoxStyle")
        };

        _cameraCombo.Items.Add(new ComboBoxItem { Content = "Automatico", Tag = string.Empty });
        foreach (var cam in CameraService.Shared.AvailableCameras())
        {
            _cameraCombo.Items.Add(new ComboBoxItem { Content = cam.Name, Tag = cam.MonikerString });
        }

        _cameraCombo.SelectedIndex = 0;
        for (int i = 0; i < _cameraCombo.Items.Count; i++)
        {
            if (((ComboBoxItem)_cameraCombo.Items[i]).Tag is string tag && tag == _draft.CameraDeviceId)
            {
                _cameraCombo.SelectedIndex = i;
                break;
            }
        }

        _cameraCombo.SelectionChanged += (_, _) =>
        {
            if (_cameraCombo.SelectedItem is ComboBoxItem item)
            {
                _draft.CameraDeviceId = item.Tag as string ?? string.Empty;
            }
        };

        panel.Children.Add(_cameraCombo);
        panel.Children.Add(new TextBlock
        {
            Text = "No Windows nao precisa de permissao extra dentro do app.",
            Style = (Style)FindResource("BodyTextStyle"),
            Margin = new Thickness(0, 10, 0, 0)
        });

        return panel;
    }

    private UIElement BuildApiSection()
    {
        var panel = new StackPanel();

        panel.Children.Add(FieldLabel("Groq API Key"));

        _apiKeyBox = new PasswordBox
        {
            Style = (Style)FindResource("InputPasswordBoxStyle")
        };
        panel.Children.Add(_apiKeyBox);

        _apiKeySavedLabel = new TextBlock
        {
            Text = "Chave salva com sucesso.",
            Foreground = Brush("SuccessBrush"),
            FontSize = 11,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 8, 0, 0)
        };
        panel.Children.Add(_apiKeySavedLabel);

        var saveKeyBtn = new Button
        {
            Content = "Salvar chave",
            Style = (Style)FindResource("LinkButtonStyle"),
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0)
        };
        saveKeyBtn.Click += (_, _) => SaveApiKeyNow();
        panel.Children.Add(saveKeyBtn);

        panel.Children.Add(FieldLabel("Modelo", new Thickness(0, 16, 0, 6)));

        var modelCombo = new ComboBox
        {
            Style = (Style)FindResource("InputComboBoxStyle")
        };
        var models = new[]
        {
            ("meta-llama/llama-4-scout-17b-16e-instruct", "Llama 4 Scout"),
            ("meta-llama/llama-4-maverick-17b-128e-instruct", "Llama 4 Maverick"),
            ("llama-3.2-90b-vision-preview", "Llama 3.2 90B Vision"),
            ("llama-3.2-11b-vision-preview", "Llama 3.2 11B Vision")
        };
        foreach (var (id, label) in models)
        {
            modelCombo.Items.Add(new ComboBoxItem { Content = label, Tag = id });
        }

        modelCombo.SelectedIndex = 0;
        for (int i = 0; i < modelCombo.Items.Count; i++)
        {
            if (((ComboBoxItem)modelCombo.Items[i]).Tag is string tag && tag == _draft.GroqModel)
            {
                modelCombo.SelectedIndex = i;
                break;
            }
        }
        modelCombo.SelectionChanged += (_, _) =>
        {
            if (modelCombo.SelectedItem is ComboBoxItem item)
            {
                _draft.GroqModel = item.Tag as string ?? _draft.GroqModel;
            }
        };

        panel.Children.Add(modelCombo);
        return panel;
    }

    private UIElement BuildSystemSection()
    {
        var panel = new StackPanel();

        _autostartToggle = new CheckBox
        {
            Content = "Iniciar junto com o Windows",
            Style = (Style)FindResource("PanelCheckBoxStyle"),
            IsChecked = AutostartService.Shared.IsRegistered
        };
        panel.Children.Add(_autostartToggle);

        return panel;
    }

    private UIElement BuildAdvancedSection()
    {
        var panel = new StackPanel();

        var resetBtn = new Button
        {
            Content = "Refazer onboarding",
            Style = (Style)FindResource("SecondaryButtonStyle"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 170
        };
        resetBtn.Click += (_, _) =>
        {
            var flag = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WaterCue",
                ".onboarding_complete");

            if (System.IO.File.Exists(flag))
            {
                System.IO.File.Delete(flag);
            }

            Close();
            new OnboardingWindow().Show();
        };

        panel.Children.Add(resetBtn);
        return panel;
    }

    private StackPanel MakeLabeledSlider(
        string title,
        double min,
        double max,
        double tick,
        double initialValue,
        Func<double, string> format,
        Action<double> onChange)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleLabel = new TextBlock
        {
            Text = title,
            Foreground = Brush("TextPrimaryBrush"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        var valueLabel = new TextBlock
        {
            Text = format(initialValue),
            Foreground = Brush("AccentBrush"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(titleLabel, 0);
        Grid.SetColumn(valueLabel, 1);
        row.Children.Add(titleLabel);
        row.Children.Add(valueLabel);
        panel.Children.Add(row);

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = tick,
            IsSnapToTickEnabled = true,
            Value = initialValue,
            Style = (Style)FindResource("PanelSliderStyle")
        };
        slider.ValueChanged += (_, e) =>
        {
            valueLabel.Text = format(e.NewValue);
            onChange(e.NewValue);
        };
        panel.Children.Add(slider);

        return panel;
    }

    private static string FormatInterval(int minutes)
    {
        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        int hours = minutes / 60;
        int remainderMinutes = minutes % 60;
        return remainderMinutes == 0 ? $"{hours}h" : $"{hours}h{remainderMinutes}min";
    }

    private static string FormatSeconds(double seconds)
    {
        int totalSeconds = (int)seconds;
        if (totalSeconds < 60)
        {
            return $"{totalSeconds}s";
        }

        int minutes = totalSeconds / 60;
        int remainderSeconds = totalSeconds % 60;
        return remainderSeconds == 0 ? $"{minutes}min" : $"{minutes}min{remainderSeconds}s";
    }

    private TextBlock FieldLabel(string text, Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            Style = (Style)FindResource("FieldLabelTextStyle"),
            Margin = margin ?? new Thickness(0, 0, 0, 6)
        };
    }

    private Brush Brush(string resourceKey) => (Brush)FindResource(resourceKey);

    private void LoadCurrentApiKey()
    {
        var key = SettingsService.Shared.LoadApiKey();
        if (_apiKeyBox != null && !string.IsNullOrEmpty(key))
        {
            _apiKeyBox.Password = key;
        }
    }

    private void SaveApiKeyNow()
    {
        if (_apiKeyBox == null)
        {
            return;
        }

        var key = _apiKeyBox.Password.Trim();
        if (key.Length < 8)
        {
            return;
        }

        SettingsService.Shared.SaveApiKey(key);
        if (_apiKeySavedLabel != null)
        {
            _apiKeySavedLabel.Visibility = Visibility.Visible;
            Task.Delay(2000).ContinueWith(_ =>
                Dispatcher.Invoke(() =>
                {
                    if (_apiKeySavedLabel != null)
                    {
                        _apiKeySavedLabel.Visibility = Visibility.Collapsed;
                    }
                }));
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_autostartToggle != null)
        {
            bool launch = _autostartToggle.IsChecked == true;
            if (launch)
            {
                AutostartService.Shared.Register();
            }
            else
            {
                AutostartService.Shared.Unregister();
            }

            _draft.LaunchAtLogin = launch;
        }

        _draft.Save();
        AppState.Shared.Settings = _draft;
        TimerService.Shared.Schedule(_draft);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
