using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using KokonoeAssistant.Services;

namespace KokonoeAssistant.Windows
{
    /// <summary>
    /// MediaViewerWindow - Production-grade viewer для візуальних даних моніторингу
    /// </summary>
    public partial class MediaViewerWindow : Window
    {
        private readonly MultiMediaBuffer _buffer;
        private List<FrameDisplayData> _frames = new();

        public class FrameDisplayData
        {
            public int FrameIndex { get; set; }
            public DateTime Timestamp { get; set; }
            public BitmapImage? Screenshot { get; set; }
            public string ActivityStatus { get; set; } = "";
            public string Expression { get; set; } = "";
            public string Details { get; set; } = "";
        }

        public MediaViewerWindow(MultiMediaBuffer buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            InitializeComponent();
            LoadAndDisplayFrames();
        }

        private void LoadAndDisplayFrames()
        {
            try
            {
                _frames.Clear();
                MediaGrid.Items.Clear();

                var bufferFrames = _buffer.Frames;
                if (bufferFrames.Count == 0)
                {
                    TitleStatus.Text = "📹 No data - monitoring not active";
                    StatusText.Text = "Start monitoring to capture frames";
                    return;
                }

                foreach (var frame in bufferFrames)
                {
                    try
                    {
                        var displayData = new FrameDisplayData
                        {
                            FrameIndex = frame.FrameIndex,
                            Timestamp = frame.Timestamp,
                            ActivityStatus = frame.ScreenActivity?.IsActive == true ? "🔴 Active" : "⚫ Idle",
                            Expression = frame.WebcamAnalysis?.ExpressionLevel ?? "?",
                            Details = BuildDetails(frame)
                        };

                        if (frame.ScreenshotBytes != null)
                            displayData.Screenshot = ConvertBytesToImage(frame.ScreenshotBytes);

                        _frames.Add(displayData);
                        AddFrameToGrid(displayData);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MediaViewer] Frame error: {ex.Message}");
                    }
                }

                UpdateStatus();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading frames: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddFrameToGrid(FrameDisplayData data)
        {
            var border = new Border
            {
                Width = 250,
                Height = 200,
                Margin = new Thickness(5),
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 107, 53)),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 26, 26, 46)),
                ToolTip = data.Details
            };

            var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

            if (data.Screenshot != null)
            {
                var img = new System.Windows.Controls.Image 
                { 
                    Source = data.Screenshot, 
                    Height = 140, 
                    Stretch = System.Windows.Media.Stretch.UniformToFill 
                };
                stack.Children.Add(img);
            }

            var infoStack = new StackPanel { Margin = new Thickness(5) };
            
            var timeBlock = new TextBlock
            {
                Text = data.Timestamp.ToString("HH:mm:ss"),
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 188, 204, 220)),
                TextAlignment = TextAlignment.Center
            };
            infoStack.Children.Add(timeBlock);

            var statusBlock = new TextBlock
            {
                Text = data.ActivityStatus,
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 149, 0)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoStack.Children.Add(statusBlock);

            stack.Children.Add(infoStack);
            border.Child = stack;
            MediaGrid.Items.Add(border);
        }

        private string BuildDetails(MultiMediaBuffer.MediaFrame frame)
        {
            var details = $"⏰ {frame.Timestamp:HH:mm:ss}\n";
            
            if (frame.ScreenActivity != null)
            {
                details += $"🖥️  {(frame.ScreenActivity.IsActive ? "Active" : "Idle")}\n";
                details += $"📌 {frame.ScreenActivity.ActiveWindowTitle ?? "Unknown"}\n";
                details += $"📊 {frame.ScreenActivity.PixelDifferencePercentage:F1}% change\n";
            }

            if (frame.WebcamAnalysis != null && frame.WebcamAnalysis.FaceDetected)
            {
                details += $"👤 {frame.WebcamAnalysis.ExpressionLevel}\n";
                details += $"💡 {(frame.WebcamAnalysis.Brightness ?? 0):F2}\n";
            }

            return details.Trim();
        }

        private BitmapImage ConvertBytesToImage(byte[] bytes)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = new MemoryStream(bytes);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return new BitmapImage();
            }
        }

        private void UpdateStatus()
        {
            var frames = _buffer.Frames;
            if (frames.Count == 0) return;

            var duration = DateTime.Now - frames[0].Timestamp;
            var activeCount = frames.Count(f => f.ScreenActivity?.IsActive == true);
            
            TitleStatus.Text = $"📹 Last {duration.TotalMinutes:F0} min | {activeCount}/{frames.Count} active";
            FrameCountText.Text = $"📊 {frames.Count}/12 frames";
            StatusInfoText.Text = _buffer.GetActivityPattern(6);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadAndDisplayFrames();

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog 
                { 
                    Description = "Select export folder" 
                };
                
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    int count = 0;
                    foreach (var frame in _frames.Where(f => f.Screenshot != null))
                    {
                        try
                        {
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(frame.Screenshot));
                            var path = Path.Combine(dlg.SelectedPath, $"frame_{frame.Timestamp:yyyy-MM-dd_HH-mm-ss}.png");
                            using (var stream = File.Create(path))
                                encoder.Save(stream);
                            count++;
                        }
                        catch { }
                    }
                    System.Windows.MessageBox.Show($"Exported {count} frames", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}

