using WindowScatter;
using Xunit;
using static WindowScatter.Win32Interop;

public class LayoutTests
{
    private static WindowInfo Win(int id, int x, int y, int w, int h) => new()
    {
        Handle = new IntPtr(id), Title = $"Window {id}", WasMaximized = false,
        OriginalRect = new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h }
    };

    private static List<WindowLayout> Calculate(List<WindowInfo> windows, double w = 1920, double h = 1080)
        => new WindowLayoutCalculator().CalculateLayouts(windows, w, h);

    private static void VerifyGeometry(List<WindowLayout> layouts, double w, double h)
    {
        double? scale = null;
        foreach (var l in layouts)
        {
            Assert.True(double.IsFinite(l.X + l.Y + l.Width + l.Height));
            Assert.True(l.Width > 0 && l.Height > 0);
            Assert.InRange(l.X, 0, w - l.Width + 1e-7);
            Assert.InRange(l.Y, 0, h - l.Height + 1e-7);
            double sx = l.Width / (l.Window.OriginalRect.Right - l.Window.OriginalRect.Left);
            double sy = l.Height / (l.Window.OriginalRect.Bottom - l.Window.OriginalRect.Top);
            Assert.Equal(sx, sy, 8);
            if (scale.HasValue) Assert.Equal(scale.Value, sx, 8);
            scale = sx;
        }
        for (int i = 0; i < layouts.Count; i++)
        for (int j = i + 1; j < layouts.Count; j++)
        {
            var a = layouts[i]; var b = layouts[j];
            double area = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X))
                * Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
            Assert.True(area <= .05 * Math.Min(a.Width * a.Height, b.Width * b.Height) + 1e-6,
                $"Excessive overlap: {area}");
        }
    }

    [Fact]
    public void MixedSizesKeepOneScaleAndAspectRatios()
        => VerifyGeometry(Calculate([Win(1, 0, 0, 1800, 900), Win(2, 900, 200, 300, 600),
            Win(3, 300, 400, 700, 200)]), 1920, 1080);

    [Fact]
    public void DenseStackFitsWithoutExcessiveOverlap()
        => VerifyGeometry(Calculate(Enumerable.Range(1, 20).Select(i => Win(i, 0, 0, 1920, 1080)).ToList()), 1920, 1080);

    [Fact]
    public void RepeatedCallsAndEnumerationOrderDoNotChangeLayout()
    {
        var windows = Enumerable.Range(1, 8).Select(i => Win(i, i * 90, i * 40, 900, 600)).ToList();
        var calculator = new WindowLayoutCalculator();
        var first = calculator.CalculateLayouts(windows, 1920, 1080).ToDictionary(l => l.Window.Handle);
        windows.Reverse();
        var second = calculator.CalculateLayouts(windows, 1920, 1080);
        foreach (var l in second)
        {
            var a = first[l.Window.Handle];
            Assert.Equal((a.X, a.Y, a.Width, a.Height), (l.X, l.Y, l.Width, l.Height));
        }
    }

    [Fact]
    public void SeparatedWindowsRetainTheirOriginalCenters()
    {
        var layouts = Calculate([Win(1, 50, 100, 400, 300), Win(2, 1300, 650, 400, 300)]);
        var left = layouts.Single(l => l.Window.Handle == new IntPtr(1));
        var right = layouts.Single(l => l.Window.Handle == new IntPtr(2));
        Assert.Equal(250, left.X + left.Width / 2, 6);
        Assert.Equal(250, left.Y + left.Height / 2, 6);
        Assert.Equal(1500, right.X + right.Width / 2, 6);
        Assert.Equal(800, right.Y + right.Height / 2, 6);
    }

    [Fact]
    public void SmallOverlapIsAllowedWithoutMovingOriginalCenters()
    {
        var layouts = Calculate([Win(1, 100, 100, 800, 600), Win(2, 490, 100, 800, 600)]);
        var left = layouts.Single(l => l.Window.Handle == new IntPtr(1));
        Assert.Equal(500, left.X + left.Width / 2, 6);
        var right = layouts.Single(l => l.Window.Handle == new IntPtr(2));
        Assert.Equal(890, right.X + right.Width / 2, 6);
        VerifyGeometry(layouts, 1920, 1080);
    }

    [Fact]
    public void EmptyInputReturnsEmpty() => Assert.Empty(Calculate([]));

    [Fact]
    public void MovingAndResizingSameHandleUpdatesNextLayout()
    {
        var calculator = new WindowLayoutCalculator();
        var first = calculator.CalculateLayouts([Win(1, 0, 0, 400, 300)], 1920, 1080).Single();
        var second = calculator.CalculateLayouts([Win(1, 900, 500, 800, 600)], 1920, 1080).Single();
        Assert.Equal(400, second.Width, 6);
        Assert.Equal(300, second.Height, 6);
        Assert.Equal(1300, second.X + second.Width / 2, 6);
        Assert.Equal(800, second.Y + second.Height / 2, 6);
        Assert.NotEqual(first.X, second.X);
    }

    [Fact]
    public void SingleWindowStaysCenteredOnItsOriginalPosition()
    {
        var layout = Calculate([Win(1, 600, 300, 400, 200)]).Single();
        Assert.Equal(800, layout.X + layout.Width / 2, 6);
        Assert.Equal(400, layout.Y + layout.Height / 2, 6);
    }

    [Fact]
    public void ExactFitOnFractionalPaddingDoesNotInvertClampBounds()
        => VerifyGeometry(Calculate([Win(1, -380, 108, 1058, 1816)], 3720, 643), 3720, 643);

    [Fact]
    public void RandomizedDesktopGeometryPreservesLayoutInvariants()
    {
        var random = new Random(12345);
        for (int sample = 0; sample < 100; sample++)
        {
            int w = random.Next(320, 4000), h = random.Next(240, 2200);
            var windows = Enumerable.Range(1, random.Next(1, 21)).Select(i => Win(i,
                random.Next(-1000, w + 1000), random.Next(-500, h + 500),
                random.Next(100, 3000), random.Next(100, 2000))).ToList();
            List<WindowLayout> layouts;
            try { layouts = Calculate(windows, w, h); }
            catch (Exception e)
            {
                throw new Exception($"Sample {sample}, screen {w}x{h}: " + string.Join("; ", windows.Select(v =>
                    $"{v.OriginalRect.Left},{v.OriginalRect.Top},{v.OriginalRect.Right - v.OriginalRect.Left},{v.OriginalRect.Bottom - v.OriginalRect.Top}")), e);
            }
            Assert.Equal(windows.Count, layouts.Count);
            Assert.Equal(windows.Count, layouts.Select(l => l.Window.Handle).Distinct().Count());
            VerifyGeometry(layouts, w, h);
        }
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(1080, 1920)]
    [InlineData(3840, 2160)]
    public void OffscreenAndMixedWindowsFitDifferentScreens(double w, double h)
        => VerifyGeometry(Calculate([Win(1, -1000, -500, 4000, 2000), Win(2, 3000, 2000, 100, 200),
            Win(3, 100, 100, 300, 900)], w, h), w, h);
}
