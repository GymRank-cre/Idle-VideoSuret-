using System.Collections.Generic;
using UnityEngine;

// Fait apparaître le retour d'information : chiffres flottants et éclats de particules.
// Tout vient de pools ; les réglages sont dans FeedbackConfig.
public class FeedbackSpawner : MonoBehaviour
{
    struct ActiveBurst
    {
        public GameObject go;
        public float releaseAt;
    }

    [SerializeField] FeedbackConfig config;
    [SerializeField] GameObject floatingTextPrefab;
    [SerializeField] GameObject burstPrefab;

    const float MergeWindow = 0.25f;

    ObjectPool textPool;
    ObjectPool burstPool;
    FloatingText currentText;
    float lastTextTime;
    readonly List<ActiveBurst> bursts = new List<ActiveBurst>();

    void Awake()
    {
        textPool = new ObjectPool(floatingTextPrefab, new GameObject("FloatingTexts").transform);
        burstPool = new ObjectPool(burstPrefab, new GameObject("Bursts").transform);
    }

    // Une caisse arrive au poste.
    public void CrateArrived(Vector3 position)
    {
        Burst(position, config.CrateBurstColor);
    }

    // Un produit fini apparaît sur le plateau.
    public void ProductReady(Vector3 position)
    {
        Burst(position + Vector3.up * 0.1f, config.FloatColor);
    }

    // Un billet est ramassé : éclat vert et "+valeur" qui monte.
    // Les billets ramassés presque ensemble (moins de 0,25 s) s'additionnent dans un seul chiffre lisible.
    public void BillCollected(Vector3 position, int value)
    {
        Burst(position, config.BillBurstColor);

        if (currentText != null && currentText.gameObject.activeInHierarchy && Time.time - lastTextTime < MergeWindow)
        {
            currentText.AddValue(value);
            lastTextTime = Time.time;
            return;
        }

        GameObject go = textPool.Get(position, Quaternion.identity);
        currentText = go.GetComponent<FloatingText>();
        currentText.Play(value, position, config.FloatColor, config.FloatDuration, config.FloatHeight, textPool.Release);
        lastTextTime = Time.time;
    }

    // Message libre qui monte (« +5 % vitesse »).
    public void Message(Vector3 position, string text)
    {
        GameObject go = textPool.Get(position, Quaternion.identity);
        go.GetComponent<FloatingText>().PlayText(text, position, config.FloatColor, config.FloatDuration * 1.6f, config.FloatHeight, textPool.Release);
    }

    // Étincelles à une position (achat, apparition).
    public void Sparkle(Vector3 position)
    {
        Burst(position, config.FloatColor);
    }

    // Confettis de déblocage : plusieurs éclats colorés plus gros et plus longs.
    public void Confetti(Vector3 position)
    {
        Color[] palette = { new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 1f), new Color(0.5f, 1f, 0.5f), new Color(1f, 0.55f, 0.2f) };
        for (int i = 0; i < palette.Length; i++)
            Burst(position + Vector3.up * 0.6f, palette[i], config.BurstCount * 2, config.BurstSpeed * 1.8f, 1.4f);
    }

    // Petit nuage de poussière sous les pieds en courant.
    public void Dust(Vector3 position)
    {
        Burst(position + Vector3.up * 0.05f, new Color(0.8f, 0.75f, 0.65f, 0.7f), 3, 0.8f, 0.7f);
    }

    void Burst(Vector3 position, Color color)
    {
        Burst(position, color, config.BurstCount, config.BurstSpeed, 1f);
    }

    void Burst(Vector3 position, Color color, int count, float speed, float sizeScale)
    {
        GameObject go = burstPool.Get(position, Quaternion.identity);
        var ps = go.GetComponent<ParticleSystem>();

        var main = ps.main;
        main.startColor = color;
        main.startLifetime = config.BurstLifetime;
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(config.BurstSize * 0.7f * sizeScale, config.BurstSize * 1.2f * sizeScale);

        ps.Clear();
        ps.Emit(count);
        bursts.Add(new ActiveBurst { go = go, releaseAt = Time.time + config.BurstLifetime + 0.2f });
    }

    void Update()
    {
        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            if (Time.time < bursts[i].releaseAt) continue;
            burstPool.Release(bursts[i].go);
            bursts.RemoveAt(i);
        }
    }
}
