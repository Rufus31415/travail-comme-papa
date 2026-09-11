namespace TravailCommePapa;

/// <summary>Phrases prononcées : lues d'une traite, naturellement, sans effet de ton ni pause ajoutée.</summary>
internal static class SpeechScript
{
    private static readonly string[] DigitNames =
        ["zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf"];

    public static string Letter(char letter, string word) => $"{letter} comme {word}";

    public static string Digit(char digit) => DigitNames[digit - '0'];
}
