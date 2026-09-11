namespace TravailCommePapa;

/// <summary>
/// Mots simples par lettre, tirés "en roulement" : chaque mot est dit une fois
/// avant qu'un mot ne soit répété (sac mélangé), et jamais deux fois de suite.
/// Règle : 2 syllabes bien distinctes (sauf prénoms imposés et X, sans alternative).
/// </summary>
internal sealed class Words
{
    private static readonly Dictionary<char, string[]> All = new()
    {
        ['A'] = ["Avion", "Abeille", "Agneau", "Adèle", "Agathe"],
        ['B'] = ["Ballon", "Bébé", "Bateau", "Bisou", "Banane", "Bonbon"],
        ['C'] = ["Canard", "Cochon", "Camion", "Câlin", "Carotte", "Cadeau", "Céleste"],
        ['D'] = ["Dodo", "Dauphin", "Dragon", "Dessin", "Doudou"],
        ['E'] = ["Étoile", "Écharpe", "Échelle", "Élodie", "Éléphant", "Écureuil"],
        ['F'] = ["Fourmi", "Fusée", "Fromage", "Facteur"],
        ['G'] = ["Gâteau", "Girafe", "Guitare", "Gabriel", "Garage"],
        ['H'] = ["Hibou", "Hubert"],
        ['I'] = ["Igloo", "Italie", "Inès"],
        ['J'] = ["Jardin", "Jambon", "Jouet"],
        ['K'] = ["Kiwi", "Koala", "Kangourou"],
        ['L'] = ["Lapin", "Lézard", "Lion"],
        ['M'] = ["Mouton", "Moto", "Maison", "Maman", "Mamie", "Marius"],
        ['N'] = ["Nuage", "Navet", "Nager"],
        ['O'] = ["Oiseau", "Orange", "Oreille"],
        ['P'] = ["Poisson", "Panda", "Papa", "Pépé", "Pelleteuse"],
        ['Q'] = ["Quatre", "Question"],
        ['R'] = ["Renard", "Robot", "Requin", "Raisin"],
        ['S'] = ["Soleil", "Souris", "Sapin", "Stella"],
        ['T'] = ["Tracteur", "Tomate", "Tortue", "Tonton Gérémy", "Tonton Célestin", "Tata Pauline"],
        ['U'] = ["Ustensile", "Utile", "Unique"],
        ['V'] = ["Vélo", "Voiture", "Valise"],
        ['W'] = ["Wagon"],
        ['X'] = ["Xylophone"],
        ['Y'] = ["Yaourt", "Les Yeux"],
        ['Z'] = ["Zéro", "Zèbre", "Zizi"],
    };

    /// <summary>Toutes les combinaisons lettre/mot (pour préparer les voix à l'avance).</summary>
    public static IEnumerable<(char Letter, string Word)> Pairs =>
        All.SelectMany(kv => kv.Value.Select(w => (kv.Key, w)));

    private readonly Random _rng = new();
    private readonly Dictionary<char, Queue<string>> _bags = new();
    private readonly Dictionary<char, string> _last = new();

    public string Next(char letter)
    {
        if (!All.TryGetValue(letter, out var words)) return letter.ToString();

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
