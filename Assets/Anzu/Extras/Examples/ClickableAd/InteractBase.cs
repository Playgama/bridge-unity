using UnityEngine;
using Anzu;

public abstract class InteractBase : MonoBehaviour
{
    public MeshRenderer _frameRenderer = null;

    protected AnzuAd _anzuAd = null;
    protected readonly Color _activeColor = new Color(1, 0, 1);
    protected readonly Color _inactiveColor = new Color(1, 1, 0);

    protected abstract void RestClick();
    protected abstract void HandleClick();
    protected abstract void ClickDetected();

    protected void Awake()
    {
        _anzuAd = GetComponent<AnzuAd>();
        RestClick();
    }

    protected void Update()
    {
        var canInteract = _anzuAd != null
            && _anzuAd.IsClickable;

        if (canInteract)
        {
            HandleClick();
        }
    }

    protected bool DidHitObject()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject;
    }
}