using System;
using System.Drawing;
using System.IO;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Webcam Analyzer - базовий аналіз фото з камери
    /// Виявляє обличчя, освітлення, приблизний вираз
    /// </summary>
    public class WebcamAnalyzer
    {
        public class WebcamAnalysisResult
        {
            public bool FaceDetected { get; set; }
            public string? ExpressionLevel { get; set; } = "unknown"; // "ok", "tired", "stressed", "focused"
            public double? Brightness { get; set; } // 0-1
            public double? Confidence { get; set; } // Confidence in analysis
            public string? Details { get; set; } // Additional observations
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Простий аналіз фото з камери без зовнішніх залежностей
        /// </summary>
        public WebcamAnalysisResult AnalyzePhotoBytes(byte[] photoBytes)
        {
            var result = new WebcamAnalysisResult
            {
                Timestamp = DateTime.Now,
                Confidence = 0.6 // Default confidence for simple analysis
            };

            try
            {
                using (var ms = new MemoryStream(photoBytes))
                using (var bitmap = new Bitmap(ms))
                {
                    // Analyze brightness
                    result.Brightness = CalculateAverageBrightness(bitmap);

                    // Simple face detection (just checks for skin-tone areas)
                    result.FaceDetected = DetectFaceRegions(bitmap);

                    // Guess expression based on brightness and image characteristics
                    result.ExpressionLevel = GuessExpressionLevel(bitmap, result.Brightness ?? 0);

                    // Confidence based on image clarity
                    result.Confidence = EvaluateImageQuality(bitmap);

                    // Details
                    if (result.Brightness < 0.3)
                        result.Details = "Low lighting conditions";
                    if (result.Brightness > 0.9)
                        result.Details = "Very bright lighting";
                    if (!result.FaceDetected)
                        result.Details = "No face detected in frame";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebcamAnalyzer] Analysis error: {ex.Message}");
                result.FaceDetected = false;
                result.Details = "Analysis failed";
            }

            return result;
        }

        private double CalculateAverageBrightness(Bitmap bitmap)
        {
            try
            {
                double totalBrightness = 0;
                int pixelCount = 0;
                int step = 8; // Sample every 8th pixel for speed

                for (int x = 0; x < bitmap.Width; x += step)
                {
                    for (int y = 0; y < bitmap.Height; y += step)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        // Perceptually weighted brightness
                        double brightness = (pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114) / 255.0;
                        totalBrightness += brightness;
                        pixelCount++;
                    }
                }

                return pixelCount > 0 ? totalBrightness / pixelCount : 0.5;
            }
            catch
            {
                return 0.5;
            }
        }

        private bool DetectFaceRegions(Bitmap bitmap)
        {
            try
            {
                int skinTonePixels = 0;
                int checkedPixels = 0;
                int step = 4;

                for (int x = 0; x < bitmap.Width; x += step)
                {
                    for (int y = 0; y < bitmap.Height; y += step)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        
                        // Simple skin tone detection (HSV-like check)
                        if (IsSkinTone(pixel))
                            skinTonePixels++;
                        
                        checkedPixels++;
                    }
                }

                // If more than 5% of sampled pixels are skin tone, consider face detected
                double skinPercentage = (double)skinTonePixels / checkedPixels;
                return skinPercentage > 0.05;
            }
            catch
            {
                return false;
            }
        }

        private bool IsSkinTone(Color color)
        {
            // Heuristic skin tone detection in RGB space
            // Based on common skin tone ranges

            int r = color.R;
            int g = color.G;
            int b = color.B;

            // Skin tones generally have higher R than B, and moderate G
            bool inRGBRange = r > 95 && g > 40 && b > 20 &&
                              r > g && r > b &&
                              Math.Abs(r - g) > 15;

            // Additional check: not too saturated (avoid colored objects)
            bool notTooSaturated = !(r > 220 && g < 50 && b < 50); // Red object
            bool notTooDesaturated = !(r < 100 && g < 100 && b < 100); // Dark areas

            return inRGBRange && notTooSaturated && notTooDesaturated;
        }

        private string GuessExpressionLevel(Bitmap bitmap, double brightness)
        {
            try
            {
                // Very simple heuristics based on image properties
                
                // If very dark, might indicate tiredness or eyes closed
                if (brightness < 0.25)
                    return "tired";

                // If very bright and high contrast, might indicate focus
                double contrast = CalculateContrast(bitmap);
                if (contrast > 0.6 && brightness > 0.6)
                    return "focused";

                // If moderate brightness and low contrast, might indicate relaxed
                if (brightness > 0.4 && contrast < 0.4)
                    return "ok";

                // If high brightness and high contrast might indicate stressed
                if (brightness > 0.7 && contrast > 0.7)
                    return "stressed";

                return "ok";
            }
            catch
            {
                return "unknown";
            }
        }

        private double CalculateContrast(Bitmap bitmap)
        {
            try
            {
                double[] brightnesses = new double[16]; // Divide image into 4x4 grid
                int index = 0;

                int cellWidth = bitmap.Width / 4;
                int cellHeight = bitmap.Height / 4;

                for (int cy = 0; cy < 4; cy++)
                {
                    for (int cx = 0; cx < 4; cx++)
                    {
                        double cellBrightness = 0;
                        int count = 0;

                        for (int y = cy * cellHeight; y < (cy + 1) * cellHeight; y += 4)
                        {
                            for (int x = cx * cellWidth; x < (cx + 1) * cellWidth; x += 4)
                            {
                                if (x < bitmap.Width && y < bitmap.Height)
                                {
                                    Color pixel = bitmap.GetPixel(x, y);
                                    cellBrightness += (pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114) / 255.0;
                                    count++;
                                }
                            }
                        }

                        brightnesses[index++] = count > 0 ? cellBrightness / count : 0.5;
                    }
                }

                // Calculate standard deviation as contrast measure
                double mean = 0;
                foreach (var b in brightnesses)
                    mean += b;
                mean /= brightnesses.Length;

                double variance = 0;
                foreach (var b in brightnesses)
                    variance += (b - mean) * (b - mean);
                variance /= brightnesses.Length;

                return Math.Sqrt(variance);
            }
            catch
            {
                return 0.5;
            }
        }

        private double EvaluateImageQuality(Bitmap bitmap)
        {
            try
            {
                // Confidence based on image resolution and clarity
                double resolutionScore = Math.Min(1.0, (bitmap.Width * bitmap.Height) / (1280.0 * 720.0));
                
                // Calculate sharpness (edge detection proxy)
                double sharpnessScore = CalculateSharpness(bitmap);

                // Confidence is average of resolution and sharpness
                return (resolutionScore + sharpnessScore) / 2.0;
            }
            catch
            {
                return 0.5;
            }
        }

        private double CalculateSharpness(Bitmap bitmap)
        {
            try
            {
                double edgePixels = 0;
                int checkedPixels = 0;
                int step = 8;

                for (int x = step; x < bitmap.Width - step; x += step)
                {
                    for (int y = step; y < bitmap.Height - step; y += step)
                    {
                        Color center = bitmap.GetPixel(x, y);
                        Color right = bitmap.GetPixel(x + step, y);
                        Color bottom = bitmap.GetPixel(x, y + step);

                        int diffRight = Math.Abs(center.R - right.R) + Math.Abs(center.G - right.G) + Math.Abs(center.B - right.B);
                        int diffBottom = Math.Abs(center.R - bottom.R) + Math.Abs(center.G - bottom.G) + Math.Abs(center.B - bottom.B);

                        if (diffRight > 50 || diffBottom > 50)
                            edgePixels++;

                        checkedPixels++;
                    }
                }

                double sharpness = checkedPixels > 0 ? edgePixels / (double)checkedPixels : 0.5;
                return Math.Min(1.0, sharpness);
            }
            catch
            {
                return 0.5;
            }
        }
    }
}
