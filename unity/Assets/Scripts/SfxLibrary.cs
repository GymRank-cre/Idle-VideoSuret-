using System;
using UnityEngine;

public enum SfxId
{
    Pickup, Deposit, ProductReady, Bill, OrderPaid, Unlock, Upgrade, Hire, Click, Refuse
}

// Génère tous les effets sonores par code (formes d'onde simples avec enveloppe) : aucun fichier externe.
// Chaque clip est créé une seule fois (AudioManager.Awake), jamais pendant le jeu.
public static class SfxLibrary
{
    public const int SampleRate = 44100;
    const float TargetRms = 0.10f;   // niveau commun (~ -20 dBFS) pour que les sons soient homogènes
    const float PeakLimit = 0.85f;   // aucune crête au-dessus de 0,9

    enum Wave { Sine, SoftSquare, Triangle }

    public static AudioClip[] CreateAll()
    {
        var clips = new AudioClip[Enum.GetValues(typeof(SfxId)).Length];
        foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            clips[(int)id] = Make(id);
        return clips;
    }

    static AudioClip Make(SfxId id)
    {
        float[] data;
        switch (id)
        {
            case SfxId.Pickup: // pop court : sinus qui monte vite
                data = Buffer(0.10f);
                Sweep(data, 0f, 0.10f, 420f, 900f, 9f);
                break;
            case SfxId.Deposit: // choc sourd : grave + bruit filtré
                data = Buffer(0.20f);
                Sweep(data, 0f, 0.20f, 110f, 60f, 14f);
                Noise(data, 0f, 0.08f, 0.35f, 30f, 0.12f);
                break;
            case SfxId.ProductReady: // ding
                data = Buffer(0.50f);
                Tone(data, 0f, 0.50f, 1318f, 1f, 7f, Wave.Sine);
                Tone(data, 0f, 0.40f, 2637f, 0.3f, 10f, Wave.Sine);
                break;
            case SfxId.Bill: // pièce : deux notes
                data = Buffer(0.26f);
                Tone(data, 0f, 0.10f, 1568f, 1f, 18f, Wave.SoftSquare);
                Tone(data, 0.07f, 0.19f, 2093f, 1f, 14f, Wave.SoftSquare);
                break;
            case SfxId.OrderPaid: // tiroir-caisse : deux tintements
                data = Buffer(0.70f);
                Tone(data, 0f, 0.40f, 1047f, 1f, 8f, Wave.Sine);
                Tone(data, 0f, 0.30f, 2093f, 0.4f, 10f, Wave.Sine);
                Tone(data, 0.14f, 0.50f, 1319f, 1f, 7f, Wave.Sine);
                Tone(data, 0.14f, 0.40f, 2637f, 0.4f, 9f, Wave.Sine);
                Noise(data, 0f, 0.03f, 0.25f, 60f, 0.4f);
                break;
            case SfxId.Unlock: // arpège montant
                data = Buffer(0.62f);
                float[] arp = { 523.25f, 659.25f, 783.99f, 1046.5f };
                for (int i = 0; i < arp.Length; i++)
                    Tone(data, i * 0.09f, 0.30f, arp[i], 1f, 9f, Wave.Triangle);
                break;
            case SfxId.Upgrade: // accord montant
                data = Buffer(0.70f);
                float[] chord = { 392f, 493.88f, 587.33f, 783.99f };
                for (int i = 0; i < chord.Length; i++)
                    Tone(data, i * 0.06f, 0.45f, chord[i], 1f, 6f, Wave.Triangle);
                break;
            case SfxId.Hire: // petite fanfare
                data = Buffer(0.95f);
                float[] melody = { 392f, 392f, 523.25f, 659.25f, 783.99f };
                float[] starts = { 0f, 0.12f, 0.24f, 0.40f, 0.58f };
                float[] lens = { 0.12f, 0.12f, 0.16f, 0.18f, 0.37f };
                for (int i = 0; i < melody.Length; i++)
                    Tone(data, starts[i], lens[i], melody[i], 1f, 3f, Wave.SoftSquare);
                break;
            case SfxId.Click: // tic bref
                data = Buffer(0.05f);
                Tone(data, 0f, 0.05f, 1000f, 1f, 55f, Wave.Sine);
                break;
            default: // Refuse : bip grave en deux impulsions
                data = Buffer(0.30f);
                Tone(data, 0f, 0.12f, 150f, 1f, 8f, Wave.SoftSquare);
                Tone(data, 0.15f, 0.14f, 130f, 1f, 8f, Wave.SoftSquare);
                break;
        }

        Normalize(data);
        AudioClip clip = AudioClip.Create("sfx_" + id, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float[] Buffer(float seconds)
    {
        return new float[Mathf.CeilToInt(seconds * SampleRate)];
    }

    // Note avec attaque courte (2 ms) et décroissance exponentielle.
    static void Tone(float[] data, float start, float length, float freq, float amp, float decay, Wave wave)
    {
        int s0 = Mathf.RoundToInt(start * SampleRate);
        int n = Mathf.Min(Mathf.RoundToInt(length * SampleRate), data.Length - s0);
        float attack = 0.002f * SampleRate;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Mathf.Min(1f, i / attack) * Mathf.Exp(-decay * t);
            data[s0 + i] += amp * env * Osc(wave, freq * t);
        }
    }

    // Glissando de f0 à f1 (sinus).
    static void Sweep(float[] data, float start, float length, float f0, float f1, float decay)
    {
        int s0 = Mathf.RoundToInt(start * SampleRate);
        int n = Mathf.Min(Mathf.RoundToInt(length * SampleRate), data.Length - s0);
        float phase = 0f;
        float attack = 0.002f * SampleRate;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float f = Mathf.Lerp(f0, f1, t / length);
            phase += f / SampleRate;
            float env = Mathf.Min(1f, i / attack) * Mathf.Exp(-decay * t);
            data[s0 + i] += env * Mathf.Sin(2f * Mathf.PI * phase);
        }
    }

