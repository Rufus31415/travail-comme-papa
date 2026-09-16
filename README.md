# Travail comme Papa

Application Windows plein écran pour les enfants de 2-3 ans. Elle leur fait découvrir le clavier et la souris d'un vrai PC, sans qu'ils puissent en sortir.

## Ce que fait l'enfant

| Action | Résultat |
|---|---|
| Clic gauche / droit / molette | Dessine en **rouge** / **vert** / **bleu** (boutons latéraux : orange / violet) |
| Lettre | Écrite en **MAJUSCULE** (même si Verr. Maj est actif). La voix dit « A comme Abricot » pendant que la lettre grossit, danse et brille. Une bulle en bas affiche le mot |
| Chiffre (rangée du haut ou pavé numérique) | Le chiffre est prononcé, et la bulle montre autant de ronds que sa valeur |
| Entrée / Espace | Retour à la ligne / espace |
| Retour arrière / Suppr | Efface avant / après le curseur |
| Flèches, Début, Fin | Déplacent le curseur de texte (la souris sert uniquement à dessiner) |

Les mots changent à chaque fois (roulement). Tous les mots d'une lettre passent avant qu'un mot ne revienne. Pour les modifier, voir [TravailCommePapa/Words.cs](TravailCommePapa/Words.cs).

Une touche maintenue enfoncée n'écrit sa lettre qu'une seule fois.

## Console parent

**Maintenir ALT environ 1,5 s.** Un cercle de progression apparaît en bas à droite, puis la console s'ouvre. Toujours en tenant ALT, appuyer sur :

- **F4** : quitter
- **C** : tout effacer (dessin + texte)
- **D** : effacer le dessin
- **T** : effacer le texte
- **S** : enregistrer une image PNG dans `Images\Travail comme Papa\`
- **M** : couper / rétablir le son du PC
- **V** : remettre le volume du PC à 30 %

Relâcher ALT ferme la console. Le délai se règle avec `AdminHoldMs`, et le niveau de la touche **V** avec `AdminVolumeLevel`, dans [MainForm.cs](TravailCommePapa/MainForm.cs). Les touches multimédia étant bloquées, **M** et **V** sont le seul moyen d'agir sur le volume sans quitter l'application.

## Verrouillage : ce qui est bloqué

- **Toutes** les touches sont interceptées par un hook clavier bas niveau avant Windows : touche Windows, Alt+Tab, Alt+Échap, Ctrl+Échap, Alt+F4, Ctrl+Maj+Échap, touche Menu, touches multimédia (volume/muet)…
- Fenêtre sans bordure, plein écran, au-dessus de tout, qui reprend le premier plan toutes les 300 ms.
- La souris est confinée à l'écran principal. Les écrans secondaires sont recouverts.
- Les popups « Touches rémanentes » (Maj ×5), « Touches filtres » et « Touches bascules » sont désactivées pendant l'utilisation, puis restaurées à la fermeture.
- Le **bouton d'alimentation** est mis sur « Ne rien faire » dans le plan de gestion de l'alimentation : un appui court n'éteint plus le PC. Le réglage d'origine est rendu à la fermeture.
- L'écran ne se met pas en veille.

### Limites (impossible à bloquer par une application)

- **Ctrl+Alt+Suppr** et **Win+L** : gérés directement par Windows. Win+L verrouille simplement la session, sans danger.
- **Gestes du pavé tactile** (3-4 doigts) et **balayages depuis le bord** d'un écran tactile : les désactiver dans *Paramètres > Bluetooth et appareils > Pavé tactile* si besoin.
- **Appui long sur le bouton d'alimentation** (4 s et plus) : c'est une coupure matérielle, hors de portée de Windows. Seul l'appui court est neutralisé.

Le mode kiosque Windows (Accès attribué) demande un compte dédié et une reconnexion. Il n'est donc pas utilisé : après ALT+F4, le PC est immédiatement utilisable.

## Compiler / lancer

Prérequis : SDK .NET 9 (ou plus récent).

```powershell
./build.ps1                        # produit publish\TravailCommePapa.exe
# ou pour développer :
dotnet run --project TravailCommePapa
```

La voix utilise les voix françaises « OneCore » de Windows (Julie en priorité, puis Hortense, puis Paul). L'ordre se change dans `PreferredVoices` de [Speaker.cs](TravailCommePapa/Speaker.cs). Toutes les phrases sont préparées en mémoire au démarrage, ce qui supprime la latence. Si ces voix sont absentes, l'app se replie sur l'ancienne voix SAPI, et sans aucune voix elle fonctionne quand même, en silence.

Les phrases (« B comme Ballon », lues naturellement d'une traite) sont définies dans [SpeechScript.cs](TravailCommePapa/SpeechScript.cs), où `LetterNames` corrige les lettres que la voix écorche (Y se dit « I grec »). Un court silence est ajouté au début de chaque phrase, sans quoi la carte son avale la première consonne (« Zède » devient « ède »).
