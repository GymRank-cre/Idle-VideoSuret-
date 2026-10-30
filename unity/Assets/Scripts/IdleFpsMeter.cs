using UnityEngine;

// Images par seconde des 10 dernières secondes : la moyenne et le pire 1 % des images. C'est le seul chiffre qui dit si le jeu tient sur un vrai téléphone ; il s'affiche dans les Réglages.
public class IdleFpsMeter : MonoBehaviour
{
    const int Capacity = 1200;   // 10 s à 120 images par seconde

    public static float Average { get; private set; }
    public static float Worst { get; private set; }

    [SerializeField, Min(1f)] float windowSeconds = 10f;
    [SerializeField, Min(0.1f)] float refreshEvery = 1f;

    readonly float[] durations = new float[Capacity];
    readonly float[] sorted = new float[Capacity];
    int next, count;
    float sum, timer;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        while (count > 0 && (sum + dt > windowSeconds || count == Capacity))
        {
            int oldest = (next - count + Capacity) % Capacity;
            sum -= durations[oldest];
            count--;
        }
        durations[next] = dt;
        next = (next + 1) % Capacity;
        count++;
        sum += dt;

        timer -= dt;
        if (timer > 0f) return;
        timer = refreshEvery;
        float average, worst;
        Stats(durations, next, count, sorted, out average, out worst);
        Average = average;
        Worst = worst;
    }

    // `count` dernières durées d'image d'un tableau circulaire (`next` : prochaine case) : images/s moyennes, et images/s du pire 1 % des images.
    public static void Stats(float[] durations, int next, int count, float[] sorted, out float average, out float worst)
    {
        average = worst = 0f;
        if (count == 0) return;
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            float d = durations[(next - 1 - i + durations.Length) % durations.Length];
            sorted[i] = d;
            total += d;
        }
        System.Array.Sort(sorted, 0, count);
        average = count / total;
        worst = 1f / sorted[Mathf.Clamp(Mathf.CeilToInt(count * 0.99f) - 1, 0, count - 1)];
    }
}
