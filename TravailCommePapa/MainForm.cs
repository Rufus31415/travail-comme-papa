using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using static TravailCommePapa.NativeMethods;

namespace TravailCommePapa;

/// <summary>
/// Fenêtre unique plein écran, toujours au premier plan.
/// Couches (de bas en haut) : fond + cahier + texte animé, puis dessin à la souris,
/// puis console parent (Alt maintenu).
/// </summary>
internal sealed partial class MainForm : Form
{
    /// <summary>Durée d'appui sur Alt avant l'apparition de la console parent.</summary>
    private const int AdminHoldMs = 1500;

    private static readonly Color[] LetterPalette =
    [
        Color.FromArgb(239, 83, 80),   // rouge
        Color.FromArgb(255, 152, 0),   // orange
        Color.FromArgb(253, 196, 0),   // jaune
        Color.FromArgb(102, 187, 106), // vert
        Color.FromArgb(38, 166, 154),  // turquoise
        Color.FromArgb(41, 182, 246),  // bleu ciel
        Color.FromArgb(66, 110, 245),  // bleu
        Color.FromArgb(149, 97, 226),  // violet
        Color.FromArgb(236, 64, 150),  // rose
    ];

    private readonly Dictionary<MouseButtons, Color> _mouseColors = new()
    {
        [MouseButtons.Left] = Color.FromArgb(229, 57, 53),    // rouge
        [MouseButtons.Right] = Color.FromArgb(67, 160, 71),   // vert
        [MouseButtons.Middle] = Color.FromArgb(30, 136, 229), // bleu
        [MouseButtons.XButton1] = Color.FromArgb(251, 140, 0),
        [MouseButtons.XButton2] = Color.FromArgb(142, 68, 173),
    };

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly TextModel _text = new();
    private readonly Words _words = new();
    private readonly Speaker _speaker = new();
    private readonly HashSet<Keys> _held = new();
    private readonly List<BlockerForm> _blockers = new();
    private readonly Dictionary<MouseButtons, Cursor> _cursors = new();
    private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 15 };
    private readonly System.Windows.Forms.Timer _guardTimer = new() { Interval = 300 };

    private KeyboardHook? _hook;
    private Cursor _defaultCursor = Cursors.Arrow;
    private bool _allowClose;

    // Dessin
    private MouseButtons _activeButton = MouseButtons.None;
    private Point _lastPoint;

    // Console parent
    private bool _altDown;
    private long _altDownAt;
    private bool _adminVisible;
    private string? _adminStatus;

    // Bulle "A comme Abricot"
    private Glyph? _bubbleGlyph;
    private string _bubbleWord = "";
    private long _bubbleStart = -1;

    private long Now => _clock.ElapsedMilliseconds;

    public MainForm()
    {
        Text = "Travail comme Papa";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen!.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.White;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);

        _speaker.Finished += OnSpeechFinished;
        _animTimer.Tick += (_, _) => OnAnimationTick();
        _guardTimer.Tick += (_, _) => KeepLocked();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        int cursorSize = Math.Clamp(Height * 72 / 1080, 48, 128);
        _defaultCursor = CursorFactory.Create(Color.FromArgb(255, 213, 79), cursorSize);
        foreach (var (button, color) in _mouseColors)
            _cursors[button] = CursorFactory.Create(color, cursorSize);
        Cursor = _defaultCursor;

        BuildScene();

        foreach (var screen in Screen.AllScreens.Where(s => !s.Primary))
        {
            var b = new BlockerForm(this, screen);
            _blockers.Add(b);
            b.Show();
        }

        _hook = new KeyboardHook();
        _hook.KeyEvent += OnHookKey;
        _animTimer.Start();
        _guardTimer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        KeepLocked();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { /* tout est peint dans OnPaint */ }

    protected override void OnPaint(PaintEventArgs e) => RenderScene(e.Graphics, Now, export: false);

    // ------------------------------------------------------------------ Verrouillage

    private void KeepLocked()
    {
        if (_allowClose || IsDisposed) return;

        if (GetForegroundWindow() != Handle)
        {
            IntPtr fg = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
            uint me = GetCurrentThreadId();
            if (fgThread != me && fgThread != 0)
            {
                AttachThreadInput(me, fgThread, true);
                SetForegroundWindow(Handle);
                BringWindowToTop(Handle);
                AttachThreadInput(me, fgThread, false);
            }
            else
            {
                SetForegroundWindow(Handle);
            }
            Activate();
        }

        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        foreach (var b in _blockers)
            SetWindowPos(b.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

        Cursor.Clip = Bounds; // la souris ne peut pas quitter l'écran principal
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (!_allowClose) BeginInvoke(KeepLocked);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason is CloseReason.UserClosing or CloseReason.None)
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _animTimer.Stop();
        _guardTimer.Stop();
        _hook?.Dispose();
        _speaker.Dispose();
        Cursor.Clip = Rectangle.Empty;
        foreach (var b in _blockers) { b.Dispose(); }
        SystemGuard.Restore();
        base.OnFormClosed(e);
    }

    // ------------------------------------------------------------------ Clavier

    private void OnHookKey(Keys key, bool down)
    {
        if (key is Keys.LMenu or Keys.RMenu or Keys.Menu)
        {
            if (down && !_altDown)
            {
                _altDown = true;
                _altDownAt = Now;
            }
            else if (!down)
            {
                _altDown = false;
                if (_adminVisible)
                {
                    _adminVisible = false;
                    _adminStatus = null;
                    Invalidate();
                }
            }
            return;
        }

        if (!down)
        {
            _held.Remove(key);
            return;
        }
        bool repeat = !_held.Add(key);

        if (_adminVisible)
        {
            if (!repeat) HandleAdminKey(key);
            return;
        }

        switch (key)
        {
            case >= Keys.A and <= Keys.Z:
                if (!repeat) TypeLetterOrDigit((char)('A' + (key - Keys.A)));
                break;
            case >= Keys.D0 and <= Keys.D9:
                if (!repeat) TypeLetterOrDigit((char)('0' + (key - Keys.D0)));
                break;
            case >= Keys.NumPad0 and <= Keys.NumPad9:
                if (!repeat) TypeLetterOrDigit((char)('0' + (key - Keys.NumPad0)));
                break;
            case Keys.Enter:
                InsertSilent('\n');
                break;
            case Keys.Space:
                InsertSilent(' ');
                break;
            case Keys.Back:
                _text.Backspace();
                AfterTextChange();
                break;
            case Keys.Delete:
                _text.Delete();
                AfterTextChange();
                break;
            case Keys.Left: _text.Left(); AfterTextChange(); break;
            case Keys.Right: _text.Right(); AfterTextChange(); break;
            case Keys.Up: _text.Up(); AfterTextChange(); break;
            case Keys.Down: _text.Down(); AfterTextChange(); break;
            case Keys.Home: _text.Home(); AfterTextChange(); break;
            case Keys.End: _text.End(); AfterTextChange(); break;
        }
    }

    private void TypeLetterOrDigit(char c)
    {
        long now = Now;
        var palette = LetterPalette;
        int idx = char.IsLetter(c) ? c - 'A' : c - '0' + 4;
        var glyph = new Glyph { Ch = c, Color = palette[idx % palette.Length], BornMs = now, SpeakStartMs = now };
        _text.Insert(glyph);
        AfterTextChange();

        _bubbleGlyph = glyph;
        _bubbleStart = now;
        if (char.IsLetter(c))
        {
            _bubbleWord = _words.Next(c);
            _speaker.SayLetter(c, _bubbleWord, glyph);
        }
        else
        {
            _bubbleWord = "";
            _speaker.SayDigit(c, glyph);
        }

        if (GlyphCenter(glyph) is { } center) Burst(center, glyph.Color, 16, 1f);
    }

    private void InsertSilent(char c)
    {
        _text.Insert(new Glyph { Ch = c, Color = Color.Gray, BornMs = Now });
        AfterTextChange();
    }

    private void AfterTextChange()
    {
        _caretResetAt = Now;
        EnsureCaretVisible();
        Invalidate();
    }

    private void OnSpeechFinished(object tag)
    {
        if (tag is Glyph g && g.SpeakEndMs < 0) g.SpeakEndMs = Now;
        Invalidate();
    }

    // ------------------------------------------------------------------ Console parent

    private void HandleAdminKey(Keys key)
    {
        switch (key)
        {
            case Keys.F4:
                _allowClose = true;
                Close();
                break;
            case Keys.C:
                ClearDrawing();
                _text.Clear();
                _bubbleGlyph = null;
                AfterTextChange();
                _adminStatus = "Tout est effacé ✔";
                break;
            case Keys.D:
                ClearDrawing();
                _adminStatus = "Dessin effacé ✔";
                break;
            case Keys.T:
                _text.Clear();
                _bubbleGlyph = null;
                AfterTextChange();
                _adminStatus = "Texte effacé ✔";
                break;
            case Keys.S:
            case Keys.E:
                _adminStatus = ExportPng();
                break;
        }
        Invalidate();
    }

    private string ExportPng()
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Travail comme Papa");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"dessin_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            using var bmp = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                RenderScene(g, Now, export: true);
            bmp.Save(file, ImageFormat.Png);
            return "Image enregistrée : " + file;
        }
        catch (Exception ex)
        {
            return "Erreur d'enregistrement : " + ex.Message;
        }
    }

    // ------------------------------------------------------------------ Souris = pinceau

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_mouseColors.ContainsKey(e.Button)) return;
        _activeButton = e.Button;
        _lastPoint = e.Location;
        Cursor = _cursors[e.Button];
        DrawSegment(e.Location, e.Location);
        Burst(e.Location, _mouseColors[e.Button], 6, 0.5f);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_activeButton == MouseButtons.None) return;
        if ((MouseButtons & _activeButton) == 0) { _activeButton = MouseButtons.None; Cursor = _defaultCursor; return; }
        DrawSegment(_lastPoint, e.Location);
        _lastPoint = e.Location;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != _activeButton) return;
        // Si un autre bouton est encore enfoncé, on continue avec sa couleur
        var still = _mouseColors.Keys.FirstOrDefault(b => (MouseButtons & b) != 0);
        _activeButton = still;
        _lastPoint = e.Location;
        Cursor = still == MouseButtons.None ? _defaultCursor : _cursors[still];
    }

    private void DrawSegment(Point a, Point b)
    {
        if (_drawLayer == null) return;
        var color = _mouseColors[_activeButton];
        using (var g = Graphics.FromImage(_drawLayer))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(color, _brushSize) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            if (a == b)
            {
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, a.X - _brushSize / 2f, a.Y - _brushSize / 2f, _brushSize, _brushSize);
            }
            else
            {
                g.DrawLine(pen, a, b);
            }
        }
        var r = Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        r.Inflate((int)_brushSize + 2, (int)_brushSize + 2);
        Invalidate(r);
    }

    private void ClearDrawing()
    {
        if (_drawLayer == null) return;
        using var g = Graphics.FromImage(_drawLayer);
        g.Clear(Color.Transparent);
    }

    // ------------------------------------------------------------------ Animation

    private void OnAnimationTick()
    {
        long now = Now;
        if (_altDown && !_adminVisible && now - _altDownAt >= AdminHoldMs)
        {
            _adminVisible = true;
            _held.Clear();
            Invalidate();
        }
        if (IsAnimating(now)) Invalidate();
        else InvalidateCaretIfBlinkChanged(now);
    }
}
