using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WaterCueWindows.Services;

namespace WaterCueWindows.Views;

public partial class OnboardingWindow : Window
{
    private int _step;
    private const int TotalSteps = 5;

    public OnboardingWindow()
    {
        InitializeComponent();
        RenderStep(0);
    }

    private void GoNext()
    {
        _step = Math.Min(_step + 1, TotalSteps - 1);
        RenderStep(_step);
    }

    private void RenderStep(int step)
    {
        UpdateProgressDots(step);
        StepContent.Children.Clear();

        switch (step)
        {
            case 0:
                BuildWelcome();
                break;
            case 1:
                BuildCameraSetup();
                break;
            case 2:
                BuildApiKey();
                break;
            case 3:
                BuildIntervals();
                break;
            default:
                BuildDone();
                break;
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

    private static TextBlock H1(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
        FontSize = 24,
        FontWeight = FontWeights.Bold,
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

    private static TextBlock EmojiIcon(string emoji, double size = 48) => new()
    {
        Text = emoji,
        FontSize = size,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 0, 0, 12)
    };

    private Button PrimaryButton(string label, RoutedEventHandler handler)
    {
        var btn = new Button
        {
            Height = 42,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 4, 0, 4),
            Template = BuildPrimaryTemplate(),
            Content = new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
            }
        };

        btn.Click += handler;
        return btn;
    }

