using UnityEngine;
using UnityEngine.InputSystem;

// Le navigateur n'autorise le son qu'après un premier geste : au premier toucher ou clic, on libère l'audio, puis ce script s'arrête.
public class IdleAudioUnlock : MonoBehaviour
{
    void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (AudioManager.Instance != null) AudioManager.Instance.Unlock();
        enabled = false;
    }
}
