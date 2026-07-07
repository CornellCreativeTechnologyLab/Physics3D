using UnityEngine;
using UnityEngine.UI;

public class MaterialColorToImage : MonoBehaviour
{
    public Material sourceMaterial;
    public Image targetImage;

    void Start()
    {
        if (sourceMaterial != null && targetImage != null)
        {
            Color c = sourceMaterial.GetColor("_Color");   // Standard shader uses _Color
            targetImage.color = c;
        }
    }
}
