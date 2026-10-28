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

    void Burst(Vector3 position, Color color)
    {
        GameObject go = burstPool.Get(position, Quaternion.identity);
        var ps = go.GetComponent<ParticleSystem>();

        var main = ps.main;
        main.startColor = color;
        main.startLifetime = config.BurstLifetime;
        main.startSpeed = config.BurstSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(config.BurstSize * 0.7f, config.BurstSize * 1.2f);

        ps.Clear();
        ps.Emit(config.BurstCount);
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
