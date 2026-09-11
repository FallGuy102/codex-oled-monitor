using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CodexOledMonitor;

internal sealed record OledFrame(Bitmap Preview, int[] Bytes, int FiveHourRemaining, int WeeklyRemaining);

internal static class OledRenderer
{
    public static OledFrame Render(UsageSnapshot usage)
    {
        var five = Remaining(usage.FiveHour);
        var week = Remaining(usage.Weekly);
        var bitmap = new Bitmap(128, 40, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var white = new SolidBrush(Color.White);
        using var black = new SolidBrush(Color.Black);
        using var whitePen = new Pen(Color.White, 1);
        using var font = new Font("Segoe UI", 7, FontStyle.Bold, GraphicsUnit.Pixel);

        graphics.Clear(Color.Black);
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        graphics.SmoothingMode = SmoothingMode.None;

        graphics.FillRectangle(white, 0, 0, 128, 10);
        graphics.DrawString("CODEX", font, black, 3, 0);
        if (!string.IsNullOrWhiteSpace(usage.Plan)) graphics.DrawString(usage.Plan, font, black, 45, 0);
        graphics.DrawString("LIVE", font, black, 105, 0);

        DrawRow(graphics, font, white, black, whitePen, "5H", five, 13);
        DrawRow(graphics, font, white, black, whitePen, "7D", week, 27);
        return new OledFrame(bitmap, ToBytes(bitmap), five, week);
    }

    private static void DrawRow(Graphics g, Font font, Brush white, Brush black, Pen whitePen,
        string label, int remaining, int y)
    {
        g.FillRectangle(white, 1, y, 15, 10);
        g.DrawString(label, font, black, 2, y + 1);
        g.DrawRectangle(whitePen, 20, y + 1, 71, 7);
        var width = (int)Math.Floor(69 * remaining / 100d);
        if (width > 0) g.FillRectangle(white, 21, y + 2, width, 6);
        g.DrawString($"{remaining}%", font, white, 96, y + 1);
    }

    private static int Remaining(LimitWindow? window) =>
        window is null ? 0 : Math.Clamp(100 - window.UsedPercent, 0, 100);

    private static int[] ToBytes(Bitmap bitmap)
    {
        var bytes = new int[640];
        for (var y = 0; y < 40; y++)
        for (var byteX = 0; byteX < 16; byteX++)
        {
            var value = 0;
            for (var bit = 0; bit < 8; bit++)
                if (bitmap.GetPixel(byteX * 8 + bit, y).R >= 96) value |= 1 << (7 - bit);
            bytes[y * 16 + byteX] = value;
        }
        return bytes;
    }
}
