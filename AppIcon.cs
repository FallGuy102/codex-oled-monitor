using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexOledMonitor;

internal static class AppIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create()
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var background = new SolidBrush(Color.FromArgb(24, 28, 36));
        using var ring = new Pen(Color.White, 8.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var accent = new SolidBrush(Color.FromArgb(31, 214, 167));
        using var accentEdge = new Pen(Color.FromArgb(24, 28, 36), 3f);

        graphics.FillEllipse(background, 2, 2, 60, 60);
        graphics.DrawArc(ring, 15, 14, 35, 35, 42, 276);
        graphics.FillEllipse(accent, 43, 43, 15, 15);
        graphics.DrawEllipse(accentEdge, 43, 43, 15, 15);

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
