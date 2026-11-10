using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowScatter
{
    internal class WindowLayoutCalculator
    {
        // A little overlap is preferable to losing the user's spatial context.
        // Measured against the smaller thumbnail so small windows stay visible.
        private const double MaxOverlapFraction = 0.05;

        public List<WindowLayout> CalculateLayouts(List<WindowInfo> windows, double screenW, double screenH)
        {
            if (!double.IsFinite(screenW) || !double.IsFinite(screenH) || screenW <= 0 || screenH <= 0)
                throw new ArgumentOutOfRangeException(nameof(screenW), "Screen dimensions must be finite and positive.");
            if (windows.Count == 0) return new List<WindowLayout>();
            if (windows.Any(w => Width(w) <= 0 || Height(w) <= 0))
                throw new ArgumentException("Window dimensions must be positive.", nameof(windows));

            double padding = Math.Min(50, Math.Min(screenW, screenH) * 0.05);
            double availableW = screenW - 2 * padding;
            double availableH = screenH - 2 * padding;

            // Never use enumeration/Z-order as a tie-breaker: activating a window
            // changes Z-order even when none of the desktop geometry has changed.
            var ordered = windows.OrderByDescending(w => Width(w) * Height(w))
                .ThenBy(w => w.OriginalRect.Left + Width(w) / 2)
                .ThenBy(w => w.OriginalRect.Top + Height(w) / 2)
                .ThenBy(w => w.Handle.ToInt64()).ToList();
            double area = ordered.Sum(w => Width(w) * Height(w));
            double scale = Math.Min(0.5, Math.Sqrt(availableW * availableH * 0.65 / area));
            scale = Math.Min(scale, ordered.Min(w => Math.Min(availableW / Width(w), availableH / Height(w))));

            // Retry the WHOLE arrangement at a common scale; never resize a single
            // thumbnail or swap windows into slots made for different dimensions.
            // The final scale also fits every window in one row, guaranteeing a
            // feasible arrangement without an overlapping-center fallback.
            double rowScale = Math.Min(scale, availableW / ordered.Sum(Width));
            while (true)
            {
                var layouts = TryPlace(ordered, scale, padding, screenW, screenH);
                if (layouts != null) return layouts;
                // Greedy placement is not a complete packing solver. At the
                // guaranteed row-fit scale, use a spatially ordered row rather
                // than retrying the same unsuccessful arrangement forever.
                if (scale <= rowScale)
                    return PlaceRow(ordered, rowScale, padding, screenW, screenH);
                scale = Math.Max(rowScale, scale * 0.9);
            }
        }

        private static double Width(WindowInfo w) => (double)w.OriginalRect.Right - w.OriginalRect.Left;
        private static double Height(WindowInfo w) => (double)w.OriginalRect.Bottom - w.OriginalRect.Top;

        private static List<WindowLayout> PlaceRow(List<WindowInfo> windows, double scale,
            double padding, double screenW, double screenH)
        {
            double x = (screenW - windows.Sum(Width) * scale) / 2;
            var result = new List<WindowLayout>();
            foreach (var w in windows.OrderBy(w => w.OriginalRect.Left + Width(w) / 2)
                .ThenBy(w => w.OriginalRect.Top + Height(w) / 2).ThenBy(w => w.Handle.ToInt64()))
            {
                double height = Height(w) * scale;
                result.Add(new WindowLayout
                {
                    Window = w, OrigWidth = Width(w), OrigHeight = Height(w),
                    Width = Width(w) * scale, Height = height, X = x,
                    Y = Math.Clamp(w.OriginalRect.Top + Height(w) / 2 - height / 2,
                        padding, Math.Max(padding, screenH - padding - height))
                });
                x += Width(w) * scale;
            }
            return result;
        }

        private static List<WindowLayout>? TryPlace(List<WindowInfo> windows, double scale,
            double padding, double screenW, double screenH)
        {
            var placed = new List<WindowLayout>();
            foreach (var window in windows)
            {
                var layout = new WindowLayout
                {
                    Window = window, OrigWidth = Width(window), OrigHeight = Height(window),
                    Width = Width(window) * scale, Height = Height(window) * scale
                };
                // A window fitted exactly to the available dimension can put
                // the computed upper bound a few ULPs below padding.
                double maxX = Math.Max(padding, screenW - padding - layout.Width);
                double maxY = Math.Max(padding, screenH - padding - layout.Height);
                double anchorX = Math.Clamp(window.OriginalRect.Left + layout.OrigWidth / 2 - layout.Width / 2, padding, maxX);
                double anchorY = Math.Clamp(window.OriginalRect.Top + layout.OrigHeight / 2 - layout.Height / 2, padding, maxY);
                var xs = new List<double> { anchorX, padding, maxX };
                var ys = new List<double> { anchorY, padding, maxY };
                foreach (var other in placed)
                {
                    double allowedArea = MaxOverlapFraction * Math.Min(layout.Width * layout.Height, other.Width * other.Height);
                    double overlapX = allowedArea / Math.Min(layout.Height, other.Height);
                    double overlapY = allowedArea / Math.Min(layout.Width, other.Width);
                    xs.AddRange(new[] { other.X - layout.Width, other.X + other.Width,
                        other.X - layout.Width + overlapX, other.X + other.Width - overlapX });
                    ys.AddRange(new[] { other.Y - layout.Height, other.Y + other.Height,
                        other.Y - layout.Height + overlapY, other.Y + other.Height - overlapY });
                }

                double bestDistance = double.PositiveInfinity;
                bool found = false;
                // Include obstacle edges as candidates, rather than a coarse grid
                // that can miss narrow free spaces. Sorted ties are deterministic.
                var candidateXs = xs.Select(x => Math.Clamp(x, padding, maxX)).Distinct().OrderBy(x => x).ToArray();
                var candidateYs = ys.Select(y => Math.Clamp(y, padding, maxY)).Distinct().OrderBy(y => y).ToArray();
                foreach (double x in candidateXs)
                foreach (double y in candidateYs)
                {
                    double distance = (x - anchorX) * (x - anchorX) + (y - anchorY) * (y - anchorY);
                    if (distance >= bestDistance) continue;
                    if (placed.Any(other => OverlapsTooMuch(x, y, layout, other))) continue;
                    bestDistance = distance;
                    layout.X = x;
                    layout.Y = y;
                    found = true;
                }
                if (!found) return null;
                placed.Add(layout);
            }
            return placed;
        }

        private static bool OverlapsTooMuch(double x, double y, WindowLayout layout, WindowLayout other)
        {
            double w = Math.Max(0, Math.Min(x + layout.Width, other.X + other.Width) - Math.Max(x, other.X));
            double h = Math.Max(0, Math.Min(y + layout.Height, other.Y + other.Height) - Math.Max(y, other.Y));
            double smallerArea = Math.Min(layout.Width * layout.Height, other.Width * other.Height);
            return w * h > smallerArea * MaxOverlapFraction + 1e-7;
        }
    }
}
