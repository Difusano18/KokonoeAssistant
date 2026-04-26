using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Screen Capture Service - знімає скриншоти екрану кожні N хвилин
    /// Зберігає в буфер, без архівування (локальна історія)
    /// </summary>
    public class ScreenCaptureService : IDisposable
    {
        private readonly int _intervalSeconds;
        private Task? _captureTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly List<byte[]> _screenshotBuffer = new();
        private readonly object _bufferLock = new();
        private const int BUFFER_CAPACITY = 12; // 12 скриншотів = 1 година при 5 хвилин

        public bool IsRunning { get; private set; }
        public IReadOnlyList<byte[]> ScreenshotBuffer
        {
            get
            {
                lock (_bufferLock)
                {
                    return _screenshotBuffer.AsReadOnly();
                }
            }
        }

        public ScreenCaptureService(int intervalSeconds = 300)
        {
            _intervalSeconds = intervalSeconds;
        }

        public void Start()
        {
            if (IsRunning) return;

            _cancellationTokenSource = new CancellationTokenSource();
            _captureTask = CaptureLoopAsync(_cancellationTokenSource.Token);
            IsRunning = true;
            System.Diagnostics.Debug.WriteLine("[ScreenCaptureService] Started, interval: " + _intervalSeconds + "s");
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;

            _cancellationTokenSource?.Cancel();
            if (_captureTask != null)
            {
                try
                {
                    await Task.WhenAny(_captureTask, Task.Delay(5000));
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation token is cancelled
                }
            }
            
            IsRunning = false;
            System.Diagnostics.Debug.WriteLine("[ScreenCaptureService] Stopped");
        }

        public void Stop()
        {
            // Synchronous wrapper for backward compatibility
            StopAsync().Wait(5000);
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _screenshotBuffer.Clear();
            System.Diagnostics.Debug.WriteLine("[ScreenCaptureService] Disposed");
        }

        private async Task CaptureLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    CaptureScreenshot();
                    await Task.Delay(_intervalSeconds * 1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCaptureService] Error: {ex.Message}");
            }
        }

        private void CaptureScreenshot()
        {
            try
            {
                // Get primary screen dimensions
                int screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

                using (var bitmap = new Bitmap(screenWidth, screenHeight))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // Capture the entire screen
                    graphics.CopyFromScreen(0, 0, 0, 0, new Size(screenWidth, screenHeight));

                    // Compress to JPEG bytes (to save memory)
                    using (var ms = new MemoryStream())
                    {
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(x => x.MimeType == "image/jpeg");
                        
                        if (jpegEncoder != null)
                        {
                            var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                            encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                                System.Drawing.Imaging.Encoder.Quality, 70L);
                            bitmap.Save(ms, jpegEncoder, encoderParams);
                        }
                        else
                        {
                            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }

                        byte[] imageBytes = ms.ToArray();

                        lock (_bufferLock)
                        {
                            _screenshotBuffer.Add(imageBytes);
                            if (_screenshotBuffer.Count > BUFFER_CAPACITY)
                                _screenshotBuffer.RemoveAt(0);
                        }

                        System.Diagnostics.Debug.WriteLine(
                            $"[ScreenCaptureService] Screenshot captured: {imageBytes.Length} bytes, " +
                            $"buffer: {_screenshotBuffer.Count}/{BUFFER_CAPACITY}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCaptureService] Capture error: {ex.Message}");
            }
        }

        public byte[]? GetLatestScreenshot()
        {
            lock (_bufferLock)
            {
                if (_screenshotBuffer.Count > 0)
                    return _screenshotBuffer[_screenshotBuffer.Count - 1];
            }
            return null;
        }

        public byte[]? GetScreenshotAt(int index)
        {
            lock (_bufferLock)
            {
                if (index >= 0 && index < _screenshotBuffer.Count)
                    return _screenshotBuffer[index];
            }
            return null;
        }

        public void ClearBuffer()
        {
            lock (_bufferLock)
            {
                _screenshotBuffer.Clear();
            }
        }
    }
}
