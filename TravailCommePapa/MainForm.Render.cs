using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace TravailCommePapa;

/// <summary>Rendu : fond, cahier, lettres animées, bulle "A comme Abricot", particules, console parent.</summary>
internal sealed partial class MainForm
{
    private sealed class GlyphShape
    {
        public GraphicsPath Path = new();
        public RectangleF Bounds;
    }

    private sealed class Particle
    {
        public float X, Y, VX, VY, Size, Rot, VRot;
        public Color Color;
        public long Born;
        public int Life;
        public bool Star;
    }

    private static readonly (string Key, string Label)[] AdminActions =
    [
        ("F4", "Quitter l'application"),
        ("C", "Tout effacer (dessin + texte)"),
        ("D", "Effacer le dessin"),
        ("T", "Effacer le texte"),
        ("S", "Enregistrer une image PNG"),
        ("M", "Couper / rétablir le son"),
        ("V", "Volume à 30 %"),
        ("W", "Modifier les mots de chaque lettre"),
    ];

    private readonly Dictionary<(char, bool), GlyphShape> _shapes = new();
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();

    private Bitmap? _background;
    private Bitmap? _drawLayer;
    private FontFamily _normalFamily = FontFamily.GenericSansSerif;
    private FontFamily _boldFamily = FontFamily.GenericSansSerif;
    private FontStyle _normalStyle = FontStyle.Bold;
    private FontStyle _boldStyle = FontStyle.Bold;
    private Font _commeFont = SystemFonts.DefaultFont;
    private Font _wordFont = SystemFonts.DefaultFont;
    private Font _hintFont = SystemFonts.DefaultFont;
    private Font _adminTitleFont = SystemFonts.DefaultFont;
    private Font _adminFont = SystemFonts.DefaultFont;
    private Font _adminKeyFont = SystemFonts.DefaultFont;

    private float _em, _lineHeight, _brushSize = 12;
    private float _capTop, _capH, _capTopBold, _capHBold;
    private RectangleF _paper, _textRect, _band;
    private int _visibleLines = 1, _scrollLine;
    private long _caretResetAt;
    private bool _lastCaretOn;
    private Rectangle _caretRect;

    // ------------------------------------------------------------------ Mise en place

    private void BuildScene()
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        _em = h / 10f;
        _lineHeight = _em * 1.22f;
        float margin = h * 0.035f;
        float bandH = h * 0.2f;
        _paper = new RectangleF(margin, margin, w - 2 * margin, h - bandH - 2 * margin);
        _band = new RectangleF(margin, _paper.Bottom, w - 2 * margin, h - _paper.Bottom);
        _textRect = RectangleF.FromLTRB(_paper.Left + _em * 0.65f, _paper.Top + _em * 0.2f, _paper.Right - _em * 0.5f, _paper.Bottom - _em * 0.2f);
        _visibleLines = Math.Max(1, (int)(_textRect.Height / _lineHeight));
        _brushSize = Math.Max(8f, h / 70f);

        (_normalFamily, _normalStyle) = PickFamily(("Segoe UI Semibold", FontStyle.Regular), ("Segoe UI", FontStyle.Bold), ("Arial", FontStyle.Bold));
        (_boldFamily, _boldStyle) = PickFamily(("Segoe UI Black", FontStyle.Regular), ("Arial Black", FontStyle.Regular), ("Segoe UI", FontStyle.Bold));

        _shapes.Clear();
        var hn = GetShape('H', false).Bounds;
        var hb = GetShape('H', true).Bounds;
        (_capTop, _capH, _capTopBold, _capHBold) = (hn.Top, hn.Height, hb.Top, hb.Height);

        float bubbleH = _band.Height * 0.78f;
        _commeFont = new Font("Segoe UI", bubbleH * 0.24f, FontStyle.Regular, GraphicsUnit.Pixel);
        _wordFont = new Font(_boldFamily, bubbleH * 0.34f, _boldStyle, GraphicsUnit.Pixel);
        _hintFont = new Font("Segoe UI", h / 75f, FontStyle.Regular, GraphicsUnit.Pixel);
        _adminTitleFont = new Font("Segoe UI", h / 26f, FontStyle.Bold, GraphicsUnit.Pixel);
        _adminFont = new Font("Segoe UI", h / 42f, FontStyle.Regular, GraphicsUnit.Pixel);
        _adminKeyFont = new Font("Segoe UI", h / 46f, FontStyle.Bold, GraphicsUnit.Pixel);

