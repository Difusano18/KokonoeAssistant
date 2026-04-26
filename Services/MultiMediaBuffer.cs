using System;
using System.Collections.Generic;
using System.Linq;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Multi-Media Buffer - комбінує скриншоти та фото з камери
    /// Зберігає їх разом з аналізом і хешами
    /// </summary>
    public class MultiMediaBuffer
    {
        public class MediaFrame
        {
            public int FrameIndex { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public byte[]? ScreenshotBytes { get; set; }
            public byte[]? WebcamPhotoBytes { get; set; }
            public string? ScreenshotHash { get; set; }
            public string? WebcamPhotoHash { get; set; }
            public ActivityAnalyzer.ActivityState? ScreenActivity { get; set; }
            public WebcamAnalyzer.WebcamAnalysisResult? WebcamAnalysis { get; set; }
        }

        private readonly List<MediaFrame> _frames = new();
        private readonly object _lock = new();
        private int _frameCounter = 0;
        private const int MAX_FRAMES = 12;

        public IReadOnlyList<MediaFrame> Frames
        {
            get
            {
                lock (_lock)
                {
                    return _frames.AsReadOnly();
                }
            }
        }

        public int FrameCount
        {
            get
            {
                lock (_lock)
                {
                    return _frames.Count;
                }
            }
        }

        public void AddFrame(byte[]? screenshot, byte[]? webcamPhoto, string? screenHash, string? webcamHash)
        {
            lock (_lock)
            {
                var frame = new MediaFrame
                {
                    FrameIndex = _frameCounter++,
                    ScreenshotBytes = screenshot,
                    WebcamPhotoBytes = webcamPhoto,
                    ScreenshotHash = screenHash,
                    WebcamPhotoHash = webcamHash,
                    Timestamp = DateTime.Now
                };

                _frames.Add(frame);

                if (_frames.Count > MAX_FRAMES)
                    _frames.RemoveAt(0);
            }
        }

        public void UpdateFrameAnalysis(int frameIndex, ActivityAnalyzer.ActivityState? activity, WebcamAnalyzer.WebcamAnalysisResult? webcam)
        {
            lock (_lock)
            {
                var frame = _frames.FirstOrDefault(f => f.FrameIndex == frameIndex);
                if (frame != null)
                {
                    frame.ScreenActivity = activity;
                    frame.WebcamAnalysis = webcam;
                }
            }
        }

        public MediaFrame? GetLatestFrame()
        {
            lock (_lock)
            {
                return _frames.LastOrDefault();
            }
        }

        public MediaFrame? GetFrameAt(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _frames.Count)
                    return _frames[index];
            }
            return null;
        }

        public void ClearBuffer()
        {
            lock (_lock)
            {
                _frames.Clear();
                _frameCounter = 0;
            }
        }

        /// <summary>
        /// Отримує послідовність зміни активності за останні N фреймів
        /// </summary>
        public string GetActivityPattern(int lastFrameCount = 6)
        {
            lock (_lock)
            {
                var recentFrames = _frames.TakeLast(lastFrameCount).ToList();
                if (recentFrames.Count == 0) return "unknown";

                var pattern = string.Join("->",
                    recentFrames.Select(f =>
                        f.ScreenActivity?.IsActive == true ? "Active" : "Idle"
                    )
                );
                return pattern;
            }
        }

        /// <summary>
        /// Отримує середню яскравість з останніх фреймів
        /// </summary>
        public double GetAverageBrightness(int lastFrameCount = 6)
        {
            lock (_lock)
            {
                var recentFrames = _frames.TakeLast(lastFrameCount)
                    .Where(f => f.WebcamAnalysis?.Brightness >= 0)
                    .ToList();

                if (recentFrames.Count == 0) return 0;
                return recentFrames.Average(f => f.WebcamAnalysis!.Brightness ?? 0);
            }
        }

        /// <summary>
        /// Отримує найбільш частий активний статус з останніх фреймів
        /// </summary>
        public string GetDominantActivityState(int lastFrameCount = 6)
        {
            lock (_lock)
            {
                var recentFrames = _frames.TakeLast(lastFrameCount)
                    .Where(f => f.ScreenActivity != null)
                    .ToList();

                if (recentFrames.Count == 0) return "unknown";

                var activeCount = recentFrames.Count(f => f.ScreenActivity!.IsActive);
                return activeCount > recentFrames.Count / 2 ? "Active" : "Idle";
            }
        }
    }
}
