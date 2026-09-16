namespace TravailCommePapa;

/// <summary>Phrases prononcées : lues d'une traite, naturellement, sans effet de ton ni pause ajoutée.</summary>
internal static class SpeechScript
{
    private static readonly string[] DigitNames =
        ["zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf"];

    /// <summary>Lettres dont la voix ne dit pas le vrai nom : on l'écrit en toutes lettres.</summary>
    private static readonly Dictionary<char, string> LetterNames = new()
    {
        ['Y'] = "I grec", // sinon la voix prononce seulement le son « i »
    };

    public static string Letter(char letter, string word) => $"{LetterName(letter)} comme {word}";

    public static string Digit(char digit) => DigitNames[digit - '0'];

    private static string LetterName(char letter) =>
        LetterNames.TryGetValue(letter, out var name) ? name : letter.ToString();
}
