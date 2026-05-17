using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaPen = System.Windows.Media.Pen;
using WPoint = System.Windows.Point;
using WSize = System.Windows.Size;
using WVector = System.Windows.Vector;

namespace KokonoeAssistant.Controls
{
    public sealed class NeuralParticleBackground : FrameworkElement
    {
        private const double LinkDistance = 130.0;
        private const double TargetAreaPerParticle = 32000.0;
        private const int MinParticles = 18;
        private const int MaxParticles = 72;
        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(95);

        private readonly List<Particle> _particles = new();
        private readonly Random _random = new(1309);
        private readonly DispatcherTimer _timer;
        private DateTime _lastFrameUtc;
        private DateTime _lastResizeUtc;
        private WSize _lastSize;

        private readonly MediaBrush _backgroundBrush;
        private readonly MediaBrush _vignetteBrush;
        private readonly MediaPen _linePen;
        private readonly MediaBrush _particleBrush;
        private readonly MediaBrush _particleGlowBrush;

        public NeuralParticleBackground()
        {
            IsHitTestVisible = false;
            SnapsToDevicePixels = false;
            UseLayoutRounding = false;

            _backgroundBrush = BuildBackgroundBrush();
            _vignetteBrush = BuildVignetteBrush();
            _linePen = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(19, 82, 132, 92)), 0.55);
            _linePen.Freeze();
            _particleBrush = new SolidColorBrush(MediaColor.FromArgb(84, 126, 168, 82));
            _particleBrush.Freeze();
            _particleGlowBrush = new SolidColorBrush(MediaColor.FromArgb(10, 130, 210, 86));
            _particleGlowBrush.Freeze();
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = FrameInterval
            };
            _timer.Tick += OnTimerTick;

            Loaded += (_, _) =>
            {
                _lastFrameUtc = DateTime.UtcNow;
                EnsureParticleField(new WSize(ActualWidth, ActualHeight));
                if (IsAnimated)
                    _timer.Start();
            };

            Unloaded += (_, _) => _timer.Stop();
            SizeChanged += (_, e) =>
            {
                _lastResizeUtc = DateTime.UtcNow;
                EnsureParticleField(e.NewSize);
            };
        }

        public bool IsAnimated { get; set; } = false;

        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            dc.DrawRectangle(_backgroundBrush, null, bounds);
            DrawLinks(dc);
            DrawParticles(dc);
            dc.DrawRectangle(_vignetteBrush, null, bounds);
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if ((DateTime.UtcNow - _lastResizeUtc).TotalMilliseconds < 220)
                return;

            var now = DateTime.UtcNow;
            var delta = Math.Clamp((now - _lastFrameUtc).TotalSeconds, 0.0, 0.12);
            _lastFrameUtc = now;
            UpdateParticles(delta);
            InvalidateVisual();
        }

        private void EnsureParticleField(WSize size)
        {
            if (size.Width <= 0 || size.Height <= 0) return;

            var desired = (int)Math.Clamp(
                Math.Round((size.Width * size.Height) / TargetAreaPerParticle),
                MinParticles,
                MaxParticles);

            while (_particles.Count < desired)
                _particles.Add(CreateParticle(size));

            if (_particles.Count > desired)
                _particles.RemoveRange(desired, _particles.Count - desired);

            if (_lastSize.Width > 0 && _lastSize.Height > 0 && _particles.Count > 0)
            {
                var scaleX = size.Width / _lastSize.Width;
                var scaleY = size.Height / _lastSize.Height;
                foreach (var particle in _particles)
                    particle.Position = new WPoint(particle.Position.X * scaleX, particle.Position.Y * scaleY);
            }

            _lastSize = size;
            InvalidateVisual();
        }

        private Particle CreateParticle(WSize size)
        {
            var speed = 0.55 + _random.NextDouble() * 2.2;
            var angle = _random.NextDouble() * Math.PI * 2.0;
            return new Particle
            {
                Position = new WPoint(_random.NextDouble() * size.Width, _random.NextDouble() * size.Height),
                Velocity = new WVector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                Radius = 0.9 + _random.NextDouble() * 1.35,
                DriftPhase = _random.NextDouble() * Math.PI * 2.0
            };
        }

        private void UpdateParticles(double delta)
        {
            var width = ActualWidth;
            var height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            for (var i = 0; i < _particles.Count; i++)
            {
                var particle = _particles[i];
                particle.DriftPhase += delta * 0.06;

                var drift = new WVector(
                    Math.Cos(particle.DriftPhase) * 0.18,
                    Math.Sin(particle.DriftPhase * 0.83) * 0.18);

                var next = particle.Position + ((particle.Velocity + drift) * delta);

                if (next.X < -8) next.X = width + 8;
                else if (next.X > width + 8) next.X = -8;

                if (next.Y < -8) next.Y = height + 8;
                else if (next.Y > height + 8) next.Y = -8;

                particle.Position = next;
            }
        }

        private void DrawLinks(DrawingContext dc)
        {
            for (var i = 0; i < _particles.Count; i++)
            {
                var a = _particles[i];
                for (var j = i + 1; j < _particles.Count; j++)
                {
                    var b = _particles[j];
                    var dx = a.Position.X - b.Position.X;
                    var dy = a.Position.Y - b.Position.Y;
                    var distanceSq = dx * dx + dy * dy;
                    if (distanceSq > LinkDistance * LinkDistance) continue;

                    var distance = Math.Sqrt(distanceSq);
                    var opacity = Math.Pow(1.0 - distance / LinkDistance, 1.8) * 0.34;
                    if (opacity <= 0.01) continue;

                    dc.PushOpacity(opacity);
                    dc.DrawLine(_linePen, a.Position, b.Position);
                    dc.Pop();
                }
            }
        }

        private void DrawParticles(DrawingContext dc)
        {
            foreach (var particle in _particles)
            {
                dc.PushOpacity(0.42);
                dc.DrawEllipse(_particleGlowBrush, null, particle.Position, particle.Radius * 3.4, particle.Radius * 3.4);
                dc.Pop();

                dc.DrawEllipse(_particleBrush, null, particle.Position, particle.Radius, particle.Radius);
            }
        }

        private static MediaBrush BuildBackgroundBrush()
        {
            var brush = new RadialGradientBrush
            {
                Center = new WPoint(0.38, 0.42),
                GradientOrigin = new WPoint(0.34, 0.36),
                RadiusX = 0.92,
                RadiusY = 0.88
            };
            brush.GradientStops.Add(new GradientStop((MediaColor)MediaColorConverter.ConvertFromString("#0A130B"), 0.0));
            brush.GradientStops.Add(new GradientStop((MediaColor)MediaColorConverter.ConvertFromString("#071008"), 0.45));
            brush.GradientStops.Add(new GradientStop((MediaColor)MediaColorConverter.ConvertFromString("#030604"), 1.0));
            brush.Freeze();
            return brush;
        }

        private static MediaBrush BuildVignetteBrush()
        {
            var brush = new RadialGradientBrush
            {
                Center = new WPoint(0.5, 0.48),
                GradientOrigin = new WPoint(0.5, 0.48),
                RadiusX = 0.78,
                RadiusY = 0.72
            };
            brush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(0, 0, 0, 0), 0.0));
            brush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(52, 0, 0, 0), 0.72));
            brush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(170, 0, 0, 0), 1.0));
            brush.Freeze();
            return brush;
        }
    }
}
