using UnityEngine;

// Cale ce RectTransform dans la zone sûre de l'écran (encoche, coins arrondis).
// À mettre sur un conteneur plein écran sous le Canvas.
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rect;
    Rect lastArea;
    Vector2Int lastScreen;

    void OnEnable()
    {
        rect = (RectTransform)transform;
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != lastArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height)
            Apply();
    }

    void Apply()
    {
        lastArea = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = lastArea.position;
        Vector2 max = lastArea.position + lastArea.size;
        rect.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
        rect.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
