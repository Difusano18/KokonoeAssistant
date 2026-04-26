using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Webcam Capture Service - фото з веб-камери кожні N хвилин
    /// ПРИМІТКА: Для повної функціональності потрібна Windows.Media API или інша бібліотека
    /// Поки що працює як заглушка, яка буде вдосконалена пізніше
    /// </summary>
    public class WebcamCaptureService : IDisposable
    {
        private readonly int _intervalSeconds;
        private Task? _captureTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly List<byte[]> _webcamBuffer = new();
        private readonly object _bufferLock = new();
        private const int BUFFER_CAPACITY = 12;

        public bool IsRunning { get; private set; }
        public bool IsAvailable { get; private set; }
        public IReadOnlyList<byte[]> WebcamBuffer
        {
            get
            {
                lock (_bufferLock)
                {
                    return _webcamBuffer.AsReadOnly();
                }
            }
        }

        public WebcamCaptureService(int intervalSeconds = 300)
        {
            _intervalSeconds = intervalSeconds;
            // For now, webcam is not available without additional dependencies
            IsAvailable = false;
            System.Diagnostics.Debug.WriteLine("[WebcamCaptureService] Webcam capture not available (requires Windows.Media API or similar library)");
        }

        public async Task InitializeAsync()
        {
            // Placeholder for future webcam initialization
            IsAvailable = false;
            await Task.CompletedTask;
        }

        public void Start()
        {
            if (IsRunning || !IsAvailable) return;

            _cancellationTokenSource = new CancellationTokenSource();
            _captureTask = CaptureLoopAsync(_cancellationTokenSource.Token);
            IsRunning = true;
            System.Diagnostics.Debug.WriteLine("[WebcamCaptureService] Started");
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
            System.Diagnostics.Debug.WriteLine("[WebcamCaptureService] Stopped");
        }

        public void Stop()
        {
            // Synchronous wrapper for backward compatibility
            StopAsync().Wait(5000);
        }

        public void Cleanup()
        {
            Dispose();
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _webcamBuffer.Clear();
            System.Diagnostics.Debug.WriteLine("[WebcamCaptureService] Disposed");
        }

        private async Task CaptureLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await CapturePhotoAsync();
                    await Task.Delay(_intervalSeconds * 1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        private async Task CapturePhotoAsync()
        {
            // Placeholder - no actual capture without proper API
            await Task.CompletedTask;
        }

        public byte[]? GetLatestPhoto()
        {
            lock (_bufferLock)
            {
                if (_webcamBuffer.Count > 0)
                    return _webcamBuffer[_webcamBuffer.Count - 1];
            }
            return null;
        }

        public byte[]? GetPhotoAt(int index)
        {
            lock (_bufferLock)
            {
                if (index >= 0 && index < _webcamBuffer.Count)
                    return _webcamBuffer[index];
            }
            return null;
        }

        public void ClearBuffer()
        {
            lock (_bufferLock)
            {
                _webcamBuffer.Clear();
            }
        }
    }
}