    // Bruit blanc filtré passe-bas simple (déterministe : graine fixe).
    static void Noise(float[] data, float start, float length, float amp, float decay, float smooth)
    {
        int s0 = Mathf.RoundToInt(start * SampleRate);
        int n = Mathf.Min(Mathf.RoundToInt(length * SampleRate), data.Length - s0);
        uint seed = 12345u;
        float low = 0f;
        for (int i = 0; i < n; i++)
        {
            seed = seed * 1664525u + 1013904223u;
            float white = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
            low += (white - low) * smooth;
            float t = i / (float)SampleRate;
            data[s0 + i] += amp * low * Mathf.Exp(-decay * t);
        }
    }

    static float Osc(Wave wave, float cycles)
    {
        float frac = cycles - Mathf.Floor(cycles);
        switch (wave)
        {
            case Wave.SoftSquare: // carré adouci : somme des deux premiers harmoniques impairs
                return 0.8f * Mathf.Sin(2f * Mathf.PI * cycles) + 0.25f * Mathf.Sin(6f * Mathf.PI * cycles);
            case Wave.Triangle:
                return 4f * Mathf.Abs(frac - 0.5f) - 1f;
            default:
                return Mathf.Sin(2f * Mathf.PI * cycles);
        }
    }

    // Même niveau efficace pour tous les sons, puis crête limitée à PeakLimit.
    static void Normalize(float[] data)
    {
        double sum = 0;
        float peak = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i] * data[i];
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        if (peak <= 0f) return;

        // l'RMS est calculé sur la partie sonore (hors silence final) pour comparer des sons de durées différentes
        int active = 0;
        for (int i = 0; i < data.Length; i++)
            if (Mathf.Abs(data[i]) > peak * 0.02f) active++;
        float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, active)));
        float gain = TargetRms / rms;
        if (peak * gain > PeakLimit) gain = PeakLimit / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
    }
}
