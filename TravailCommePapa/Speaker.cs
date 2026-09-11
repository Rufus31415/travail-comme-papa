using System.Media;
using System.Text;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using SapiSynthesizer = System.Speech.Synthesis.SpeechSynthesizer;
using WinSynthesizer = Windows.Media.SpeechSynthesis.SpeechSynthesizer;

namespace TravailCommePapa;

/// <summary>
/// Synthèse vocale française avec les voix "OneCore" de Windows (bien plus naturelles que SAPI).
/// Toutes les phrases sont préparées en mémoire au démarrage : aucune latence à l'appui d'une touche.
/// Chaque nouvelle phrase interrompt la précédente. Repli sur l'ancienne voix SAPI si besoin.
/// </summary>
internal sealed class Speaker : IDisposable
{
    /// <summary>Voix préférées, dans l'ordre.</summary>
    private static readonly string[] PreferredVoices = ["Julie", "Hortense", "Paul"];

    /// <summary>Débit de la voix (1 = normal, 0.5 = deux fois plus lent).</summary>
    private const double SpeakingRate = 0.8;

    private readonly WinSynthesizer? _synth;
    private readonly SapiSynthesizer? _sapi;
    private readonly Dictionary<string, Task<byte[]>> _cache = new();
    private readonly SoundPlayer _player = new();
    private readonly System.Windows.Forms.Timer _endTimer = new();
    private System.Speech.Synthesis.Prompt? _sapiPrompt;
    private object? _currentTag;
    private int _seq;

    /// <summary>Levé (sur le thread UI) quand la phrase associée au tag est terminée ou interrompue.</summary>
    public event Action<object>? Finished;

    public Speaker()
    {
        // Fin naturelle : on ne coupe pas le son, on signale seulement la fin (pour l'animation)
        _endTimer.Tick += (_, _) => FinishCurrent(stopAudio: false);
        try
        {
            var voices = WinSynthesizer.AllVoices
                .Where(v => v.Language.StartsWith("fr", StringComparison.OrdinalIgnoreCase)).ToList();
            if (voices.Count > 0)
            {
                var voice = PreferredVoices
                    .Select(n => voices.FirstOrDefault(v => v.DisplayName.Contains(n, StringComparison.OrdinalIgnoreCase)))
                    .FirstOrDefault(v => v != null) ?? voices[0];
                _synth = new WinSynthesizer { Voice = voice };
                try { _synth.Options.SpeakingRate = SpeakingRate; } catch { }
            }
        }
        catch
        {
            _synth = null;
        }

        if (_synth != null)
        {
            _ = PrewarmAsync();
            return;
        }

        try
        {
            _sapi = new SapiSynthesizer();
            _sapi.SetOutputToDefaultAudioDevice();
            var fr = _sapi.GetInstalledVoices().FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == "fr");
            if (fr != null) _sapi.SelectVoice(fr.VoiceInfo.Name);
            _sapi.SpeakCompleted += (_, e) => { if (e.Prompt == _sapiPrompt) FinishCurrent(stopAudio: false); };
            _sapi.Rate = -2;
        }
        catch
        {
            _sapi = null; // pas de voix du tout : l'app fonctionne quand même, en silence
        }
    }

    public void SayLetter(char letter, string word, object tag) => Say(SpeechScript.Letter(letter, word), tag);

    public void SayDigit(char digit, object tag) => Say(SpeechScript.Digit(digit), tag);

    private async void Say(string text, object tag)
    {
        FinishCurrent();
        int seq = ++_seq;
        _currentTag = tag;

        if (_synth == null)
        {
            SayWithSapi(text);
            return;
        }

        byte[] wav;
        try { wav = await GetAudio(text); }
        catch { wav = []; }
        if (seq != _seq) return; // une autre touche a été pressée entre-temps

        if (wav.Length == 0)
        {
            StartEndTimer(800);
            return;
        }
        try
        {
            _player.Stream = new MemoryStream(wav);
            _player.Play();
        }
        catch { }
        // Marge : le son démarre un peu après Play(), l'animation ne doit pas finir avant la voix
        StartEndTimer(WavDurationMs(wav) + 250);
    }

    private void SayWithSapi(string text)
    {
        if (_sapi == null)
        {
            StartEndTimer(900);
            return;
        }
        _sapi.SpeakAsyncCancelAll();
        _sapiPrompt = new System.Speech.Synthesis.Prompt(text);
        _sapi.SpeakAsync(_sapiPrompt);
    }

    private void StartEndTimer(int ms)
    {
        _endTimer.Stop();
        _endTimer.Interval = Math.Max(1, ms);
        _endTimer.Start();
    }

    /// <summary>Signale la fin de la phrase en cours ; l'arrête si elle est interrompue par une autre.</summary>
    private void FinishCurrent(bool stopAudio = true)
    {
        _endTimer.Stop();
        if (stopAudio) try { _player.Stop(); } catch { }
        var tag = _currentTag;
        _currentTag = null;
        if (tag != null) Finished?.Invoke(tag);
    }

    // ------------------------------------------------------------------ Synthèse + cache

    private async Task PrewarmAsync()
    {
        await Task.Delay(1000);
        foreach (var d in "0123456789")
            await TryGet(SpeechScript.Digit(d));
        foreach (var (letter, word) in Words.Pairs)
            await TryGet(SpeechScript.Letter(letter, word));

        async Task TryGet(string text)
        {
            try { await GetAudio(text); } catch { }
        }
    }

    private Task<byte[]> GetAudio(string text)
    {
        if (!_cache.TryGetValue(text, out var task) || task.IsFaulted)
        {
            task = SynthesizeAsync(text);
            _cache[text] = task;
        }
        return task;
    }

    private async Task<byte[]> SynthesizeAsync(string text)
    {
        using var stream = await _synth!.SynthesizeTextToStreamAsync(text);
        uint size = (uint)stream.Size;
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    /// <summary>Durée d'un fichier WAV PCM, lue dans son en-tête.</summary>
    private static int WavDurationMs(byte[] wav)
    {
        try
        {
            int pos = 12, byteRate = 0;
            while (pos + 8 <= wav.Length)
            {
                string id = Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                if (id == "fmt ") byteRate = BitConverter.ToInt32(wav, pos + 16);
                else if (id == "data" && byteRate > 0)
                    return (int)(Math.Min(size, wav.Length - pos - 8) * 1000L / byteRate);
                pos += 8 + size + (size & 1);
            }
        }
        catch { }
        return 1000;
    }

    public void Dispose()
    {
        _endTimer.Dispose();
        try { _player.Stop(); } catch { }
        _player.Dispose();
        _synth?.Dispose();
        try { _sapi?.SpeakAsyncCancelAll(); } catch { }
        _sapi?.Dispose();
    }
}
