using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WaterCueWindows.Services;

namespace WaterCueWindows.Views;

public partial class LockOverlayWindow : Window
{
    private readonly DispatcherTimer _emergencyTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _emergencyElapsed;

    public bool IsPrimary { get; set; }

    public LockOverlayWindow()
    {
        InitializeComponent();

        _emergencyTimer.Tick += OnEmergencyTick;
        AppState.Shared.PropertyChanged += OnAppStateChanged;
        CameraService.Shared.LiveFrameReady += OnLiveFrame;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsPrimary)
        {
            PrimaryContent.Visibility = Visibility.Visible;
            SecondaryContent.Visibility = Visibility.Collapsed;
            UpdateCameraStatusUI();
            StartEmergencyCountdown();
        }
        else
        {
            PrimaryContent.Visibility = Visibility.Collapsed;
            SecondaryContent.Visibility = Visibility.Visible;
        }

        UpdateLockStateUI(AppState.Shared.LockState);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        AppState.Shared.PropertyChanged -= OnAppStateChanged;
        CameraService.Shared.LiveFrameReady -= OnLiveFrame;
        _emergencyTimer.Stop();
    }

    private void OnLiveFrame(object? sender, System.Windows.Media.Imaging.BitmapSource frame)
    {
        if (!IsPrimary)
        {
            return;
        }

        Dispatcher.InvokeAsync(() =>
        {
            CameraPreview.Source = frame;
            if (NoCameraOverlay.Visibility == Visibility.Visible)
            {
                UpdateCameraStatusUI();
            }
        });
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsPrimary)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            if (e.PropertyName == nameof(AppState.LockState))
            {
                UpdateLockStateUI(AppState.Shared.LockState);
            }

            if (e.PropertyName == nameof(AppState.ValidationFailedReason))
            {
                FailedReasonLabel.Text = AppState.Shared.ValidationFailedReason;
            }
        });
    }

    private void UpdateCameraStatusUI()
    {
        var camera = CameraService.Shared;
        bool running = camera.IsRunning;

        NoCameraOverlay.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        CaptureButton.IsEnabled = running && AppState.Shared.LockState == LockState.Locked;
        CaptureButtonLabel.Text = running ? "Capturar foto" : "Aguardando câmera...";
        CaptureButtonLabel.Foreground = Brushes.White;

        if (camera.Error != null)
        {
            CameraStatusIcon.Text = "ERRO";
            CameraStatusLabel.Text = "Câmera indisponível ou bloqueada.";
            RetryButton.Visibility = Visibility.Visible;
        }
        else if (!running)
        {
            CameraStatusIcon.Text = "CAM";
            CameraStatusLabel.Text = "Iniciando câmera...";
            RetryButton.Visibility = Visibility.Collapsed;
        }

        CameraGlow.Opacity = running ? 0.3 : 0;
        CameraRingBrush.Color = running
            ? Color.FromArgb(0x75, 0x3A, 0xA0, 0xF3)
            : Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
    }

    private void UpdateLockStateUI(LockState state)
    {
        if (!IsPrimary)
        {
            return;
        }

        CameraPanel.Visibility = state == LockState.Locked ? Visibility.Visible : Visibility.Collapsed;
        ValidatingPanel.Visibility = state == LockState.Validating ? Visibility.Visible : Visibility.Collapsed;
        FailedPanel.Visibility = state == LockState.ValidationFailed ? Visibility.Visible : Visibility.Collapsed;

        if (state == LockState.ValidationFailed)
        {
            FailedReasonLabel.Text = AppState.Shared.ValidationFailedReason;
        }

        if (state == LockState.Locked)
        {
            UpdateCameraStatusUI();
        }
    }

    private void StartEmergencyCountdown()
    {
        _emergencyElapsed = 0;
        int delay = AppState.Shared.Settings.EmergencyDelaySeconds;
        EmergencyCountdownLabel.Text = $"Emergência disponível em {delay}s";
        EmergencyButton.Visibility = Visibility.Collapsed;
        _emergencyTimer.Start();
    }

    private void OnEmergencyTick(object? sender, EventArgs e)
    {
        _emergencyElapsed++;
        int delay = AppState.Shared.Settings.EmergencyDelaySeconds;
        int remaining = delay - _emergencyElapsed;

        if (remaining <= 0)
        {
            _emergencyTimer.Stop();
            EmergencyCountdownLabel.Text = string.Empty;
            EmergencyButton.Visibility = Visibility.Visible;
        }
        else
        {
            EmergencyCountdownLabel.Text = $"Emergência disponível em {remaining}s";
        }
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
        => AppState.Shared.CaptureAndValidate();

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        CameraService.Shared.Stop();
        CameraService.Shared.Start();
    }

    private void RetryCapture_Click(object sender, RoutedEventArgs e)
    {
        AppState.Shared.LockState = LockState.Locked;
        UpdateCameraStatusUI();
    }

    private void EmergencyButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "O desbloqueio de emergência não conta como hidratação. Use só se realmente precisar.",
            "Desbloquear sem beber água?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            AppState.Shared.EmergencyUnlock();
        }
    }
}
