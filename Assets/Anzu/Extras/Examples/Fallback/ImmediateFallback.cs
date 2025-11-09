using UnityEngine;
using Anzu;

public class ImmediateFallback : MonoBehaviour
{
    public AnzuAd _anzuAd = null;
    public Texture _fallbackTexture = null;

    private void Awake()
    {
        var meshRenderer = _anzuAd.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogWarning("MeshRenderer not found on GameObject: " + gameObject.name);
        }
        else
        {
            var material = meshRenderer.material;

            if (material == null)
            {
                Debug.LogWarning("Material not found on MeshRenderer: " + gameObject.name);
            }
            else
            {
                // Apply the fallback texture immediately to ensure display.
                material.mainTexture = _fallbackTexture;
            }
        }
    }
}
