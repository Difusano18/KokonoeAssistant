using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
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
        private const double TargetAreaPerParticle = 14000.0;
        private const int MinParticles = 42;
        private const int MaxParticles = 190;

        private readonly List<Particle> _particles = new();
        private readonly Random _random = new(1309);
        private TimeSpan _lastFrame;
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
            _linePen = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(31, 90, 160, 110)), 0.7);
            _linePen.Freeze();
            _particleBrush = new SolidColorBrush(MediaColor.FromArgb(115, 150, 210, 90));
            _particleBrush.Freeze();
            _particleGlowBrush = new SolidColorBrush(MediaColor.FromArgb(20, 160, 255, 100));
            _particleGlowBrush.Freeze();

            Loaded += (_, _) =>
            {
                _lastFrame = TimeSpan.Zero;
                CompositionTarget.Rendering += OnRendering;
                EnsureParticleField(new WSize(ActualWidth, ActualHeight));
            };

            Unloaded += (_, _) => CompositionTarget.Rendering -= OnRendering;
            SizeChanged += (_, e) => EnsureParticleField(e.NewSize);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            dc.DrawRectangle(_backgroundBrush, null, bounds);
            DrawLinks(dc);
            DrawParticles(dc);
            dc.DrawRectangle(_vignetteBrush, null, bounds);
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (e is not RenderingEventArgs args) return;
            if (_lastFrame == TimeSpan.Zero)
            {
                _lastFrame = args.RenderingTime;
                return;
            }

            var delta = Math.Clamp((args.RenderingTime - _lastFrame).TotalSeconds, 0.0, 0.05);
            _lastFrame = args.RenderingTime;
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
            var speed = 2.0 + _random.NextDouble() * 7.0;
            var angle = _random.NextDouble() * Math.PI * 2.0;
            return new Particle
            {
                Position = new WPoint(_random.NextDouble() * size.Width, _random.NextDouble() * size.Height),
                Velocity = new WVector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                Radius = 1.0 + _random.NextDouble() * 1.8,
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
                particle.DriftPhase += delta * 0.18;

                var drift = new WVector(
                    Math.Cos(particle.DriftPhase) * 0.45,
                    Math.Sin(particle.DriftPhase * 0.83) * 0.45);

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
                    var opacity = Math.Pow(1.0 - distance / LinkDistance, 1.8) * 0.55;
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
                dc.PushOpacity(0.65);
                dc.DrawEllipse(_particleGlowBrush, null, particle.Position, particle.Radius * 4.8, particle.Radius * 4.8);
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
