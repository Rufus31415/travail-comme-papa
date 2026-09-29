namespace TravailCommePapa;

/// <summary>
/// Mots simples par lettre, tirés "en roulement" : chaque mot est dit une fois
/// avant qu'un mot ne soit répété (sac mélangé), et jamais deux fois de suite.
/// Règle : 2 syllabes bien distinctes (sauf prénoms imposés et X, sans alternative).
/// </summary>
internal sealed class Words
{
    /// <summary>
    /// Mots par défaut, utilisés quand il n'y a pas de fichier de config (ou pour une lettre laissée vide).
    /// Les prénoms de la famille ne sont volontairement pas ici : ils se saisissent dans l'éditeur de la console parent.
    /// </summary>
    private static readonly Dictionary<char, string[]> Defaults = new()
    {
        ['A'] = ["Avion", "Abeille", "Agneau"],
        ['B'] = ["Ballon", "Bébé", "Bateau", "Bisou", "Banane", "Bonbon"],
        ['C'] = ["Canard", "Cochon", "Camion", "Câlin", "Carotte", "Cadeau"],
        ['D'] = ["Dodo", "Dauphin", "Dragon", "Dessin", "Doudou"],
        ['E'] = ["Étoile", "Écharpe", "Échelle", "Éléphant", "Écureuil"],
        ['F'] = ["Fourmi", "Fusée", "Fromage", "Facteur"],
        ['G'] = ["Gâteau", "Girafe", "Guitare", "Garage"],
        ['H'] = ["Hibou"],
        ['I'] = ["Igloo", "Italie"],
        ['J'] = ["Jardin", "Jambon", "Jouet"],
        ['K'] = ["Kiwi", "Koala", "Kangourou"],
        ['L'] = ["Lapin", "Lézard", "Lion"],
        ['M'] = ["Mouton", "Moto", "Maison", "Maman", "Mamie"],
        ['N'] = ["Nuage", "Navet", "Nager", "Nounours"],
        ['O'] = ["Oiseau", "Orange", "Oreille"],
        ['P'] = ["Poisson", "Panda", "Papa", "Pépé", "Pelleteuse"],
        ['Q'] = ["Quatre", "Question", "Quiche"],
        ['R'] = ["Renard", "Robot", "Requin", "Raisin"],
        ['S'] = ["Soleil", "Souris", "Sapin"],
        ['T'] = ["Tracteur", "Tomate", "Tortue"],
        ['U'] = ["Ustensile", "Utile", "Unique"],
        ['V'] = ["Vélo", "Voiture", "Valise"],
        ['W'] = ["Wagon"],
        ['X'] = ["Xylophone"],
        ['Y'] = ["Yaourt", "Les Yeux"],
        ['Z'] = ["Zéro", "Zèbre", "Zizi"],
    };

    private readonly Dictionary<char, List<string>> _all;

    /// <param name="custom">Mots personnalisés (fichier de config) ; null = mots par défaut.</param>
    public Words(IReadOnlyDictionary<char, List<string>>? custom = null) => _all = Effective(custom);

    /// <summary>
    /// Dictionnaire réellement utilisé pour les 26 lettres : les mots personnalisés d'une lettre,
    /// ou à défaut les mots par défaut. Renvoie une copie modifiable.
    /// </summary>
    public static Dictionary<char, List<string>> Effective(IReadOnlyDictionary<char, List<string>>? custom)
    {
        var result = new Dictionary<char, List<string>>();
        for (char c = 'A'; c <= 'Z'; c++)
        {
            IEnumerable<string> words = custom != null && custom.TryGetValue(c, out var mine) && mine.Count > 0
                ? mine
                : Defaults.GetValueOrDefault(c) ?? [];
            result[c] = [.. words];
        }
        return result;
    }

    /// <summary>Toutes les combinaisons lettre/mot (pour préparer les voix à l'avance).</summary>
    public IEnumerable<(char Letter, string Word)> Pairs =>
        _all.SelectMany(kv => kv.Value.Select(w => (kv.Key, w)));

    private readonly Random _rng = new();
    private readonly Dictionary<char, Queue<string>> _bags = new();
    private readonly Dictionary<char, string> _last = new();

    public string Next(char letter)
    {
        if (!_all.TryGetValue(letter, out var words) || words.Count == 0) return letter.ToString();

        if (!_bags.TryGetValue(letter, out var bag) || bag.Count == 0)
        {
            var shuffled = words.OrderBy(_ => _rng.Next()).ToList();
            // Évite de répéter le dernier mot entendu en début de nouveau sac
            if (shuffled.Count > 1 && _last.TryGetValue(letter, out var last) && shuffled[0] == last)
                (shuffled[0], shuffled[^1]) = (shuffled[^1], shuffled[0]);
            bag = new Queue<string>(shuffled);
            _bags[letter] = bag;
        }

        var word = bag.Dequeue();
        _last[letter] = word;
        return word;
    }
}
