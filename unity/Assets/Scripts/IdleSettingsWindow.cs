using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fenêtre Réglages (U3c) : sons, moins d'effets, effacer la partie en deux touches, version du jeu.
public class IdleSettingsWindow : MonoBehaviour
{
    [SerializeField] IdleSheet sheet;
    [SerializeField] Button soundToggle;
    [SerializeField] Button motionToggle;
    [SerializeField] Button eraseButton;
    [SerializeField] Text eraseLabel;
    [SerializeField] Text versionText;
    [SerializeField] Color onColor = new Color32(0x3D, 0xDC, 0x5A, 0xFF);
    [SerializeField] Color offColor = new Color32(0xB9, 0xC3, 0xCF, 0xFF);
    [SerializeField] float knobTravel = 36f;
    [SerializeField, Min(0.5f)] float eraseArmSeconds = 3.5f;
    [SerializeField] float contentHeight;

    float armedUntil = -1f;
    bool wired;

    void OnEnable()
    {
        if (!wired)
        {
            wired = true;
            soundToggle.onClick.AddListener(ToggleSound);
            motionToggle.onClick.AddListener(ToggleMotion);
            eraseButton.onClick.AddListener(Erase);
        }
        Disarm();
        sheet.SetContentHeight(contentHeight);
        Refresh();
    }

    void ToggleSound()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.SetSound(!audio.SoundOn);
        Refresh();
    }

    void ToggleMotion()
    {
        MotionSettings.ReduceMotion = !MotionSettings.ReduceMotion;
        Refresh();
    }

    void Erase()
    {
        if (armedUntil < 0f)
        {
            armedUntil = Time.unscaledTime + eraseArmSeconds;
            eraseLabel.text = Loc.T("settings.erase.arm");
            return;
        }
        SaveSystem.EraseEverything();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Disarm()
    {
        armedUntil = -1f;
        eraseLabel.text = Loc.T("settings.erase.yes");
    }

    void Update()
    {
        if (armedUntil >= 0f && Time.unscaledTime > armedUntil) Disarm();
    }

    void Refresh()
    {
        AudioManager audio = AudioManager.Instance;
        Show(soundToggle, audio != null && audio.SoundOn);
        Show(motionToggle, MotionSettings.ReduceMotion);
        versionText.text = Loc.T("settings.footer", BuildInfo.Version, NumberFormat.Short((long)IdleHost.Instance.Game.Earned));
    }

    // Interrupteur : la piste change de couleur et le rond glisse.
    void Show(Button toggle, bool on)
    {
        toggle.GetComponent<Image>().color = on ? onColor : offColor;
        var knob = (RectTransform)toggle.transform.GetChild(0);
        knob.anchoredPosition = new Vector2(on ? knobTravel : -knobTravel, knob.anchoredPosition.y);
    }
}
