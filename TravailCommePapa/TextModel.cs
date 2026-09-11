namespace TravailCommePapa;

/// <summary>Un caractère affiché, avec son état d'animation.</summary>
internal sealed class Glyph
{
    public char Ch;
    public Color Color;
    public long BornMs;
    public long SpeakStartMs = -1;
    public long SpeakEndMs = -1;

    public bool Speaking => SpeakStartMs >= 0 && SpeakEndMs < 0;
}

/// <summary>
/// Zone de texte minimaliste : liste de glyphes + position du curseur,
/// avec retour à la ligne automatique (au caractère) et navigation aux flèches.
/// </summary>
internal sealed class TextModel
{
    public readonly List<Glyph> Glyphs = new();
    public int Caret { get; private set; }

    private float _maxWidth = 1000;
    private Func<char, float> _advance = _ => 50;
    private bool _dirty = true;

    // Résultat de la mise en page (X en pixels relatifs à la zone, Line = n° de ligne)
    public float[] X = [];
    public int[] Line = [];
    public float[] CaretX = [0];
    public int[] CaretLine = [0];
    public int LineCount = 1;

    public void Configure(float maxWidth, Func<char, float> advance)
    {
        _maxWidth = maxWidth;
        _advance = advance;
        _dirty = true;
    }

    public void EnsureLayout()
    {
        if (!_dirty) return;
        int n = Glyphs.Count;
        X = new float[n];
        Line = new int[n];
        CaretX = new float[n + 1];
        CaretLine = new int[n + 1];

        float x = 0;
        int line = 0;
        for (int i = 0; i < n; i++)
        {
            var g = Glyphs[i];
            if (g.Ch == '\n')
            {
                CaretX[i] = X[i] = x;
                CaretLine[i] = Line[i] = line;
                line++;
                x = 0;
                continue;
            }
            float a = _advance(g.Ch);
            if (x > 0 && x + a > _maxWidth)
            {
                line++;
                x = 0;
            }
            CaretX[i] = X[i] = x;
            CaretLine[i] = Line[i] = line;
            x += a;
        }
        CaretX[n] = x;
        CaretLine[n] = line;
        LineCount = line + 1;
        _dirty = false;
    }

    public void Insert(Glyph g)
    {
        Glyphs.Insert(Caret, g);
        Caret++;
        _dirty = true;
    }

    public void Backspace()
    {
        if (Caret == 0) return;
        Glyphs.RemoveAt(Caret - 1);
        Caret--;
        _dirty = true;
    }

    public void Delete()
    {
        if (Caret >= Glyphs.Count) return;
        Glyphs.RemoveAt(Caret);
        _dirty = true;
    }

    public void Clear()
    {
        Glyphs.Clear();
        Caret = 0;
        _dirty = true;
    }

    public void Left() { if (Caret > 0) Caret--; }
    public void Right() { if (Caret < Glyphs.Count) Caret++; }

    public void Up() => MoveVertical(-1);
    public void Down() => MoveVertical(+1);

    public void Home()
    {
        EnsureLayout();
        int line = CaretLine[Caret];
        while (Caret > 0 && CaretLine[Caret - 1] == line) Caret--;
    }

    public void End()
    {
        EnsureLayout();
        int line = CaretLine[Caret];
        while (Caret < Glyphs.Count && CaretLine[Caret + 1] == line) Caret++;
    }

    private void MoveVertical(int dir)
    {
        EnsureLayout();
        int target = CaretLine[Caret] + dir;
        if (target < 0) { Caret = 0; return; }
        if (target >= LineCount) { Caret = Glyphs.Count; return; }

        float x = CaretX[Caret];
        int best = Caret;
        float bestDist = float.MaxValue;
        for (int i = 0; i <= Glyphs.Count; i++)
        {
            if (CaretLine[i] != target) continue;
            float d = Math.Abs(CaretX[i] - x);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        Caret = best;
    }
}
