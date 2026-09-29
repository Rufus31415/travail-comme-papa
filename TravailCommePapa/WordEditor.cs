using System.Text;
using static TravailCommePapa.NativeMethods;

namespace TravailCommePapa;

/// <summary>
/// Éditeur des mots de chaque lettre (console parent). Toute la saisie passe par le hook clavier,
/// puisque Windows ne reçoit aucune touche : cette classe traduit les touches en actions ou en caractères.
/// </summary>
internal sealed class WordEditor
{
    public const int MaxWordLength = 24;

    /// <summary>Articles tolérés avant le mot (« Les Yeux » pour Y).</summary>
    private static readonly string[] Articles = ["le", "la", "les", "un", "une"];

    public Dictionary<char, List<string>> Words { get; }
    public char Letter { get; private set; } = 'A';
    public int Selected { get; private set; }
    public string Input { get; private set; } = "";
    public string? Status { get; private set; }
    public bool StatusIsError { get; private set; }

    /// <summary>Levé quand l'utilisateur demande à fermer l'éditeur (Échap).</summary>
    public event Action? CloseRequested;

    public WordEditor(Dictionary<char, List<string>> words) => Words = words;

    public List<string> CurrentWords => Words[Letter];

    /// <summary>Vrai après un échec d'enregistrement : un nouvel Échap ferme alors sans enregistrer.</summary>
    public bool SaveFailed { get; private set; }

    public void ReportSaveFailure()
    {
        SaveFailed = true;
        SetStatus("Erreur : enregistrement impossible. Échap encore pour quitter sans enregistrer.", error: true);
    }

    public void HandleKey(Keys key, bool repeat, bool shift, bool capsLock)
    {
        switch (key)
        {
            case Keys.Escape:
                if (!repeat) CloseRequested?.Invoke();
                return;
            case Keys.Left: ChangeLetter(-1); return;
            case Keys.Right: ChangeLetter(+1); return;
            case Keys.Home: SetLetter('A'); return;
            case Keys.End: SetLetter('Z'); return;
            case Keys.Up: Selected = Math.Max(0, Selected - 1); return;
            case Keys.Down: Selected = Math.Max(0, Math.Min(CurrentWords.Count - 1, Selected + 1)); return;
            case Keys.Back:
                if (Input.Length > 0) Input = Input[..^1];
                return;
            case Keys.Enter:
                if (!repeat) AddInput();
                return;
            case Keys.Delete:
                if (!repeat) DeleteSelected();
                return;
            case Keys.F2:
                if (!repeat) EditSelected();
                return;
        }

        if (Input.Length < MaxWordLength && TranslateChar(key, shift, capsLock) is { } c)
        {
            Input += c;
            Status = null;
        }
    }

    private void ChangeLetter(int delta) => SetLetter((char)('A' + (Letter - 'A' + delta + 26) % 26));

    private void SetLetter(char letter)
    {
        Letter = letter;
        Selected = 0;
        Input = "";
        Status = null;
    }

    private void AddInput()
    {
        string word = Input.Trim();
        if (word.Length == 0) return;
        word = char.ToUpper(word[0], System.Globalization.CultureInfo.CurrentCulture) + word[1..];

        if (!StartsWithLetter(word, Letter))
        {
            SetStatus($"« {word} » ne commence pas par {Letter} (articles tolérés : {string.Join(", ", Articles)})", error: true);
            return;
        }

        var words = CurrentWords;
        if (words.Any(w => string.Equals(w, word, StringComparison.CurrentCultureIgnoreCase)))
        {
            SetStatus($"« {word} » existe déjà pour la lettre {Letter}", error: true);
            return;
        }
        words.Add(word);
        Selected = words.Count - 1;
        Input = "";
        SetStatus($"« {word} » ajouté ✔", error: false);
    }

    /// <summary>Le mot (après un éventuel article) commence par la lettre, sans tenir compte des accents ni de la casse.</summary>
    private static bool StartsWithLetter(string word, char letter)
    {
        int space = word.IndexOf(' ');
        if (space > 0 && space < word.Length - 1
            && Articles.Contains(word[..space], StringComparer.CurrentCultureIgnoreCase))
            word = word[(space + 1)..].TrimStart();

        string decomposed = word.Normalize(NormalizationForm.FormD);
        return decomposed.Length > 0 && char.ToUpperInvariant(decomposed[0]) == letter;
    }

    private void DeleteSelected()
    {
        var words = CurrentWords;
        if (Selected < 0 || Selected >= words.Count) return;
        string word = words[Selected];
        words.RemoveAt(Selected);
        Selected = Math.Clamp(Selected, 0, Math.Max(0, words.Count - 1));
        SetStatus($"« {word} » supprimé ✔", error: false);
    }

    /// <summary>Reprend le mot sélectionné dans la ligne de saisie pour le corriger.</summary>
    private void EditSelected()
    {
        var words = CurrentWords;
        if (Selected < 0 || Selected >= words.Count) return;
        Input = words[Selected];
        words.RemoveAt(Selected);
        Selected = Math.Clamp(Selected, 0, Math.Max(0, words.Count - 1));
        Status = null;
    }

    private void SetStatus(string text, bool error)
    {
        Status = text;
        StatusIsError = error;
    }

    /// <summary>Caractère produit par la touche avec la disposition de clavier courante (null si aucun).</summary>
    private static char? TranslateChar(Keys key, bool shift, bool capsLock)
    {
        if (key == Keys.Space) return ' ';

        var state = new byte[256];
        if (shift) state[(int)Keys.ShiftKey] = 0x80;
        if (capsLock) state[(int)Keys.CapsLock] = 0x01;

        uint vk = (uint)key;
        uint scan = MapVirtualKey(vk, 0);
        var buffer = new StringBuilder(8);
        // Drapeau 4 : ne pas modifier l'état des touches mortes du système
        int n = ToUnicodeEx(vk, scan, state, buffer, buffer.Capacity, 4, GetKeyboardLayout(0));
        if (n != 1) return null;
        char c = buffer[0];
        return char.IsControl(c) ? null : c;
    }
}
