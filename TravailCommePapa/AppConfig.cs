using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TravailCommePapa;

/// <summary>
/// Réglages personnalisés (pour l'instant : les mots de chaque lettre), stockés dans
/// TravailCommePapa.Config.json à côté de l'exe. Si l'écriture y est impossible (dossier protégé),
/// le fichier est posé dans %APPDATA%\TravailCommePapa. Au chargement, le plus récent des deux gagne.
/// </summary>
internal sealed class AppConfig
{
    public const string FileName = "TravailCommePapa.Config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // garde les accents lisibles
    };

    /// <summary>Mots par lettre ("A" → ["Avion", ...]). Une lettre absente ou vide utilise les mots par défaut.</summary>
    public Dictionary<string, List<string>> Words { get; set; } = new();

    private static string ExeFile =>
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, FileName);

    private static string UserFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TravailCommePapa", FileName);

    /// <summary>Fichier actuellement utilisé (le plus récent), ou celui où l'enregistrement sera tenté en premier.</summary>
    public static string ActivePath =>
        new[] { ExeFile, UserFile }.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? ExeFile;

    /// <summary>Charge la config ; null si aucun fichier lisible n'existe.</summary>
    public static AppConfig? Load()
    {
        var files = new[] { ExeFile, UserFile }
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc);
        foreach (var file in files)
        {
            try
            {
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(file, Encoding.UTF8), JsonOptions);
            }
            catch { /* fichier illisible ou mal formé : on essaie le suivant */ }
        }
        return null;
    }

    /// <summary>Les mots par lettre, sous la forme attendue par <see cref="TravailCommePapa.Words"/>.</summary>
    public Dictionary<char, List<string>> WordsByLetter()
    {
        var result = new Dictionary<char, List<string>>();
        foreach (var (key, words) in Words)
        {
            if (key.Length != 1 || words == null) continue;
            var list = words.Select(w => w?.Trim() ?? "").Where(w => w.Length > 0).ToList();
            if (list.Count > 0) result[char.ToUpperInvariant(key[0])] = list;
        }
        return result;
    }

    public static AppConfig FromWords(IReadOnlyDictionary<char, List<string>> words) => new()
    {
        Words = words.Where(kv => kv.Value.Count > 0)
                     .OrderBy(kv => kv.Key)
                     .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
    };

    /// <summary>Enregistre à côté de l'exe, sinon dans %APPDATA%. Renvoie le chemin écrit, ou null en cas d'échec.</summary>
    public string? Save()
    {
        string json = JsonSerializer.Serialize(this, JsonOptions);
        foreach (var file in new[] { ExeFile, UserFile })
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, json, new UTF8Encoding(false));
                return file;
            }
            catch { /* dossier protégé, disque plein... : emplacement suivant */ }
        }
        return null;
    }
}
