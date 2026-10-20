using SkiaSharp;
using System.Security.Cryptography;
using System.Text;

namespace ShinraBot;

static class RankCardRenderer
{
    public static byte[] Render(string username, int level, int xp)
    {
        const int width = 900;
        const int height = 280;
        var progressStart = level * level * 100;
        var progressEnd = (level + 1) * (level + 1) * 100;
        var progress = Math.Clamp((xp - progressStart) / (float)(progressEnd - progressStart), 0, 1);
        var colorSeed = SHA256.HashData(Encoding.UTF8.GetBytes(username));
        var accent = new SKColor(colorSeed[0], colorSeed[1], colorSeed[2]);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(24, 29, 39));

        using var backgroundPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                [new SKColor(37, 49, 62), new SKColor(24, 29, 39)],
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRoundRect(new SKRect(0, 0, width, height), 24, 24, backgroundPaint);

        using var accentPaint = new SKPaint { Color = accent, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(36, 38, 46, 242), 5, 5, accentPaint);

        using var titlePaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 48,
            Typeface = SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold),
            IsAntialias = true
        };
        using var detailPaint = new SKPaint
        {
            Color = new SKColor(183, 194, 208),
            TextSize = 26,
            Typeface = SKTypeface.FromFamilyName("sans-serif"),
            IsAntialias = true
        };

        canvas.DrawText(username, 78, 104, titlePaint);
        canvas.DrawText($"LEVEL {level}", 78, 158, detailPaint);
        canvas.DrawText($"{xp:N0} XP", 78, 202, detailPaint);

        using var trackPaint = new SKPaint { Color = new SKColor(64, 74, 88), IsAntialias = true };
        using var progressPaint = new SKPaint { Color = accent, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(78, 226, 842, 244), 9, 9, trackPaint);
        canvas.DrawRoundRect(new SKRect(78, 226, 78 + 764 * progress, 244), 9, 9, progressPaint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}