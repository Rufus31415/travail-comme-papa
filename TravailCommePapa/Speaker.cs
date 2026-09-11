using System.Globalization;
using System.Speech.Synthesis;

namespace TravailCommePapa;

/// <summary>Synthèse vocale française. Chaque nouvelle phrase interrompt la précédente.</summary>
internal sealed class Speaker : IDisposable
{
    private readonly SpeechSynthesizer? _synth;
    private readonly Dictionary<Prompt, object> _pending = new();

    /// <summary>Levé (sur le thread UI) quand la phrase associée au tag est terminée ou interrompue.</summary>
    public event Action<object>? Finished;

    public Speaker()
    {
        try
        {
            _synth = new SpeechSynthesizer();
            _synth.SetOutputToDefaultAudioDevice();
            var fr = _synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .FirstOrDefault(v => v.VoiceInfo.Culture.TwoLetterISOLanguageName == "fr");
            if (fr != null) _synth.SelectVoice(fr.VoiceInfo.Name);
            _synth.Rate = -1;   // un peu plus lent, pour les petites oreilles
            _synth.Volume = 100;
            _synth.SpeakCompleted += (_, e) =>
            {
                if (_pending.Remove(e.Prompt, out var tag)) Finished?.Invoke(tag);
            };
        }
        catch
        {
            _synth = null; // pas de voix disponible : l'app fonctionne quand même, en silence
        }
    }

    /// <summary>"A comme Abricot" : la lettre est épelée, puis le mot est dit.</summary>
    public void SayLetter(char letter, string word, object tag)
    {
        var pb = new PromptBuilder(new CultureInfo("fr-FR"));
        pb.AppendTextWithHint(letter.ToString(), SayAs.SpellOut);
        pb.AppendText(" comme " + word);
        Say(pb, tag);
    }

    public void SayDigit(char digit, object tag)
    {
        var pb = new PromptBuilder(new CultureInfo("fr-FR"));
        pb.AppendText(digit.ToString());
        Say(pb, tag);
    }

    private void Say(PromptBuilder pb, object tag)
    {
        if (_synth == null)
        {
            // Pas de voix : on simule une durée pour garder l'animation
            var t = new System.Windows.Forms.Timer { Interval = 900 };
            t.Tick += (_, _) => { t.Dispose(); Finished?.Invoke(tag); };
            t.Start();
            return;
        }
        _synth.SpeakAsyncCancelAll(); // les prompts annulés déclenchent SpeakCompleted
        var prompt = new Prompt(pb);
        _pending[prompt] = tag;
        _synth.SpeakAsync(prompt);
    }

    public void Dispose()
    {
        try { _synth?.SpeakAsyncCancelAll(); } catch { }
        _synth?.Dispose();
    }
}
