using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using WaterCueWindows.Models;

namespace WaterCueWindows.Services;

public class CameraInfo(string monikerString, string name)
{
    public string MonikerString { get; } = monikerString;
    public string Name { get; } = name;
}

public class CameraService
{
    public static readonly CameraService Shared = new();

    private VideoCaptureDevice? _device;
    private volatile bool _waitingForFrame;
    private TaskCompletionSource<byte[]>? _captureTcs;

    public bool IsRunning => _device?.IsRunning == true;
    public string? Error { get; private set; }

    // Raised with JPEG bytes after CapturePhoto completes
    public event EventHandler<byte[]>? PhotoCaptured;

    // Raised on every camera frame — subscribe for live preview UI
    public event EventHandler<BitmapSource>? LiveFrameReady;

    private CameraService() { }

    public void Start()
    {
        if (IsRunning) return;
        Error = null;

        var cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        if (cameras.Count == 0)
        {
            Error = "Nenhuma câmera encontrada.";
            return;
        }

        var preferred = HydrationSettings.Load().CameraDeviceId;
        FilterInfo? selected = null;
        foreach (FilterInfo cam in cameras)
        {
            if (cam.MonikerString == preferred) { selected = cam; break; }
        }
        selected ??= cameras[0];

        _device = new VideoCaptureDevice(selected.MonikerString);

        // Pick best resolution ≤ 1920px wide
        if (_device.VideoCapabilities?.Length > 0)
        {
            _device.VideoResolution = _device.VideoCapabilities
                .Where(c => c.FrameSize.Width <= 1920)
                .OrderByDescending(c => c.FrameSize.Width * c.FrameSize.Height)
                .FirstOrDefault();
        }

        _device.NewFrame += OnNewFrame;
        _device.Start();
    }

    public void Stop()
    {
        _waitingForFrame = false;
        _captureTcs?.TrySetCanceled();
        _captureTcs = null;

        if (_device != null)
        {
            _device.NewFrame -= OnNewFrame;
            _device.SignalToStop();
            _device.WaitForStop();
            _device = null;
        }
    }

    // Called via reflection by AppState (no params). Fires PhotoCaptured event on completion.
    public void CapturePhoto()
    {
        if (!IsRunning)
        {
            Error = "Câmera não está ativa.";
            return;
        }
        _waitingForFrame = true;
    }

    // Async variant for direct use (GroqVisionService)
    public Task<byte[]> CapturePhotoAsync(CancellationToken ct = default)
    {
        if (!IsRunning)
            return Task.FromException<byte[]>(new InvalidOperationException("Câmera não está ativa."));

        _captureTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _captureTcs.TrySetCanceled());
        _waitingForFrame = true;
        return _captureTcs.Task;
    }

    public List<CameraInfo> AvailableCameras()
    {
        var list = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        var result = new List<CameraInfo>();
        foreach (FilterInfo cam in list)
            result.Add(new CameraInfo(cam.MonikerString, cam.Name));
        return result;
    }

    private void OnNewFrame(object sender, NewFrameEventArgs e)
    {
        var bitmap = (Bitmap)e.Frame.Clone();

        // Live preview — fire on every frame (subscribers throttle as needed)
        if (LiveFrameReady != null)
        {
            try
            {
                var bitmapSource = BitmapSourceFromBitmap(bitmap);
                bitmapSource.Freeze();
                LiveFrameReady.Invoke(this, bitmapSource);
            }
            catch { }
        }

        if (!_waitingForFrame)
        {
            bitmap.Dispose();
            return;
        }
        _waitingForFrame = false;

        Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream();
                var encoder = GetJpegEncoder();
                var encParams = new EncoderParameters(1);
                encParams.Param[0] = new EncoderParameter(Encoder.Quality, 70L);
                bitmap.Save(ms, encoder, encParams);
                bitmap.Dispose();

                var bytes = ms.ToArray();
                _captureTcs?.TrySetResult(bytes);
                PhotoCaptured?.Invoke(this, bytes);
            }
            catch (Exception ex)
            {
                bitmap.Dispose();
                _captureTcs?.TrySetException(ex);
            }
        });
    }

    private static BitmapSource BitmapSourceFromBitmap(Bitmap bitmap)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            return BitmapSource.Create(
                bitmap.Width, bitmap.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmap.Height,
                bitmapData.Stride);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    private static ImageCodecInfo GetJpegEncoder()
    {
        foreach (var codec in ImageCodecInfo.GetImageEncoders())
            if (codec.FormatID == ImageFormat.Jpeg.Guid) return codec;
        throw new InvalidOperationException("JPEG encoder not found.");
    }
}
