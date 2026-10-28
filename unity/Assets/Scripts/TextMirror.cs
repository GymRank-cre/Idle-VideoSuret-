using UnityEngine;

// Recopie le texte d'un TextMesh sur un autre (ombre / contour sombre d'une étiquette 3D).
public class TextMirror : MonoBehaviour
{
    [SerializeField] TextMesh source;
    [SerializeField] TextMesh copy;

    void LateUpdate()
    {
        if (source != null && copy != null && copy.text != source.text)
            copy.text = source.text;
    }
}
