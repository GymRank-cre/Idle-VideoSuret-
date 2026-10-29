using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Petit bouton engrenage en haut au centre et panneau de réglages : interrupteurs Sons / Musique / Vibrations
// et « Effacer la sauvegarde » avec confirmation. Les états sont mémorisés par SaveSystem.
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] GameObject confirmBox;
    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;
    [SerializeField] Button soundToggle;
    [SerializeField] Button musicToggle;
    [SerializeField] Button vibrationToggle;
    [SerializeField] Button eraseButton;
    [SerializeField] Button confirmYes;
    [SerializeField] Button confirmNo;
    [SerializeField] Text versionText;  // numéro du build, pour savoir quelle version tourne sur le téléphone
    [SerializeField] Color onColor = new Color(0.2f, 0.7f, 0.3f);
    [SerializeField] Color offColor = new Color(0.35f, 0.38f, 0.43f);
    [SerializeField] float knobTravel = 36f;

    AudioManager audioManager;

    void Start()
    {
        audioManager = AudioManager.Instance;
        panel.SetActive(false);
        confirmBox.SetActive(false);
        if (versionText != null) versionText.text = Loc.T("settings.version", BuildInfo.Version);

        openButton.onClick.AddListener(() => { Click(); panel.SetActive(!panel.activeSelf); confirmBox.SetActive(false); Refresh(); });
        closeButton.onClick.AddListener(() => { Click(); panel.SetActive(false); });
        soundToggle.onClick.AddListener(() => { Click(); if (audioManager != null) audioManager.SetSound(!audioManager.SoundOn); });
        musicToggle.onClick.AddListener(() => { Click(); if (audioManager != null) audioManager.SetMusic(!audioManager.MusicOn); });
        vibrationToggle.onClick.AddListener(() => { Click(); if (audioManager != null) audioManager.SetVibration(!audioManager.VibrationOn); });
        eraseButton.onClick.AddListener(() => { Click(); confirmBox.SetActive(true); });
        confirmNo.onClick.AddListener(() => { Click(); confirmBox.SetActive(false); });
        confirmYes.onClick.AddListener(EraseAndRestart);

        if (audioManager != null) audioManager.OnSettingsChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (audioManager != null) audioManager.OnSettingsChanged -= Refresh;
    }

    static void Click()
    {
        AudioManager.Play(SfxId.Click);
    }

    void Refresh()
    {
        if (audioManager == null) return;
        Show(soundToggle, audioManager.SoundOn);
        Show(musicToggle, audioManager.MusicOn);
        Show(vibrationToggle, audioManager.VibrationOn);
    }

    // Interrupteur : la piste change de couleur et le bouton rond glisse.
    void Show(Button toggle, bool on)
    {
        toggle.GetComponent<Image>().color = on ? onColor : offColor;
        Transform knob = toggle.transform.GetChild(0);
        Vector3 p = knob.localPosition;
        p.x = on ? knobTravel : -knobTravel;
        knob.localPosition = p;
    }

    // Efface la sauvegarde puis relance la scène : nouvelle partie propre, guide compris.
    void EraseAndRestart()
    {
        SaveSystem.EraseEverything();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
