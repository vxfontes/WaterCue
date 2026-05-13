using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WaterCueWindows;
using WaterCueWindows.Services;

namespace WaterCueWindows.Views;

public partial class OnboardingWindow : Window
{
    private int _step;
    private const int TotalSteps = 7;

    public OnboardingWindow()
    {
        InitializeComponent();
        RenderStep(0);
    }

    // ─────────────────────────── Navigation ────────────────────────────

    private void GoNext() { _step = Math.Min(_step + 1, TotalSteps - 1); RenderStep(_step); }
    private void GoBack() { _step = Math.Max(_step - 1, 0); RenderStep(_step); }

    private void RenderStep(int step)
    {
        UpdateProgressDots(step);
        StepContent.Children.Clear();

        switch (step)
        {
            case 0: BuildWelcome(); break;
            case 1: BuildCameraPermission(); break;
            case 2: BuildCameraSelector(); break;
            case 3: BuildNotifications(); break;
            case 4: BuildApiKey(); break;
            case 5: BuildIntervals(); break;
            default: BuildDone(); break;
        }
    }

    private void UpdateProgressDots(int current)
    {
        ProgressDots.Items.Clear();
        for (int i = 0; i < TotalSteps; i++)
        {
            var border = new Border
            {
                Width = i == current ? 28 : 8,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(4, 0, 4, 0),
                Background = i <= current
                    ? new SolidColorBrush(Color.FromRgb(0x27, 0xB7, 0xF5))
                    : new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))
            };
            ProgressDots.Items.Add(border);
        }
    }

    // ─────────────────────────── UI Helpers ────────────────────────────

    private static TextBlock H1(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
        FontSize = 24, FontWeight = FontWeights.Bold,
        Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
        FontSize = 13,
        Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF)),
        HorizontalAlignment = HorizontalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 0, 0, 20)
    };

    private static TextBlock Icon(string emoji, double size = 48) => new()
    {
        Text = emoji, FontSize = size,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 0, 0, 12)
    };

    private Button PrimaryButton(string label, RoutedEventHandler handler)
    {
        var btn = new Button { Height = 42, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 4, 0, 4) };
        btn.Template = BuildPrimaryTemplate();
        btn.Content = new TextBlock
        {
            Text = label, Foreground = Brushes.White,
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
        };
        btn.Click += handler;
        return btn;
    }

    private Button SecondaryButton(string label, RoutedEventHandler handler)
    {
        var btn = new Button { Height = 36, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 0, 4) };
        btn.Template = BuildSecondaryTemplate();
        btn.Content = new TextBlock
        {
            Text = label, FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF)),
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
        };
        btn.Click += handler;
        return btn;
    }

    private static ControlTemplate BuildPrimaryTemplate()
    {
        var tpl = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        var brush = new LinearGradientBrush(
            Color.FromRgb(0x27, 0xB7, 0xF5),
            Color.FromRgb(0x00, 0x77, 0xC8), 0);
        factory.SetValue(Border.BackgroundProperty, brush);
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(cp);
        tpl.VisualTree = factory;
        return tpl;
    }

    private static ControlTemplate BuildSecondaryTemplate()
    {
        var tpl = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        factory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        factory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x1A, 0x29, 0x40)));
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(cp);
        tpl.VisualTree = factory;
        return tpl;
    }

    private static Border InfoCard(UIElement content)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 16),
            Child = content
        };
    }

    // ─────────────────────────── Steps ────────────────────────────────

    private void BuildWelcome()
    {
        StepContent.Children.Add(Icon("💧", 64));
        StepContent.Children.Add(H1("Bem-vindo ao WaterCue!"));
        StepContent.Children.Add(Body(
            "Vou travar seu Windows periodicamente até você beber água\ne comprovar com uma foto. Chega de \"vou agora\" que nunca vai."));
        StepContent.Children.Add(PrimaryButton("Começar →", (_, _) => GoNext()));
    }

    private void BuildCameraPermission()
    {
        StepContent.Children.Add(Icon("📷", 48));
        StepContent.Children.Add(H1("Permissão de câmera"));
        StepContent.Children.Add(Body(
            "Precisamos da câmera para validar a foto do seu copo de água\nantes de desbloquear o Windows."));

        bool available = CameraService.Shared.AvailableCameras().Count > 0;

        if (available)
        {
            var status = new TextBlock
            {
                Text = "✅  Câmera disponível",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0xC0, 0x60)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            StepContent.Children.Add(status);
            StepContent.Children.Add(PrimaryButton("Continuar →", (_, _) => GoNext()));
        }
        else
        {
            StepContent.Children.Add(Body("Câmera não detectada. Conecte uma câmera e continue."));
            StepContent.Children.Add(PrimaryButton("Continuar mesmo assim →", (_, _) => GoNext()));
        }
    }

    private void BuildCameraSelector()
    {
        StepContent.Children.Add(Icon("🎥", 48));
        StepContent.Children.Add(H1("Qual câmera usar?"));
        StepContent.Children.Add(Body("Escolha a câmera que vai usar para tirar a foto de hidratação."));

        var cameras = CameraService.Shared.AvailableCameras();

        var listBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var autoItem = new ListBoxItem
        {
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Automático", FontSize = 13, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold },
                    new TextBlock { Text = "Usa a câmera padrão do sistema", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF)) }
                }
            },
            Tag = string.Empty,
            Padding = new Thickness(8, 6, 8, 6)
        };
        listBox.Items.Add(autoItem);

        foreach (var cam in cameras)
        {
            listBox.Items.Add(new ListBoxItem
            {
                Content = new TextBlock { Text = cam.Name, FontSize = 13, Foreground = Brushes.White },
                Tag = cam.MonikerString,
                Padding = new Thickness(8, 6, 8, 6)
            });
        }

        listBox.SelectedIndex = 0;
        string currentId = AppState.Shared.Settings.CameraDeviceId;
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (((ListBoxItem)listBox.Items[i]).Tag is string tag && tag == currentId)
            { listBox.SelectedIndex = i; break; }
        }

        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is ListBoxItem item)
            {
                var s = AppState.Shared.Settings;
                s.CameraDeviceId = item.Tag as string ?? string.Empty;
                s.Save();
                AppState.Shared.Settings = s;
            }
        };

        StepContent.Children.Add(InfoCard(listBox));
        StepContent.Children.Add(PrimaryButton("Continuar →", (_, _) => GoNext()));
    }

    private void BuildNotifications()
    {
        StepContent.Children.Add(Icon("🔔", 48));
        StepContent.Children.Add(H1("Notificações"));
        StepContent.Children.Add(Body(
            "Vou te avisar alguns minutos antes de travar.\nAssim você pode beber antes e o Windows nem chega a travar."));

        var notifService = AppState.Shared.NotificationService;
        bool hasNotif = notifService != null;

        if (hasNotif)
        {
            StepContent.Children.Add(Body("✅  Notificações configuradas via Windows Toast."));
        }
        else
        {
            StepContent.Children.Add(Body("Notificações serão ativadas automaticamente."));
        }

        StepContent.Children.Add(PrimaryButton("Continuar →", (_, _) => GoNext()));
        StepContent.Children.Add(SecondaryButton("Pular (sem aviso prévio)", (_, _) => GoNext()));
    }

    private void BuildApiKey()
    {
        StepContent.Children.Add(Icon("🤖", 48));
        StepContent.Children.Add(H1("Chave Groq (IA)"));
        StepContent.Children.Add(Body(
            "A IA do Groq analisa sua foto gratuitamente.\nCole a chave abaixo — ela fica guardada no Windows Credential Manager."));

        var keyBox = new PasswordBox
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

        var errorLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x40, 0x40)),
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var saveBtn = PrimaryButton("Salvar e continuar", (_, _) =>
        {
            var key = keyBox.Password.Trim();
            if (key.Length < 20) { errorLabel.Text = "Chave muito curta. Formato: gsk_..."; errorLabel.Visibility = Visibility.Visible; return; }

            try
            {
                SaveApiKey(key);
                errorLabel.Visibility = Visibility.Collapsed;
                GoNext();
            }
            catch (Exception ex)
            {
                errorLabel.Text = ex.Message;
                errorLabel.Visibility = Visibility.Visible;
            }
        });

        var linkLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xB7, 0xF5)),
            Cursor = System.Windows.Input.Cursors.Hand,
            Text = "→ Criar chave grátis em console.groq.com",
            Margin = new Thickness(0, 4, 0, 12)
        };
        linkLabel.MouseDown += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://console.groq.com/keys") { UseShellExecute = true });

        StepContent.Children.Add(keyBox);
        StepContent.Children.Add(errorLabel);
        StepContent.Children.Add(linkLabel);
        StepContent.Children.Add(saveBtn);
    }

    private void BuildIntervals()
    {
        StepContent.Children.Add(Icon("⏱", 48));
        StepContent.Children.Add(H1("Configurar intervalos"));
        StepContent.Children.Add(Body("Ajuste quando o Windows trava e quanto tempo antes o botão de emergência aparece."));

        var s = AppState.Shared.Settings;
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };

        // Interval slider
        var intervalLabel = new TextBlock
        {
            Text = FormatInterval(s.IntervalSeconds / 60),
            FontSize = 13, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold
        };
        var intervalRow = new Grid();
        intervalRow.ColumnDefinitions.Add(new ColumnDefinition());
        intervalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var intervalTitle = new TextBlock { Text = "🔒  Travar a cada", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF)) };
        Grid.SetColumn(intervalTitle, 0); Grid.SetColumn(intervalLabel, 1);
        intervalRow.Children.Add(intervalTitle); intervalRow.Children.Add(intervalLabel);
        panel.Children.Add(intervalRow);

        var intervalSlider = new Slider
        {
            Minimum = 15, Maximum = 240, TickFrequency = 15, IsSnapToTickEnabled = true,
            Value = s.IntervalSeconds / 60.0, Margin = new Thickness(0, 4, 0, 16)
        };
        intervalSlider.ValueChanged += (_, e) =>
        {
            var settings = AppState.Shared.Settings;
            settings.IntervalSeconds = (int)e.NewValue * 60;
            settings.Save();
            AppState.Shared.Settings = settings;
            intervalLabel.Text = FormatInterval((int)e.NewValue);
        };
        panel.Children.Add(intervalSlider);

        // Emergency delay slider
        var emergLabel = new TextBlock
        {
            Text = FormatSeconds(s.EmergencyDelaySeconds),
            FontSize = 13, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold
        };
        var emergRow = new Grid();
        emergRow.ColumnDefinitions.Add(new ColumnDefinition());
        emergRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var emergTitle = new TextBlock { Text = "⚠  Emergência após", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xA0, 0x20)) };
        Grid.SetColumn(emergTitle, 0); Grid.SetColumn(emergLabel, 1);
        emergRow.Children.Add(emergTitle); emergRow.Children.Add(emergLabel);
        panel.Children.Add(emergRow);

        var emergSlider = new Slider
        {
            Minimum = 10, Maximum = 300, TickFrequency = 10, IsSnapToTickEnabled = true,
            Value = s.EmergencyDelaySeconds, Margin = new Thickness(0, 4, 0, 4)
        };
        emergSlider.ValueChanged += (_, e) =>
        {
            var settings = AppState.Shared.Settings;
            settings.EmergencyDelaySeconds = (int)e.NewValue;
            settings.Save();
            AppState.Shared.Settings = settings;
            emergLabel.Text = FormatSeconds((int)e.NewValue);
        };
        panel.Children.Add(emergSlider);

        StepContent.Children.Add(InfoCard(panel));
        StepContent.Children.Add(PrimaryButton("Continuar →", (_, _) => GoNext()));
    }

    private void BuildDone()
    {
        StepContent.Children.Add(Icon("✅", 64));
        StepContent.Children.Add(H1("Tudo pronto!"));
        StepContent.Children.Add(Body(
            $"WaterCue vai travar seu Windows a cada {FormatInterval(AppState.Shared.Settings.IntervalSeconds / 60)}.\nFique de olho na gotinha 💧 na bandeja do sistema."));

        StepContent.Children.Add(PrimaryButton("Começar agora", (_, _) =>
        {
            App.MarkOnboardingComplete();
            AppState.Shared.LockState = LockState.Idle;
            TimerService.Shared.Schedule(AppState.Shared.Settings);
            AppState.Shared.RefreshStats();
            Close();
        }));
    }

    // ─────────────────────────── Helpers ────────────────────────────────

    private static void SaveApiKey(string key)
        => SettingsService.Shared.SaveApiKey(key);

    private static string FormatInterval(int minutes)
    {
        if (minutes < 60) return $"{minutes} min";
        int h = minutes / 60, m = minutes % 60;
        return m == 0 ? $"{h}h" : $"{h}h{m}min";
    }

    private static string FormatSeconds(int seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        int m = seconds / 60, s = seconds % 60;
        return s == 0 ? $"{m}min" : $"{m}min{s}s";
    }
}
