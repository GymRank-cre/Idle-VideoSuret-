using UnityEngine;

// Boucle d'ambiance très douce générée par code : nappe de sinus (la mineur, 4 accords) et petite mélodie lente
// en pentatonique. Les fenêtres se recouvrent en rond : la boucle ne clique pas.
public static class MusicLoop
{
    const int Rate = 22050;
    const float Seconds = 32f;
    const float TargetRms = 0.045f;

    public static AudioClip Create()
    {
        int n = Mathf.RoundToInt(Seconds * Rate);
        var data = new float[n];

        // accords : la mineur, fa, do, sol (4 s chacun, deux fois dans la boucle de 32 s)
        float[][] chords =
        {
            new[] { 110f, 220f, 261.63f, 329.63f },
            new[] { 87.31f, 174.61f, 220f, 261.63f },
            new[] { 130.81f, 196f, 261.63f, 329.63f },
            new[] { 98f, 196f, 246.94f, 293.66f },
        };
        for (int c = 0; c < 8; c++)
        {
            float[] notes = chords[c % 4];
            float start = c * 4f - 0.6f;     // recouvrement de 0,6 s avec l'accord précédent
            AddPad(data, start, 5.2f, notes);
        }

        // mélodie : pentatonique la mineur (la, do, ré, mi, sol), une note toutes les 2 s environ
        float[] pent = { 440f, 523.25f, 587.33f, 659.25f, 783.99f };
        int[] seq = { 0, 2, 3, 1, 4, 3, 2, 0, 3, 4, 2, 1, 0, 2, 3, 4 };
        for (int i = 0; i < seq.Length; i++)
            AddBell(data, i * 2f + 0.5f, pent[seq[i]], 0.35f);

        Normalize(data);
        AudioClip clip = AudioClip.Create("music_ambient", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Nappe : sinus avec enveloppe sin² (aucun départ brutal).
    static void AddPad(float[] data, float start, float length, float[] freqs)
    {
        int n = data.Length;
        int len = Mathf.RoundToInt(length * Rate);
        int s0 = Mathf.RoundToInt(start * Rate);
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float w = Mathf.Sin(Mathf.PI * i / len);
            w *= w;
            float v = 0f;
            for (int f = 0; f < freqs.Length; f++)
                v += Mathf.Sin(2f * Mathf.PI * freqs[f] * t) * (f == 0 ? 0.9f : 0.5f);
            int idx = ((s0 + i) % n + n) % n;
            data[idx] += v * w * 0.25f;
        }
    }

    // Petite cloche douce : sinus + octave faible, décroissance lente.
    static void AddBell(float[] data, float start, float freq, float amp)
    {
        int n = data.Length;
        int len = Mathf.RoundToInt(1.8f * Rate);
        int s0 = Mathf.RoundToInt(start * Rate);
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(1f, i / (0.02f * Rate)) * Mathf.Exp(-2.4f * t);
            float v = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * t);
            data[(s0 + i) % n] += amp * env * v * 0.5f;
        }
    }

    static void Normalize(float[] data)
    {
        double sum = 0;
        float peak = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i] * data[i];
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        float rms = Mathf.Sqrt((float)(sum / data.Length));
        float gain = TargetRms / Mathf.Max(rms, 1e-6f);
        if (peak * gain > 0.6f) gain = 0.6f / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
    }
}
