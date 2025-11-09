using UnityEngine;

public class DoublePress : InteractBase
{
    public float _pressTimeout = 0.55f;

    private int _clickCount = 0;
    private float _lastClickTime = 0;

    protected override void RestClick()
    {
        _clickCount = 0;
        _frameRenderer.material.color = _inactiveColor;
    }

    protected override void HandleClick()
    {
        var clickDetected = Input.GetMouseButtonDown(0) && DidHitObject();

        if (clickDetected)
        {
            _clickCount++;
            _frameRenderer.material.color = _activeColor;

            if (_clickCount == 1)
            {
                _lastClickTime = Time.time;
            }
        }

        var doubleClickDetected = _clickCount > 1 && Time.time - _lastClickTime < _pressTimeout;
        var resetRequired = _clickCount > 2 || Time.time - _lastClickTime > _pressTimeout;

        if (doubleClickDetected)
        {
            ClickDetected();
        }
        else if (resetRequired)
        {
            RestClick();
        }
    }

    protected override void ClickDetected()
    {
        _clickCount = 0;
        _lastClickTime = 0;
        _anzuAd.Interact();
        _frameRenderer.material.color = _inactiveColor;
    }
}