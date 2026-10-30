using UnityEngine;

// Particules des postes (UW-4) : un seul système, donc un seul matériau et un seul appel de dessin, 150 particules vivantes au plus, aucune allocation.
// Chaque poste a son effet à un moment de son cycle : poussière (Réception), étincelles (Assemblage), anneau vert (Test), carton (Emballage), fumée (Expédition).
public class IdleStationFx : MonoBehaviour
{
    public const int MaxParticles = 150;

    [SerializeField] ParticleSystem system;

    static readonly Color Dust = new Color32(0xCD, 0xB4, 0x8A, 0xCC), Spark = new Color32(0xFF, 0xD2, 0x3C, 0xFF), SparkHot = new Color32(0xFF, 0x8A, 0x2E, 0xFF);
    static readonly Color Scan = new Color32(0x3D, 0xDC, 0x5A, 0xE6), Card = new Color32(0xD9, 0xA2, 0x5B, 0xFF), CardLight = new Color32(0xF2, 0xD6, 0xA8, 0xFF);
    static readonly Color Smoke = new Color32(0x8A, 0x93, 0x9E, 0xB3), PadGlow = new Color32(0x8C, 0xFF, 0x9E, 0xE6);

    void Awake()
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = MaxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.35f;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.15f)));
        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
    }

    void Emit(Vector3 position, Vector3 velocity, float size, Color color, float lifetime)
    {
        var e = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = color, startLifetime = lifetime };
        system.Emit(e, 1);
    }

    // Moment fort du cycle d'un poste ; `center` : centre du poste au sol.
    public void Cycle(int station, Vector3 center)
    {
        switch (station)
        {
            case 0: Puffs(center + new Vector3(0.35f, 0.3f, 0.2f), Dust, 4, 0.8f, 0.62f, 0.8f); break;
            case 1: Sparks(center + new Vector3(0.1f, 0.85f, 0.25f)); break;
            case 2: Ring(center + new Vector3(0f, 0.25f, 0f), 0.75f, 1.1f, Scan, 12); break;
            case 3: Cardboard(center + new Vector3(0.65f, 0.75f, 0f)); break;
            default: Puffs(center + new Vector3(-0.45f, 0.2f, 0.35f), Smoke, 5, 0.6f, 0.58f, 0.9f); break;
        }
    }

    // Petit anneau qui éclate sous la plaque qu'on vient d'acheter.
    public void PadRing(Vector3 position)
    {
        Ring(position + Vector3.up * 0.1f, 0.3f, 1.5f, PadGlow, 10);
    }

    void Puffs(Vector3 at, Color color, int count, float speed, float size, float lifetime)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 side = Random.insideUnitCircle * speed;
            Emit(at, new Vector3(side.x, 0.5f * speed + 0.3f, side.y), size * Random.Range(0.8f, 1.2f), color, lifetime);
        }
    }

    void Sparks(Vector3 at)
    {
        for (int i = 0; i < 7; i++)
        {
            Vector2 side = Random.insideUnitCircle * 1.6f;
            Emit(at, new Vector3(side.x, Random.Range(0.8f, 1.8f), side.y), Random.Range(0.2f, 0.32f), i % 2 == 0 ? Spark : SparkHot, 0.55f);
        }
    }

    void Cardboard(Vector3 at)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 side = Random.insideUnitCircle * 1f;
            Emit(at, new Vector3(side.x, Random.Range(0.6f, 1.3f), side.y), Random.Range(0.24f, 0.36f), i % 2 == 0 ? Card : CardLight, 0.75f);
        }
    }

    void Ring(Vector3 at, float radius, float speed, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            var direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Emit(at + direction * radius, direction * speed + Vector3.up * 0.4f, 0.3f, color, 0.55f);
        }
    }
}
