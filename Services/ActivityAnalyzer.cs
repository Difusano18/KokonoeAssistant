using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Activity Analyzer - порівнює скриншоти для виявлення Idle vs Active
    /// Аналізує активне вікно через Windows API
    /// </summary>
    public class ActivityAnalyzer
    {
        public class ActivityState
        {
            public bool IsActive { get; set; }
            public string? ActiveWindowTitle { get; set; }
            public string? ActiveWindowClass { get; set; }
            public double PixelDifferencePercentage { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public TimeSpan TimeSinceLastChange { get; set; }
        }

        private byte[]? _previousScreenshot;
        private DateTime _lastChangeTime = DateTime.Now;
        private const double ACTIVITY_THRESHOLD_PERCENT = 5.0; // 5% zminy = aktivnost
        private const long MAX_SCREENSHOT_SIZE_BYTES = 10 * 1024 * 1024; // 10MB limit

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        public ActivityState AnalyzeScreenshot(byte[] currentScreenshot)
        {
            if (currentScreenshot == null || currentScreenshot.Length == 0)
                return new ActivityState 
                { 
                    IsActive = false, 
                    ActiveWindowTitle = "Error: No screenshot",
                    Timestamp = DateTime.Now,
                    TimeSinceLastChange = DateTime.Now - _lastChangeTime
                };

            var state = new ActivityState
            {
                Timestamp = DateTime.Now,
                TimeSinceLastChange = DateTime.Now - _lastChangeTime
            };

            // Get active window
            GetActiveWindowInfo(out string? windowTitle, out string? windowClass);
            state.ActiveWindowTitle = windowTitle;
            state.ActiveWindowClass = windowClass;

            // Analyze pixel difference
            if (_previousScreenshot != null)
            {
                double difference = CalculatePixelDifference(currentScreenshot, _previousScreenshot);
                state.PixelDifferencePercentage = difference;
                state.IsActive = difference > ACTIVITY_THRESHOLD_PERCENT;

                if (state.IsActive)
                    _lastChangeTime = DateTime.Now;
            }
            else
            {
                // First screenshot, assume active
                state.IsActive = true;
            }

            // Keep previous screenshot but limit memory usage
            if (currentScreenshot.Length < MAX_SCREENSHOT_SIZE_BYTES)
            {
                _previousScreenshot = currentScreenshot;
            }
            else
            {
                // Screenshot too large, discard to avoid memory leak
                _previousScreenshot = null;
                System.Diagnostics.Debug.WriteLine($"[ActivityAnalyzer] Screenshot too large ({currentScreenshot.Length} bytes), discarding to prevent memory leak");
            }
            
            return state;
        }

        private void GetActiveWindowInfo(out string? title, out string? className)
        {
            title = null;
            className = null;

            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return;

                // Get window title
                var titleBuilder = new StringBuilder(256);
                int titleLength = GetWindowText(hwnd, titleBuilder, 256);
                if (titleLength > 0)
                    title = titleBuilder.ToString();

                // Get window class
                var classBuilder = new StringBuilder(256);
                int classLength = GetClassName(hwnd, classBuilder, 256);
                if (classLength > 0)
                    className = classBuilder.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ActivityAnalyzer] Error getting window info: {ex.Message}");
            }
        }

        private double CalculatePixelDifference(byte[] screenshot1, byte[] screenshot2)
        {
            try
            {
                if (screenshot1.Length != screenshot2.Length)
                    return 100.0; // Повна різниця

                // Порівнюємо JPEG байти (не ідеально, але швидко)
                // Можливо покращити через розпакування та порівняння пікселів

                int diffCount = 0;
                for (int i = 0; i < screenshot1.Length; i++)
                {
                    if (screenshot1[i] != screenshot2[i])
                        diffCount++;
                }

                double percentageDiff = (double)diffCount / screenshot1.Length * 100.0;
                return percentageDiff;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ActivityAnalyzer] Error calculating difference: {ex.Message}");
                return 0.0;
            }
        }

        /// <summary>
        /// Альтернативний метод порівняння через Bitmap розпакування (точніше, але повільніше)
        /// </summary>
        public double CalculatePixelDifferenceAdvanced(byte[] screenshotBytes1, byte[] screenshotBytes2)
        {
            try
            {
                using (var ms1 = new MemoryStream(screenshotBytes1))
                using (var ms2 = new MemoryStream(screenshotBytes2))
                using (var bmp1 = new Bitmap(ms1))
                using (var bmp2 = new Bitmap(ms2))
                {
                    if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height)
                        return 100.0;

                    int diffPixels = 0;
                    int totalPixels = bmp1.Width * bmp1.Height;

                    // Sample every 4th pixel для прискорення
                    int step = 4;
                    for (int x = 0; x < bmp1.Width; x += step)
                    {
                        for (int y = 0; y < bmp1.Height; y += step)
                        {
                            Color c1 = bmp1.GetPixel(x, y);
                            Color c2 = bmp2.GetPixel(x, y);

                            // Simple RGB difference threshold
                            int diff = Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
                            if (diff > 30) // Color difference threshold
                                diffPixels++;
                        }
                    }

                    double percentageDiff = (double)diffPixels / (totalPixels / (step * step)) * 100.0;
                    return percentageDiff;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ActivityAnalyzer] Advanced diff error: {ex.Message}");
                return 0.0;
            }
        }

        public string GenerateScreenshotHash(byte[] screenshotBytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(screenshotBytes);
                return Convert.ToBase64String(hash).Substring(0, 16); // Short hash for logging
            }
        }
    }
}
