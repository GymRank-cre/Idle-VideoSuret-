using System.Collections.Generic;
using UnityEngine;

// Poste d'installation : tant que le joueur est dans la zone, il y dépose ses
// caisses une à une. Quand il y en a assez, un travail démarre (barre de
// progression), puis des billets apparaissent au point de sortie.
[RequireComponent(typeof(Collider))]
public class InstallStation : MonoBehaviour
{
    [SerializeField] StationConfig config;
    [SerializeField] ResourcePile cratesSource; // son pool de caisses est partagé avec le poste
    [SerializeField] GameObject billPrefab;
    [SerializeField] FeedbackSpawner feedback; // facultatif : éclat quand une caisse arrive
    [SerializeField] ProgressBar3D progressBar;
    [SerializeField] Transform billExit;     // point autour duquel les billets tombent
    [SerializeField] float flightDuration = 0.25f;
    [SerializeField] float flightArc = 0.8f;
    [SerializeField] float billScatterRadius = 1f;

    struct Flight
    {
        public Transform item;
        public Vector3 from;
        public float elapsed;
    }

    readonly List<Flight> flights = new List<Flight>();
    ObjectPool cratePool;
    ObjectPool billPool;

    int storedCrates;
    bool working;
    float jobTimer;
    float depositTimer;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        cratePool = cratesSource.Pool;
        billPool = new ObjectPool(billPrefab, new GameObject(name + " Bills").transform);
    }

    Vector3 CrateTarget => transform.position + Vector3.up * 0.4f;

    void OnTriggerStay(Collider other)
    {
        StackCarrier carrier = other.GetComponent<StackCarrier>();
        if (carrier == null || carrier.IsEmpty) return;
        if (storedCrates + flights.Count >= config.InputCapacity) return;

        depositTimer += Time.deltaTime;
        if (depositTimer < config.DepositInterval) return;
        depositTimer = 0f;

        Transform taken = carrier.RemoveLast();
        if (taken == null) return;

        // La caisse reprise est recyclée dans le pool, puis réutilisée pour le vol.
        Vector3 from = taken.position;
        Quaternion rot = taken.rotation;
        cratePool.Release(taken.gameObject);
        GameObject flying = cratePool.Get(from, rot);
        flights.Add(new Flight { item = flying.transform, from = from });
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<StackCarrier>() != null)
            depositTimer = 0f;
    }

    void Update()
    {
        UpdateFlights();
        UpdateJob();
    }

    void UpdateFlights()
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            f.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(f.elapsed / flightDuration);

            Vector3 pos = Vector3.Lerp(f.from, CrateTarget, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * flightArc;
            f.item.position = pos;

            if (t >= 1f)
            {
                cratePool.Release(f.item.gameObject);
                flights.RemoveAt(i);
                storedCrates++;
                if (feedback != null) feedback.CrateArrived(CrateTarget);
            }
            else
            {
                flights[i] = f;
            }
        }
    }

    void UpdateJob()
    {
        if (!working && storedCrates >= config.CratesPerJob)
        {
            storedCrates -= config.CratesPerJob;
            working = true;
            jobTimer = 0f;
        }

        if (!working) return;

        jobTimer += Time.deltaTime;
        if (progressBar != null)
            progressBar.SetProgress(jobTimer / config.JobDuration);

        if (jobTimer >= config.JobDuration)
        {
            working = false;
            if (progressBar != null) progressBar.SetProgress(0f);
            SpawnBills();
        }
    }

    void SpawnBills()
    {
        Vector3 center = billExit != null ? billExit.position : transform.position;
        for (int i = 0; i < config.BillsPerJob; i++)
        {
            Vector2 offset = Random.insideUnitCircle * billScatterRadius;
            Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject go = billPool.Get(pos, rot);
            go.GetComponent<Bill>().Init(config.BillValue, billPool.Release);
        }
    }
}
