using System.Drawing.Drawing2D;
using static TravailCommePapa.NativeMethods;

namespace TravailCommePapa;

/// <summary>Fabrique un gros pointeur "flèche classique", coloré et contourné.</summary>
internal static class CursorFactory
{
    // Flèche Windows classique dans une grille 12 x 19
    private static readonly PointF[] Arrow =
    [
        new(0, 0), new(0, 16), new(4, 12.2f), new(7, 18.5f), new(9.6f, 17.3f), new(6.7f, 11.2f), new(11.6f, 11.2f),
    ];

    public static Cursor Create(Color fill, int size)
    {
        float margin = size * 0.06f;
        float scale = (size - 2 * margin) / 19f;
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var path = new GraphicsPath();
            path.AddPolygon(Arrow.Select(p => new PointF(margin + p.X * scale, margin + p.Y * scale)).ToArray());

            // Ombre douce
            using (var shadow = (GraphicsPath)path.Clone())
            using (var m = new Matrix())
            {
                m.Translate(size * 0.035f, size * 0.045f);
                shadow.Transform(m);
                using var sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
                g.FillPath(sb, shadow);
            }

            // Remplissage en dégradé
            var bounds = path.GetBounds();
            using (var brush = new LinearGradientBrush(bounds, Lighten(fill, 0.45f), fill, LinearGradientMode.ForwardDiagonal))
                g.FillPath(brush, path);

            // Reflet
            using (var clip = new Region(path))
            {
                g.SetClip(clip, CombineMode.Replace);
                using var hb = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
                g.FillEllipse(hb, margin - size * 0.25f, margin - size * 0.1f, size * 0.5f, size * 0.55f);
                g.ResetClip();
            }

            // Contour épais
            using var pen = new Pen(Color.FromArgb(40, 30, 60), Math.Max(2f, size * 0.05f)) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, path);
        }

        IntPtr hIcon = bmp.GetHicon();
        GetIconInfo(hIcon, out var info);
        info.fIcon = false;
        info.xHotspot = (int)Math.Round(margin);
        info.yHotspot = (int)Math.Round(margin);
        IntPtr hCursor = CreateIconIndirect(ref info);
        DeleteObject(info.hbmMask);
        DeleteObject(info.hbmColor);
        DestroyIcon(hIcon);
        return hCursor == IntPtr.Zero ? Cursors.Arrow : new Cursor(hCursor);
    }

    public static Color Lighten(Color c, float amount) => Color.FromArgb(c.A,
        (int)(c.R + (255 - c.R) * amount),
        (int)(c.G + (255 - c.G) * amount),
        (int)(c.B + (255 - c.B) * amount));

    public static Color Darken(Color c, float amount) => Color.FromArgb(c.A,
        (int)(c.R * (1 - amount)),
        (int)(c.G * (1 - amount)),
        (int)(c.B * (1 - amount)));
}