    private static ControlTemplate BuildPrimaryTemplate()
    {
        var tpl = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        factory.SetValue(
            Border.BackgroundProperty,
            new LinearGradientBrush(Color.FromRgb(0x27, 0xB7, 0xF5), Color.FromRgb(0x00, 0x77, 0xC8), 0));

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

    private void BuildWelcome()
    {
        StepContent.Children.Add(EmojiIcon("\U0001F4A7", 64));
        StepContent.Children.Add(H1("Bem-vindo ao WaterCue"));
        StepContent.Children.Add(Body(
            "Vou travar seu Windows periodicamente ate voce beber agua e comprovar com uma foto. "
            + "No Windows, notificacoes funcionam direto e a configuracao da camera fica em um passo so."));
        StepContent.Children.Add(PrimaryButton("Comecar", (_, _) => GoNext()));
    }

    private void BuildCameraSetup()
    {
        StepContent.Children.Add(EmojiIcon("\U0001F4F7", 48));
        StepContent.Children.Add(H1("Configurar camera"));
        StepContent.Children.Add(Body(
            "Aqui nao tem pedido extra de permissao. Escolha a camera que o app vai usar "
            + "para validar sua hidratacao."));

        var cameras = CameraService.Shared.AvailableCameras();
        var hasCamera = cameras.Count > 0;

        StepContent.Children.Add(new TextBlock
        {
            Text = hasCamera ? "Camera detectada" : "Nenhuma camera detectada agora",
            FontSize = 13,
            Foreground = hasCamera
                ? new SolidColorBrush(Color.FromRgb(0x30, 0xC0, 0x60))
                : new SolidColorBrush(Color.FromRgb(0xE8, 0xA0, 0x20)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var listBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 16)
        };

        listBox.Items.Add(new ListBoxItem
        {
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "Automatico",
                        FontSize = 13,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "Usa a camera padrao do sistema",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF))
                    }
                }
            },
            Tag = string.Empty,
            Padding = new Thickness(8, 6, 8, 6)
        });

        foreach (var cam in cameras)
        {
            listBox.Items.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = cam.Name,
                    FontSize = 13,
                    Foreground = Brushes.White
                },
                Tag = cam.MonikerString,
                Padding = new Thickness(8, 6, 8, 6)
            });
        }

        listBox.SelectedIndex = 0;
        var currentId = AppState.Shared.Settings.CameraDeviceId;
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (((ListBoxItem)listBox.Items[i]).Tag is string tag && tag == currentId)
            {
                listBox.SelectedIndex = i;
                break;
            }
        }

        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is not ListBoxItem item)
            {
                return;
            }

            var settings = AppState.Shared.Settings;
            settings.CameraDeviceId = item.Tag as string ?? string.Empty;
            settings.Save();
            AppState.Shared.Settings = settings;
        };

        StepContent.Children.Add(InfoCard(listBox));
        StepContent.Children.Add(PrimaryButton(hasCamera ? "Continuar" : "Continuar mesmo assim", (_, _) => GoNext()));
    }

    private void BuildApiKey()
    {
        StepContent.Children.Add(EmojiIcon("\U0001F916", 48));
        StepContent.Children.Add(H1("Chave Groq"));
        StepContent.Children.Add(Body(
            "A IA do Groq analisa sua foto. Cole a chave abaixo. "
            + "Ela fica guardada com protecao do proprio Windows no seu perfil local."));

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
            if (key.Length < 20)
            {
                errorLabel.Text = "Chave muito curta. Formato esperado: gsk_...";
                errorLabel.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                SettingsService.Shared.SaveApiKey(key);
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
            Text = "Criar chave gratis em console.groq.com",
            Margin = new Thickness(0, 4, 0, 12)
        };
        linkLabel.MouseDown += (_, _) =>
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("https://console.groq.com/keys") { UseShellExecute = true });

        StepContent.Children.Add(keyBox);
        StepContent.Children.Add(errorLabel);
        StepContent.Children.Add(linkLabel);
        StepContent.Children.Add(saveBtn);
    }

    private void BuildIntervals()
    {
        StepContent.Children.Add(EmojiIcon("\u23F1", 48));
        StepContent.Children.Add(H1("Configurar intervalos"));
        StepContent.Children.Add(Body("Ajuste quando o Windows trava e quanto tempo depois o botao de emergencia aparece."));

        var settings = AppState.Shared.Settings;
        var panel = new StackPanel();

        var intervalLabel = new TextBlock
        {
            Text = FormatInterval(settings.IntervalSeconds / 60),
            FontSize = 13,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold
        };

        var intervalRow = new Grid();
        intervalRow.ColumnDefinitions.Add(new ColumnDefinition());
        intervalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var intervalTitle = new TextBlock
        {
            Text = "Travar a cada",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBF))
        };

        Grid.SetColumn(intervalTitle, 0);
        Grid.SetColumn(intervalLabel, 1);
        intervalRow.Children.Add(intervalTitle);
        intervalRow.Children.Add(intervalLabel);
        panel.Children.Add(intervalRow);

        var intervalSlider = new Slider
        {
            Minimum = 15,
            Maximum = 240,
            TickFrequency = 15,
            IsSnapToTickEnabled = true,
            Value = settings.IntervalSeconds / 60.0,
            Margin = new Thickness(0, 4, 0, 16)
        };
        intervalSlider.ValueChanged += (_, e) =>
        {
            var currentSettings = AppState.Shared.Settings;
            currentSettings.IntervalSeconds = (int)e.NewValue * 60;
            currentSettings.Save();
            AppState.Shared.Settings = currentSettings;
            intervalLabel.Text = FormatInterval((int)e.NewValue);
        };
        panel.Children.Add(intervalSlider);

        var emergencyLabel = new TextBlock
        {
            Text = FormatSeconds(settings.EmergencyDelaySeconds),
            FontSize = 13,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold
        };

        var emergencyRow = new Grid();
        emergencyRow.ColumnDefinitions.Add(new ColumnDefinition());
        emergencyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var emergencyTitle = new TextBlock
        {
            Text = "Emergencia apos",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xA0, 0x20))
        };

        Grid.SetColumn(emergencyTitle, 0);
        Grid.SetColumn(emergencyLabel, 1);
        emergencyRow.Children.Add(emergencyTitle);
        emergencyRow.Children.Add(emergencyLabel);
        panel.Children.Add(emergencyRow);

        var emergencySlider = new Slider
        {
            Minimum = 10,
            Maximum = 300,
            TickFrequency = 10,
            IsSnapToTickEnabled = true,
            Value = settings.EmergencyDelaySeconds,
            Margin = new Thickness(0, 4, 0, 4)
        };
        emergencySlider.ValueChanged += (_, e) =>
        {
            var currentSettings = AppState.Shared.Settings;
            currentSettings.EmergencyDelaySeconds = (int)e.NewValue;
            currentSettings.Save();
            AppState.Shared.Settings = currentSettings;
            emergencyLabel.Text = FormatSeconds((int)e.NewValue);
        };
        panel.Children.Add(emergencySlider);

        StepContent.Children.Add(InfoCard(panel));
        StepContent.Children.Add(PrimaryButton("Continuar", (_, _) => GoNext()));
    }

    private void BuildDone()
    {
        StepContent.Children.Add(EmojiIcon("\u2705", 64));
        StepContent.Children.Add(H1("Tudo pronto"));
        StepContent.Children.Add(Body(
            $"O WaterCue vai travar seu Windows a cada {FormatInterval(AppState.Shared.Settings.IntervalSeconds / 60)}. "
            + "Fique de olho no icone da bandeja."));

        StepContent.Children.Add(PrimaryButton("Comecar agora", (_, _) =>
        {
            App.MarkOnboardingComplete();
            AppState.Shared.LockState = LockState.Idle;
            TimerService.Shared.Schedule(AppState.Shared.Settings);
            AppState.Shared.RefreshStats();
            Close();
        }));
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

    private static string FormatSeconds(int seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds}s";
        }

        int minutes = seconds / 60;
        int remainderSeconds = seconds % 60;
        return remainderSeconds == 0 ? $"{minutes}min" : $"{minutes}min{remainderSeconds}s";
    }
}
