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
                BuildMetrics();
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
            ProgressDots.Items.Add(new Border
            {
                Width = i == current ? 42 : 18,
                Height = 8,
                CornerRadius = new CornerRadius(999),
                Margin = new Thickness(0, 0, 8, 0),
                Background = i <= current ? Brush("AccentBrush") : Brush("SurfaceBrush")
            });
        }
    }

    private TextBlock StepTitle(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI Variable, Segoe UI"),
        FontSize = 30,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brush("TextPrimaryBrush"),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private TextBlock Body(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = Brush("TextSecondaryBrush"),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 18)
    };

    private TextBlock Badge(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brush("AccentBrush"),
        Margin = new Thickness(0, 0, 0, 10)
    };

    private TextBlock CardLine(string text) => new()
    {
        Text = text,
        Foreground = Brush("TextPrimaryBrush"),
        FontSize = 13,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        Style = (Style)FindResource("FieldLabelTextStyle")
    };

    private Border InfoCard(UIElement content)
    {
        return new Border
        {
            Style = (Style)FindResource("CardBorderStyle"),
            Background = Brush("BackgroundBrush"),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 16),
            Child = content
        };
    }

    private Button PrimaryButton(string label, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = label,
            Style = (Style)FindResource("PrimaryButtonStyle"),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 160,
            Margin = new Thickness(0, 2, 0, 0)
        };
        button.Click += handler;
        return button;
    }

    private void BuildWelcome()
    {
        StepContent.Children.Add(Badge("PASSO 1 DE 5"));
        StepContent.Children.Add(StepTitle("Deixe o WaterCue pronto para o seu ritmo"));
        StepContent.Children.Add(Body(
            "Este assistente configura câmera, IA e as quatro métricas principais de hidratação "
            + "com um fluxo enxuto para Windows."));

        StepContent.Children.Add(InfoCard(new StackPanel
        {
            Children =
            {
                CardLine("Configuração rápida da câmera"),
                CardLine("Integração com a Groq API"),
                CardLine("Intervalo, aviso, emergência e meta diária")
            }
        }));

        StepContent.Children.Add(PrimaryButton("Começar", (_, _) => GoNext()));
    }

    private void BuildCameraSetup()
    {
        StepContent.Children.Add(Badge("PASSO 2 DE 5"));
        StepContent.Children.Add(StepTitle("Escolha a câmera"));
        StepContent.Children.Add(Body(
            "No Windows não há uma etapa separada de permissão dentro do app. "
            + "Basta selecionar a câmera que será usada para validar a sua hidratação."));

        var cameras = CameraService.Shared.AvailableCameras();
        bool hasCamera = cameras.Count > 0;

        StepContent.Children.Add(new Border
        {
            Background = hasCamera ? Brush("AccentDimBrush") : Brush("BackgroundBrush"),
            BorderBrush = hasCamera ? Brush("CardBorderBrush") : Brush("WarningBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 16),
            Child = new TextBlock
            {
                Text = hasCamera
                    ? "Câmera detectada e pronta para uso."
                    : "Nenhuma câmera foi detectada neste momento.",
                Foreground = hasCamera ? Brush("TextPrimaryBrush") : Brush("WarningBrush"),
                FontSize = 12
            }
        });

        var listBox = new ListBox
        {
            Background = Brush("InputBrush"),
            BorderBrush = Brush("InputBorderBrush"),
            BorderThickness = new Thickness(1)
        };
        listBox.Items.Add(new ListBoxItem
        {
            Content = "Automática (padrão do sistema)",
            Tag = string.Empty,
            Padding = new Thickness(10, 8, 10, 8)
        });

        foreach (var camera in cameras)
        {
            listBox.Items.Add(new ListBoxItem
            {
                Content = camera.Name,
                Tag = camera.MonikerString,
                Padding = new Thickness(10, 8, 10, 8)
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
        StepContent.Children.Add(Badge("PASSO 3 DE 5"));
        StepContent.Children.Add(StepTitle("Conecte a Groq API"));
        StepContent.Children.Add(Body(
            "A validação da foto usa o modelo configurado aqui. "
            + "A chave fica protegida no seu perfil do Windows."));

        var keyBox = new PasswordBox
        {
            Style = (Style)FindResource("InputPasswordBoxStyle"),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var errorLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = Brush("DangerBrush"),
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var link = new TextBlock
        {
            Text = "Criar chave grátis em console.groq.com",
            Foreground = Brush("AccentBrush"),
            FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 10)
        };
        link.MouseDown += (_, _) =>
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("https://console.groq.com/keys") { UseShellExecute = true });

        StepContent.Children.Add(InfoCard(new StackPanel
        {
            Children =
            {
                FieldLabel("Groq API Key"),
                keyBox,
                errorLabel,
                link
            }
        }));

        StepContent.Children.Add(PrimaryButton("Salvar e continuar", (_, _) =>
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
                GoNext();
            }
            catch (Exception ex)
            {
                errorLabel.Text = ex.Message;
                errorLabel.Visibility = Visibility.Visible;
            }
        }));
    }

    private void BuildMetrics()
    {
        StepContent.Children.Add(Badge("PASSO 4 DE 5"));
        StepContent.Children.Add(StepTitle("Defina as 4 métricas"));
        StepContent.Children.Add(Body(
            "Esses ajustes controlam quando o WaterCue bloqueia, quanto tempo antes avisa, "
            + "quando libera a saída de emergência e qual é a sua meta diária."));

        var settings = AppState.Shared.Settings;
        var panel = new StackPanel();

        panel.Children.Add(MakeSlider(
            "Bloquear a cada",
            settings.IntervalSeconds / 60.0,
            15,
            240,
            15,
            value =>
            {
                settings.IntervalSeconds = (int)value * 60;
                settings.Save();
                AppState.Shared.Settings = settings;
            },
            value => FormatInterval((int)value)));

        panel.Children.Add(MakeSlider(
            "Avisar com antecedência",
            settings.WarningSeconds / 60.0,
            1,
            10,
            1,
            value =>
            {
                settings.WarningSeconds = (int)value * 60;
                settings.Save();
                AppState.Shared.Settings = settings;
            },
            value => $"{(int)value} min"));

        panel.Children.Add(MakeSlider(
            "Emergência após",
            settings.EmergencyDelaySeconds,
            10,
            300,
            10,
            value =>
            {
                settings.EmergencyDelaySeconds = (int)value;
                settings.Save();
                AppState.Shared.Settings = settings;
            },
            value => FormatSeconds((int)value)));

        panel.Children.Add(MakeSlider(
            "Meta diária",
            settings.DailyGoal,
            4,
            16,
            1,
            value =>
            {
                settings.DailyGoal = (int)value;
                settings.Save();
                AppState.Shared.Settings = settings;
            },
            value => $"{(int)value} copos"));

        StepContent.Children.Add(InfoCard(panel));
        StepContent.Children.Add(PrimaryButton("Continuar", (_, _) => GoNext()));
    }

    private void BuildDone()
    {
        StepContent.Children.Add(Badge("PASSO 5 DE 5"));
        StepContent.Children.Add(StepTitle("Tudo pronto"));
        StepContent.Children.Add(Body(
            $"O WaterCue vai bloquear o Windows a cada {FormatInterval(AppState.Shared.Settings.IntervalSeconds / 60)}. "
            + "A gota continua na bandeja para abrir estatísticas e preferências."));

        StepContent.Children.Add(PrimaryButton("Começar agora", (_, _) =>
        {
            App.MarkOnboardingComplete();
            AppState.Shared.LockState = LockState.Idle;
            TimerService.Shared.Schedule(AppState.Shared.Settings);
            AppState.Shared.RefreshStats();
            Close();
        }));
    }

    private StackPanel MakeSlider(
        string title,
        double initialValue,
        double min,
        double max,
        double tick,
        Action<double> onChange,
        Func<double, string> format)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = title,
            Foreground = Brush("TextPrimaryBrush"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        var valueBlock = new TextBlock
        {
            Text = format(initialValue),
            Foreground = Brush("AccentBrush"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };

        Grid.SetColumn(titleBlock, 0);
        Grid.SetColumn(valueBlock, 1);
        row.Children.Add(titleBlock);
        row.Children.Add(valueBlock);

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
            valueBlock.Text = format(e.NewValue);
            onChange(e.NewValue);
        };

        panel.Children.Add(row);
        panel.Children.Add(slider);
        return panel;
    }

    private Brush Brush(string key) => (Brush)FindResource(key);

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
