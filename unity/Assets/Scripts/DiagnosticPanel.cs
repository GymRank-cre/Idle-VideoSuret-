using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Panneau de diagnostic (caché : bouton « Diagnostic » en bas des réglages) : version du build, état de la caméra, écran,
// carte graphique, images par seconde et les 10 dernières erreurs ou avertissements. Sébastien en envoie une capture
// d'écran en cas de nouvel écran gris.
public class DiagnosticPanel : MonoBehaviour
{
    const int MaxMessages = 10;

    [SerializeField] GameObject panel;
    [SerializeField] Text body;
    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;
    [SerializeField] float refreshInterval = 0.5f;

    readonly Queue<string> messages = new Queue<string>();
    readonly StringBuilder sb = new StringBuilder(1024);
    float fps;
    float timer;

    void Awake()
    {
        panel.SetActive(false);
        Application.logMessageReceived += OnLog; // actif dès le démarrage : garde aussi ce qui s'est passé avant l'ouverture
        Debug.Log("Version du build : " + BuildInfo.Version);
    }

    void Start()
    {
        openButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); panel.SetActive(true); panel.transform.SetAsLastSibling(); Refresh(); });
        closeButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); panel.SetActive(false); });
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log) return; // seulement erreurs, exceptions, avertissements
        string line = (type == LogType.Warning ? "[avert] " : "[erreur] ") + condition.Replace('\n', ' ');
        if (line.Length > 110) line = line.Substring(0, 110) + "…";
        messages.Enqueue(line);
        while (messages.Count > MaxMessages) messages.Dequeue();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) fps = Mathf.Lerp(fps, 1f / dt, 0.05f);

        if (!panel.activeSelf) return;
        timer += dt;
        if (timer < refreshInterval) return;
        timer = 0f;
        Refresh();
    }

    void Refresh()
    {
        Camera cam = Camera.main;
        sb.Length = 0;
        sb.Append("Version : ").Append(BuildInfo.Version).Append('\n');
        sb.Append("Images/s : ").Append(fps.ToString("0")).Append('\n');
        sb.Append("Écran : ").Append(Screen.width).Append('x').Append(Screen.height).Append('\n');
        sb.Append("Carte : ").Append(SystemInfo.graphicsDeviceName).Append('\n');
        sb.Append("API : ").Append(SystemInfo.graphicsDeviceVersion).Append('\n');
        if (cam != null)
        {
            CameraFollow follow = cam.GetComponent<CameraFollow>();
            sb.Append("Caméra pos : ").Append(cam.transform.position.ToString("F1")).Append('\n');
            sb.Append("Caméra rot : ").Append(cam.transform.eulerAngles.ToString("F0")).Append('\n');
            sb.Append("Champ : ").Append(cam.fieldOfView.ToString("F1")).Append("  aspect : ").Append(cam.aspect.ToString("F3")).Append('\n');
            sb.Append("Réparations caméra : ").Append(follow != null ? follow.RepairCount : 0).Append('\n');
        }
        else
        {
            sb.Append("Caméra : introuvable\n");
        }
        sb.Append("Messages (10 derniers) :\n");
        if (messages.Count == 0) sb.Append("  aucun\n");
        foreach (string m in messages) sb.Append("  ").Append(m).Append('\n');
        body.text = sb.ToString();
    }
}
