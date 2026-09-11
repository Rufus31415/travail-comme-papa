namespace TravailCommePapa;

/// <summary>
/// Mots simples par lettre, tirés "en roulement" : chaque mot est dit une fois
/// avant qu'un mot ne soit répété (sac mélangé), et jamais deux fois de suite.
/// </summary>
internal sealed class Words
{
    private static readonly Dictionary<char, string[]> All = new()
    {
        ['A'] = ["Abricot", "Avion", "Arbre", "Ananas", "Âne", "Adèle", "Agathe"],
        ['B'] = ["Ballon", "Bébé", "Banane", "Bateau", "Bisou", "Biberon", "Bain"],
        ['C'] = ["Canard", "Cochon", "Camion", "Carotte", "Câlin", "Chat", "Céleste"],
        ['D'] = ["Doudou", "Dodo", "Dinosaure", "Dauphin", "Dent", "Dragon"],
        ['E'] = ["Éléphant", "Étoile", "Escargot", "Eau", "Écureuil"],
        ['F'] = ["Fleur", "Fraise", "Fourmi", "Fusée", "Fromage", "Feu"],
        ['G'] = ["Gâteau", "Girafe", "Glace", "Gorille", "Guitare"],
        ['H'] = ["Hibou", "Hélicoptère", "Hérisson", "Herbe", "Hubert"],
        ['I'] = ["Igloo", "Île", "Iguane", "Inès"],
        ['J'] = ["Jus", "Jouet", "Jardin", "Jaune", "Jambe"],
        ['K'] = ["Koala", "Kangourou", "Kiwi", "Kayak"],
        ['L'] = ["Lapin", "Lion", "Lait", "Lune", "Loup", "Lit"],
        ['M'] = ["Maman", "Mamie", "Marius", "Mouton", "Moto", "Maison", "Main"],
        ['N'] = ["Nounours", "Nez", "Nuage", "Neige", "Nid"],
        ['O'] = ["Oiseau", "Orange", "Ours", "Œuf", "Oreille"],
        ['P'] = ["Papa", "Pépé", "Pomme", "Poisson", "Poule", "Pain", "Pied"],
        ['Q'] = ["Quatre", "Queue", "Quille"],
        ['R'] = ["Renard", "Robot", "Requin", "Rouge", "Roue", "Radis"],
        ['S'] = ["Stella", "Soleil", "Souris", "Serpent", "Sable", "Sac", "Sucette"],
        ['T'] = ["Tortue", "Tonton Gérémy", "Tonton Célestin", "Tata Pauline", "Train", "Tracteur", "Tomate", "Tigre"],
        ['U'] = ["Un", "Ukulélé", "Usine"],
        ['V'] = ["Vache", "Vélo", "Voiture", "Vert", "Ver de terre"],
        ['W'] = ["Wagon", "Wapiti", "Wouf wouf"],
        ['X'] = ["Xylophone"],
        ['Y'] = ["Yaourt", "Yoyo", "Yeux"],
        ['Z'] = ["Zèbre", "Zoo", "Zéro"],
    };

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