        _text.Configure(_textRect.Width, Advance);

        _drawLayer?.Dispose();
        _drawLayer = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        _background?.Dispose();
        _background = BuildBackground(w, h);
    }

    private static (FontFamily, FontStyle) PickFamily(params (string Name, FontStyle Style)[] candidates)
    {
        foreach (var (name, style) in candidates)
        {
            try
            {
                var f = new FontFamily(name);
                if (f.IsStyleAvailable(style)) return (f, style);
            }
            catch (ArgumentException) { }
        }
        return (FontFamily.GenericSansSerif, FontStyle.Bold);
    }

    private GlyphShape GetShape(char c, bool bold)
    {
        if (_shapes.TryGetValue((c, bold), out var s)) return s;
        s = new GlyphShape();
        s.Path.AddString(c.ToString(), bold ? _boldFamily : _normalFamily, (int)(bold ? _boldStyle : _normalStyle),
            _em, PointF.Empty, StringFormat.GenericTypographic);
        s.Bounds = s.Path.GetBounds();
        _shapes[(c, bold)] = s;
        return s;
    }

    private float Advance(char c) => c switch
    {
        '\n' => 0,
        ' ' => _em * 0.42f,
        _ => GetShape(c, false).Bounds.Width + _em * 0.16f,
    };

    private Bitmap BuildBackground(int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var sky = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                   Color.FromArgb(178, 223, 255), Color.FromArgb(255, 209, 232), LinearGradientMode.Vertical))
            g.FillRectangle(sky, 0, 0, w, h);

        // Bulles de savon et confettis
        var rnd = new Random(7);
        for (int i = 0; i < 28; i++)
        {
            float r = h * (0.015f + (float)rnd.NextDouble() * 0.06f);
            float x = (float)rnd.NextDouble() * w, y = (float)rnd.NextDouble() * h;
            using var b = new SolidBrush(Color.FromArgb(30 + rnd.Next(50), 255, 255, 255));
            using var p = new Pen(Color.FromArgb(90, 255, 255, 255), Math.Max(1.5f, h / 500f));
            g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
            g.DrawEllipse(p, x - r, y - r, 2 * r, 2 * r);
        }
        for (int i = 0; i < 45; i++)
        {
            float r = h * (0.004f + (float)rnd.NextDouble() * 0.005f);
            float x = (float)rnd.NextDouble() * w, y = (float)rnd.NextDouble() * h;
            var c = LetterPalette[rnd.Next(LetterPalette.Length)];
            using var b = new SolidBrush(Color.FromArgb(110, c));
            g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
        }

        // Feuille du cahier
        float radius = _em * 0.35f;
        for (int i = 1; i <= 8; i++)
        {
            var shadowRect = _paper;
            shadowRect.Offset(0, i * 1.3f);
            shadowRect.Inflate(i, i);
            using var sp = RoundRect(shadowRect, radius + i);
            using var sb = new SolidBrush(Color.FromArgb(9, 40, 60, 120));
            g.FillPath(sb, sp);
        }
        using (var paper = RoundRect(_paper, radius))
        {
            using var pb = new SolidBrush(Color.FromArgb(252, 255, 255, 255));
            g.FillPath(pb, paper);
            using var pen = new Pen(Color.FromArgb(205, 222, 245), Math.Max(2f, h / 360f));
            g.DrawPath(pen, paper);
        }

        // Lignes du cahier, alignées sur le pied des lettres
        using (var linePen = new Pen(Color.FromArgb(206, 228, 250), Math.Max(1.5f, h / 540f)))
        {
            for (int k = 0; k < _visibleLines; k++)
            {
                float y = _textRect.Y + k * _lineHeight + _lineHeight / 2 + _capH / 2 + _em * 0.07f;
                g.DrawLine(linePen, _paper.Left + _em * 0.2f, y, _paper.Right - _em * 0.2f, y);
            }
        }
        using (var marginPen = new Pen(Color.FromArgb(255, 176, 196), Math.Max(2f, h / 400f)))
        {
            float x = _textRect.X - _em * 0.25f;
            g.DrawLine(marginPen, x, _paper.Top + _em * 0.12f, x, _paper.Bottom - _em * 0.12f);
        }
        return bmp;
    }

    // ------------------------------------------------------------------ Rendu

    private void RenderScene(Graphics g, long now, bool export)
    {
        if (_background == null || _drawLayer == null) return;

        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImageUnscaled(_background, 0, 0);
        g.CompositingMode = CompositingMode.SourceOver;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        // Texte : les lettres "en vedette" sont dessinées en dernier, par-dessus les autres
        _text.EnsureLayout();
        var featured = new List<(Glyph Glyph, float X, float Y, float H, float Pop)>();
        for (int i = 0; i < _text.Glyphs.Count; i++)
        {
            var gl = _text.Glyphs[i];
            if (gl.Ch is ' ' or '\n') continue;
            int line = _text.Line[i];
            if (line < _scrollLine || line >= _scrollLine + _visibleLines) continue;

            float cx = _textRect.X + _text.X[i] + Advance(gl.Ch) / 2;
            float cy = LineCenterY(line);
            float h = export ? 0 : Highlight(gl, now);
            float pop = export ? 1 : PopScale(gl, now);
            if (h > 0.001f) featured.Add((gl, cx, cy, h, pop));
            else DrawGlyph(g, gl, cx, cy, 0, pop, now);
        }
        foreach (var f in featured) DrawGlyph(g, f.Glyph, f.X, f.Y, f.H, f.Pop, now);

        if (!export)
        {
            DrawCaret(g, now);
            DrawBubble(g, now);
        }

        // Le dessin à la souris recouvre tout le reste
        g.DrawImageUnscaled(_drawLayer, 0, 0);

        if (!export)
        {
            DrawParticles(g, now);
            DrawHint(g, now);
            if (_adminVisible) DrawAdmin(g);
            if (_editor != null) DrawWordEditor(g, _editor);
        }
    }

    private float LineCenterY(int line) => _textRect.Y + (line - _scrollLine) * _lineHeight + _lineHeight / 2;

    /// <summary>Intensité de la mise en avant (0 = normal, ~1 = en vedette pendant qu'elle est prononcée).</summary>
    private static float Highlight(Glyph g, long now)
    {
        if (g.SpeakStartMs < 0) return 0;
        if (g.SpeakEndMs < 0) return BackOut(Math.Min(1f, (now - g.SpeakStartMs) / 260f));
        float peak = BackOut(Math.Min(1f, (g.SpeakEndMs - g.SpeakStartMs) / 260f));
        float t = (now - g.SpeakEndMs) / 450f;
        return t >= 1 ? 0 : peak * (1 - EaseInOut(t));
    }

    private static float PopScale(Glyph g, long now)
    {
        long age = now - g.BornMs;
        return age >= 500 ? 1f : 0.2f + 0.8f * ElasticOut(age / 500f);
    }

    private void DrawGlyph(Graphics g, Glyph gl, float cx, float cy, float h, float pop, long now)
    {
        bool bold = h > 0.25f;
        var shape = GetShape(gl.Ch, bold);
        if (shape.Bounds.Width <= 0) return;

        float scale = pop * (1 + 0.95f * h);
        float k = Math.Min(1f, h);
        float rot = h > 0 ? (float)Math.Sin(now / 110.0) * 7f * k : 0;
        float bob = h > 0 ? (float)Math.Sin(now / 160.0) * _em * 0.05f * k : 0;

        if (h > 0.02f) DrawGlow(g, cx, cy + bob, _em * 0.85f * scale, gl.Color, k);

        var state = g.Save();
        g.TranslateTransform(cx, cy + bob);
        g.RotateTransform(rot);
        g.ScaleTransform(scale, scale);
        g.TranslateTransform(-(shape.Bounds.X + shape.Bounds.Width / 2), -(bold ? _capTopBold + _capHBold / 2 : _capTop + _capH / 2));
        FillLetter(g, shape, gl.Color, bold);
        g.Restore(state);
    }

    private void FillLetter(Graphics g, GlyphShape shape, Color color, bool bold)
    {
        // Ombre portée
        var st = g.Save();
        g.TranslateTransform(_em * 0.035f, _em * 0.05f);
        using (var sb = new SolidBrush(Color.FromArgb(45, 30, 40, 80)))
            g.FillPath(sb, shape.Path);
        g.Restore(st);

        if (bold)
        {
            var b = shape.Bounds;
            b.Inflate(1, 1);
            using var grad = new LinearGradientBrush(b, CursorFactory.Lighten(color, 0.4f), color, LinearGradientMode.Vertical);
            g.FillPath(grad, shape.Path);
        }
        else
        {
            using var brush = new SolidBrush(color);
            g.FillPath(brush, shape.Path);
        }
        using var pen = new Pen(CursorFactory.Darken(color, 0.35f), _em * (bold ? 0.045f : 0.03f)) { LineJoin = LineJoin.Round };
        g.DrawPath(pen, shape.Path);
    }

    private static void DrawGlow(Graphics g, float cx, float cy, float r, Color color, float intensity)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb((int)(170 * intensity), CursorFactory.Lighten(color, 0.3f)),
            SurroundColors = [Color.FromArgb(0, color)],
        };
        g.FillPath(brush, path);
    }

    // ------------------------------------------------------------------ Curseur texte

    private bool CaretOn(long now) => (now - _caretResetAt) / 530 % 2 == 0;

    private void DrawCaret(Graphics g, long now)
    {
        int c = _text.Caret;
        int line = _text.CaretLine[c];
        if (line < _scrollLine || line >= _scrollLine + _visibleLines) return;

        float x = _textRect.X + _text.CaretX[c];
        float cy = LineCenterY(line);
        float w = _em * 0.085f, hgt = _capH * 1.35f;
        var r = new RectangleF(x - w / 2, cy - hgt / 2, w, hgt);
        _caretRect = Rectangle.Round(r);
        _caretRect.Inflate(6, 6);

        _lastCaretOn = CaretOn(now);
        if (!_lastCaretOn) return;
        using var path = RoundRect(r, w / 2);
        using var brush = new LinearGradientBrush(r, Color.FromArgb(149, 97, 226), Color.FromArgb(236, 64, 150), LinearGradientMode.Vertical);
        g.FillPath(brush, path);
    }

    private void InvalidateCaretIfBlinkChanged(long now)
    {
        if (CaretOn(now) != _lastCaretOn) Invalidate(_caretRect);
    }

    private void EnsureCaretVisible()
    {
        _text.EnsureLayout();
        int line = _text.CaretLine[_text.Caret];
        if (line < _scrollLine) _scrollLine = line;
        else if (line >= _scrollLine + _visibleLines) _scrollLine = line - _visibleLines + 1;
        _scrollLine = Math.Clamp(_scrollLine, 0, Math.Max(0, _text.LineCount - _visibleLines));
    }

    private PointF? GlyphCenter(Glyph glyph)
    {
        int i = _text.Glyphs.IndexOf(glyph);
        if (i < 0) return null;
        _text.EnsureLayout();
        int line = _text.Line[i];
        if (line < _scrollLine || line >= _scrollLine + _visibleLines) return null;
        return new PointF(_textRect.X + _text.X[i] + Advance(glyph.Ch) / 2, LineCenterY(line));
    }

    // ------------------------------------------------------------------ Bulle "A comme Abricot"

    private void DrawBubble(Graphics g, long now)
    {
        var gl = _bubbleGlyph;
        if (gl == null) return;

        float age = now - _bubbleStart;
        float s = ElasticOut(Math.Min(1f, age / 450f));
        if (gl.SpeakEndMs >= 0)
        {
            float t = (now - gl.SpeakEndMs - 2500) / 350f;
            if (t >= 1) { _bubbleGlyph = null; return; }
            if (t > 0) s *= 1 - EaseInOut(t);
        }
        if (s <= 0.01f) return;

        float bh = _band.Height * 0.78f;
        var shape = GetShape(gl.Ch, true);
        float ls = bh * 0.7f / _capHBold;
        float letterW = shape.Bounds.Width * ls;
        bool digit = char.IsDigit(gl.Ch);
        int n = digit ? gl.Ch - '0' : 0;
        float dot = bh * 0.26f, dotGap = dot * 0.35f;

        const string comme = "  comme  ";
        SizeF commeSize = g.MeasureString(comme, _commeFont);
        SizeF wordSize = g.MeasureString(_bubbleWord, _wordFont);

        float contentW = letterW + (digit
            ? (n > 0 ? dotGap * 2 + n * dot + (n - 1) * dotGap : 0)
            : commeSize.Width + wordSize.Width);
        float padX = bh * 0.38f;
        float bw = Math.Max(bh * 1.2f, contentW + 2 * padX);
        float fit = Math.Min(1f, _band.Width / bw);

        var state = g.Save();
        g.TranslateTransform(_band.X + _band.Width / 2, _band.Y + _band.Height / 2);
        g.ScaleTransform(s * fit, s * fit);

        var pill = new RectangleF(-bw / 2, -bh / 2, bw, bh);
        var shadowRect = pill;
        shadowRect.Offset(0, bh * 0.06f);
        using (var sp = RoundRect(shadowRect, bh / 2))
        using (var sb = new SolidBrush(Color.FromArgb(45, 40, 40, 90)))
            g.FillPath(sb, sp);
        using (var pp = RoundRect(pill, bh / 2))
        {
            using var wb = new SolidBrush(Color.White);
            g.FillPath(wb, pp);
            using var border = new Pen(CursorFactory.Lighten(gl.Color, 0.25f), bh * 0.045f);
            g.DrawPath(border, pp);
        }

        float x = digit && n == 0 ? -letterW / 2 : pill.X + padX;

        // La lettre, qui se dandine
        var ls2 = g.Save();
        g.TranslateTransform(x + letterW / 2, 0);
        g.RotateTransform((float)Math.Sin(now / 140.0) * 6f);
        g.ScaleTransform(ls, ls);
        g.TranslateTransform(-(shape.Bounds.X + shape.Bounds.Width / 2), -(_capTopBold + _capHBold / 2));
        FillLetter(g, shape, gl.Color, true);
        g.Restore(ls2);
        x += letterW;

        if (!digit)
        {
            using var grey = new SolidBrush(Color.FromArgb(130, 130, 150));
            g.DrawString(comme, _commeFont, grey, x, -commeSize.Height / 2);
            x += commeSize.Width;
            using var wordBrush = new SolidBrush(CursorFactory.Darken(gl.Color, 0.1f));
            g.DrawString(_bubbleWord, _wordFont, wordBrush, x, -wordSize.Height / 2);
        }
        else
        {
            // Autant de ronds que le chiffre : on apprend à compter !
            x += dotGap * 2;
            for (int k = 0; k < n; k++)
            {
                float appear = Math.Clamp((age - 250 - k * 170) / 300f, 0, 1);
                if (appear > 0)
                {
                    float d = dot * ElasticOut(appear);
                    var c = LetterPalette[k % LetterPalette.Length];
                    using var db = new SolidBrush(c);
                    g.FillEllipse(db, x + dot / 2 - d / 2, -d / 2, d, d);
                    using var dp = new Pen(CursorFactory.Darken(c, 0.3f), dot * 0.07f);
                    g.DrawEllipse(dp, x + dot / 2 - d / 2, -d / 2, d, d);
                }
                x += dot + dotGap;
            }
        }
        g.Restore(state);
    }

    // ------------------------------------------------------------------ Particules (étoiles)

    private void Burst(PointF p, Color color, int count, float power)
    {
        long now = Now;
        for (int k = 0; k < count; k++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            float speed = _em * (2.5f + (float)_rng.NextDouble() * 4f) * power;
            _particles.Add(new Particle
            {
                X = p.X,
                Y = p.Y,
                VX = (float)Math.Cos(angle) * speed,
                VY = (float)Math.Sin(angle) * speed - _em * 2f * power,
                Size = _em * (0.12f + (float)_rng.NextDouble() * 0.14f) * (0.5f + power * 0.5f),
                Rot = (float)_rng.NextDouble() * 360,
                VRot = ((float)_rng.NextDouble() - 0.5f) * 720,
                Color = _rng.NextDouble() < 0.5 ? color : LetterPalette[_rng.Next(LetterPalette.Length)],
                Born = now,
                Life = 700 + _rng.Next(600),
                Star = _rng.NextDouble() < 0.7,
            });
        }
        Invalidate();
    }

    private void DrawParticles(Graphics g, long now)
    {
        _particles.RemoveAll(p => now - p.Born >= p.Life);
        float gravity = _em * 9f;
        foreach (var p in _particles)
        {
            float age = now - p.Born;
            float t = age / 1000f;
            float a = 1 - age / p.Life;
            float x = p.X + p.VX * t;
            float y = p.Y + p.VY * t + 0.5f * gravity * t * t;
            float size = p.Size * (0.6f + 0.4f * a);
            using var brush = new SolidBrush(Color.FromArgb((int)(255 * a), p.Color));
            if (p.Star)
            {
                var st = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform(p.Rot + p.VRot * t);
                using var star = StarPath(size);
                g.FillPath(brush, star);
                g.Restore(st);
            }
            else
            {
                g.FillEllipse(brush, x - size / 2, y - size / 2, size, size);
            }
        }
    }

    private static GraphicsPath StarPath(float r)
    {
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double ang = -Math.PI / 2 + i * Math.PI / 5;
            float rr = i % 2 == 0 ? r : r * 0.45f;
            pts[i] = new PointF((float)Math.Cos(ang) * rr, (float)Math.Sin(ang) * rr);
        }
        var path = new GraphicsPath();
        path.AddPolygon(pts);
        return path;
    }

    // ------------------------------------------------------------------ Indice parent + console

    private void DrawHint(Graphics g, long now)
    {
        const string hint = "Parent : maintenir ALT";
        var size = g.MeasureString(hint, _hintFont);
        float x = ClientSize.Width - size.Width - _em * 0.3f;
        float y = ClientSize.Height - size.Height - _em * 0.15f;
        using var brush = new SolidBrush(Color.FromArgb(110, 60, 60, 100));
        g.DrawString(hint, _hintFont, brush, x, y);

        if (AdminPending)
        {
            float p = Math.Clamp((now - _altDownAt) / (float)AdminHoldMs, 0, 1);
            float r = size.Height * 0.4f;
            var rect = new RectangleF(x - r * 2.6f, y + size.Height / 2 - r, 2 * r, 2 * r);
            using var bg = new Pen(Color.FromArgb(60, 60, 60, 100), r * 0.35f);
            using var fg = new Pen(Color.FromArgb(149, 97, 226), r * 0.35f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawEllipse(bg, rect);
            if (p > 0) g.DrawArc(fg, rect, -90, 360 * p);
        }
    }

    private void DrawAdmin(Graphics g)
    {
        using (var veil = new SolidBrush(Color.FromArgb(165, 20, 20, 40)))
            g.FillRectangle(veil, ClientRectangle);

        float pad = _em * 0.45f;
        float rowH = _adminFont.Height * 1.9f;
        float titleH = _adminTitleFont.Height * 1.5f;
        float footerH = _adminFont.Height * 1.4f;
        float statusH = _adminStatus != null ? _adminFont.Height * 2.6f : 0;
        float cw = Math.Min(ClientSize.Width * 0.9f, _em * 8.5f);
        float ch = pad * 2 + titleH + AdminActions.Length * rowH + footerH + statusH;
        var card = new RectangleF((ClientSize.Width - cw) / 2, (ClientSize.Height - ch) / 2, cw, ch);

        using (var cp = RoundRect(card, _em * 0.25f))
        using (var cb = new SolidBrush(Color.FromArgb(250, 250, 252)))
            g.FillPath(cb, cp);

        float x = card.X + pad, y = card.Y + pad;
        using (var tb = new SolidBrush(Color.FromArgb(60, 40, 110)))
            g.DrawString("Console parent", _adminTitleFont, tb, x, y);
        y += titleH;

        using var textBrush = new SolidBrush(Color.FromArgb(40, 40, 60));
        using var keyBg = new SolidBrush(Color.FromArgb(236, 232, 248));
        using var keyPen = new Pen(Color.FromArgb(160, 140, 210), 2);
        var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        float keyW = _adminKeyFont.Height * 2.6f, keyH = rowH * 0.72f;
        foreach (var (key, label) in AdminActions)
        {
            var kr = new RectangleF(x, y + (rowH - keyH) / 2, keyW, keyH);
            using (var kp = RoundRect(kr, keyH * 0.25f))
            {
                g.FillPath(keyBg, kp);
                g.DrawPath(keyPen, kp);
            }
            g.DrawString(key, _adminKeyFont, textBrush, kr, center);
            var labelSize = g.MeasureString(label, _adminFont);
            g.DrawString(label, _adminFont, textBrush, x + keyW + pad * 0.6f, y + (rowH - labelSize.Height) / 2);
            y += rowH;
        }

        using (var grey = new SolidBrush(Color.FromArgb(120, 120, 140)))
            g.DrawString("Garder ALT enfoncé + touche  •  Relâcher ALT pour revenir", _adminFont, grey, x, y + footerH * 0.15f);
        y += footerH;

        if (_adminStatus != null)
        {
            bool error = _adminStatus.StartsWith("Erreur");
            using var sb = new SolidBrush(error ? Color.FromArgb(200, 40, 40) : Color.FromArgb(46, 125, 50));
            g.DrawString(_adminStatus, _adminFont, sb, new RectangleF(x, y, card.Width - 2 * pad, statusH));
        }
    }

    private void DrawWordEditor(Graphics g, WordEditor ed)
    {
        using (var veil = new SolidBrush(Color.FromArgb(200, 20, 20, 40)))
            g.FillRectangle(veil, ClientRectangle);

        const int MaxRows = 8;
        float pad = _em * 0.4f;
        float rowH = _adminFont.Height * 1.55f;
        float titleH = _adminTitleFont.Height * 1.4f;
        float stripH = _adminFont.Height * 1.6f;
        float inputH = _adminFont.Height * 1.9f;
        float lineH = _adminFont.Height * 1.3f;
        float cw = Math.Min(ClientSize.Width * 0.92f, _em * 10.5f);
        float ch = pad * 2 + titleH + stripH + MaxRows * rowH + pad * 0.6f + inputH + lineH * 2.4f + lineH * 2.4f + lineH * 1.4f;
        var card = new RectangleF((ClientSize.Width - cw) / 2, (ClientSize.Height - ch) / 2, cw, ch);

        using (var cp = RoundRect(card, _em * 0.25f))
        using (var cb = new SolidBrush(Color.FromArgb(250, 250, 252)))
            g.FillPath(cb, cp);

        using var text = new SolidBrush(Color.FromArgb(40, 40, 60));
        using var grey = new SolidBrush(Color.FromArgb(120, 120, 140));
        using var accent = new SolidBrush(Color.FromArgb(60, 40, 110));
        using var accentPen = new Pen(Color.FromArgb(160, 140, 210), 2);
        using var soft = new SolidBrush(Color.FromArgb(236, 232, 248));
        var middle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        float x = card.X + pad, w = card.Width - 2 * pad, y = card.Y + pad;

        // Titre + nombre de mots
        g.DrawString("Mots de la lettre", _adminTitleFont, accent, x, y);
        int count = ed.CurrentWords.Count;
        string countText = count == 0 ? "aucun : mots par défaut" : count == 1 ? "1 mot" : count + " mots";
        var cs = g.MeasureString(countText, _adminFont);
        g.DrawString(countText, _adminFont, grey, x + w - cs.Width, y + (titleH - cs.Height) / 2);
        y += titleH;

        // Bandeau A-Z : lettre courante en violet, lettres sans mot en pâle
        float cell = w / 26f;
        for (int i = 0; i < 26; i++)
        {
            char letter = (char)('A' + i);
            var r = new RectangleF(x + i * cell, y, cell, stripH);
            bool current = letter == ed.Letter;
            if (current)
                using (var p = RoundRect(RectangleF.Inflate(r, -cell * 0.05f, 0), cell * 0.25f))
                using (var b = new SolidBrush(Color.FromArgb(149, 97, 226)))
                    g.FillPath(b, p);
            var brush = current ? Brushes.White : ed.Words[letter].Count == 0 ? grey : text;
            g.DrawString(letter.ToString(), _adminKeyFont, brush, r, middle);
        }
        y += stripH;

        // Liste des mots (fenêtre glissante autour de la sélection)
        var words = ed.CurrentWords;
        int first = Math.Clamp(ed.Selected - MaxRows + 1, 0, Math.Max(0, words.Count - MaxRows));
        for (int row = 0; row < MaxRows; row++)
        {
            var r = new RectangleF(x, y + row * rowH, w, rowH);
            int idx = first + row;
            if (idx >= words.Count) continue;
            if (idx == ed.Selected)
                using (var p = RoundRect(RectangleF.Inflate(r, 0, -rowH * 0.05f), rowH * 0.25f))
                {
                    g.FillPath(soft, p);
                    g.DrawPath(accentPen, p);
                }
            var left = new StringFormat { LineAlignment = StringAlignment.Center };
            g.DrawString(words[idx], _adminFont, text, new RectangleF(r.X + pad * 0.6f, r.Y, r.Width - pad, r.Height), left);
        }
        y += MaxRows * rowH + pad * 0.6f;

        // Ligne de saisie
        var box = new RectangleF(x, y, w, inputH);
        using (var p = RoundRect(box, inputH * 0.25f))
        {
            g.FillPath(Brushes.White, p);
            g.DrawPath(accentPen, p);
        }
        var inputFormat = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        var inner = new RectangleF(box.X + pad * 0.6f, box.Y, box.Width - pad, box.Height);
        if (ed.Input.Length == 0)
            g.DrawString($"Nouveau mot pour {ed.Letter}…", _adminFont, grey, inner, inputFormat);
        else
            g.DrawString(ed.Input + "▏", _adminFont, text, inner, inputFormat);
        y += inputH;

        // Message
        if (ed.Status != null)
        {
            using var sb = new SolidBrush(ed.StatusIsError ? Color.FromArgb(200, 40, 40) : Color.FromArgb(46, 125, 50));
            g.DrawString(ed.Status, _adminFont, sb, new RectangleF(x, y + lineH * 0.15f, w, lineH * 2.2f));
        }
        y += lineH * 2.4f;

        g.DrawString("← → lettre  •  ↑ ↓ mot  •  Entrée ajouter\nSuppr supprimer  •  F2 modifier  •  Échap enregistrer et fermer",
            _adminFont, grey, new RectangleF(x, y, w, lineH * 2.4f));
        y += lineH * 2.4f;

        var pathFormat = new StringFormat { Trimming = StringTrimming.EllipsisPath, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString("Fichier : " + AppConfig.ActivePath, _adminFont, grey, new RectangleF(x, y, w, lineH), pathFormat);
    }

    // ------------------------------------------------------------------ Outils

    private bool IsAnimating(long now)
    {
        if (_particles.Count > 0 || _bubbleGlyph != null || AdminPending) return true;
        foreach (var gl in _text.Glyphs)
        {
            if (now - gl.BornMs < 520) return true;
            if (gl.SpeakStartMs >= 0 && (gl.SpeakEndMs < 0 || now - gl.SpeakEndMs < 480)) return true;
        }
        return false;
    }

    private static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static float ElasticOut(float t)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1);
    }

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1;
        float u = t - 1;
        return 1 + c3 * u * u * u + c1 * u * u;
    }

    private static float EaseInOut(float t) => t < 0.5f ? 2 * t * t : 1 - (float)Math.Pow(-2 * t + 2, 2) / 2;
}
