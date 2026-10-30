using System.Collections.Generic;
using UnityEngine;

// Fait apparaître le retour d'information : chiffres flottants et éclats de particules.
// Tout vient de pools ; les réglages sont dans FeedbackConfig.
public class FeedbackSpawner : MonoBehaviour
{
    struct ActiveBurst
    {
        public GameObject go;
        public ObjectPool pool;
        public float releaseAt;
    }

    [SerializeField] FeedbackConfig config;
    [SerializeField] GameObject floatingTextPrefab;
    [SerializeField] GameObject burstPrefab;
    [SerializeField] Material confettiMaterial;   // rectangles pleins (les éclats, eux, sont des points ronds doux)

    const float MergeWindow = 0.25f;
    const int MaxMessages = 22;
    static readonly Color[] ConfettiColors =
    {
        new Color(1f, 0.85f, 0.2f), new Color(1f, 0.55f, 0.2f), new Color(0.35f, 0.9f, 0.4f),
        new Color(0.3f, 0.65f, 1f), new Color(1f, 0.3f, 0.3f), Color.white
    };

    ObjectPool textPool;
    ObjectPool burstPool;
    ObjectPool confettiPool;   // même prefab, réglé en confettis à chaque tir (taille constante, fondu à la fin)
    FloatingText currentText;
    float lastTextTime;
    int liveMessages;
    System.Action<GameObject> releaseMessage;
    readonly List<ActiveBurst> bursts = new List<ActiveBurst>();

    void Awake()
    {
        textPool = new ObjectPool(floatingTextPrefab, new GameObject("FloatingTexts").transform);
        releaseMessage = go => { liveMessages--; textPool.Release(go); };
        burstPool = new ObjectPool(burstPrefab, new GameObject("Bursts").transform);
        confettiPool = new ObjectPool(burstPrefab, new GameObject("Confetti").transform);
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
        if (liveMessages >= MaxMessages) return;   // jamais plus de 22 « +x » à l'écran
        liveMessages++;
        GameObject go = textPool.Get(position, Quaternion.identity);
        go.GetComponent<FloatingText>().PlayText(text, position, config.MessageColor, config.MessageDuration, config.MessageRise, releaseMessage);
    }

    // Étincelles à une position (achat, apparition).
    public void Sparkle(Vector3 position)
    {
        Burst(position, config.FloatColor);
    }

    // Confettis de déblocage : plusieurs éclats colorés plus gros et plus longs.
    public void Confetti(Vector3 position)
    {
        // 6 couleurs x 10 = 60 particules, 1,4 s : lancées vers le haut, retombent (gravité) en tournant
        for (int i = 0; i < ConfettiColors.Length; i++)
            Burst(position + Vector3.up * 0.6f, ConfettiColors[i], 10, 6.5f, 1.6f, 1.4f, true);
    }

    // Grand nuage de poussière au sol : un poste vient d'arriver.
    public void DustRing(Vector3 position)
    {
        Burst(position + Vector3.up * 0.12f, new Color(0.86f, 0.8f, 0.68f, 0.85f), 28, 2.8f, 2.6f, 0.8f, false);
    }

    // Petit nuage de poussière sous les pieds en courant.
    public void Dust(Vector3 position)
    {
        Burst(position + Vector3.up * 0.05f, new Color(0.8f, 0.75f, 0.65f, 0.7f), 3, 0.8f, 0.7f, config.BurstLifetime, false);
    }

    void Burst(Vector3 position, Color color)
    {
        Burst(position, color, config.BurstCount, config.BurstSpeed, 1f, config.BurstLifetime, false);
    }

    void Burst(Vector3 position, Color color, int count, float speed, float sizeScale, float lifetime, bool confetti)
    {
        ObjectPool pool = confetti ? confettiPool : burstPool;
        GameObject go = pool.Get(position, Quaternion.identity);
        var ps = go.GetComponent<ParticleSystem>();

        var main = ps.main;
        main.startColor = color;
        main.startLifetime = lifetime;
        main.gravityModifier = confetti ? 0.7f : 0f;
        main.startRotation = confetti ? new ParticleSystem.MinMaxCurve(0f, 6.28f) : new ParticleSystem.MinMaxCurve(0f);
        var rot = ps.rotationOverLifetime;
        rot.enabled = confetti;
        if (confetti) rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        if (confetti)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0.3f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
        }
        var shape = ps.shape;
        shape.angle = confetti ? 40f : 25f;
        float small = config.BurstSize * 0.7f * sizeScale, large = config.BurstSize * 1.2f * sizeScale;
        if (confetti)
        {
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(small, large);
            main.startSizeY = new ParticleSystem.MinMaxCurve(small * 0.6f, large * 0.6f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = confettiMaterial;
        }
        else
        {
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(small, large);
        }

        ps.Clear();
        ps.Emit(count);
        bursts.Add(new ActiveBurst { go = go, pool = pool, releaseAt = Time.time + lifetime + 0.2f });
    }

    void Update()
    {
        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            if (Time.time < bursts[i].releaseAt) continue;
            bursts[i].pool.Release(bursts[i].go);
            bursts.RemoveAt(i);
        }
    }
}
