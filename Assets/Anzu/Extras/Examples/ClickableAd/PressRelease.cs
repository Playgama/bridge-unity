using UnityEngine;

public class PressRelease : InteractBase
{
    public float _distanceRadius = 40f;
    public float _releaseTimeout = 0.55f;

    private float _timeoutTimer = 0;
    private bool _clickInProgress = false;
    private Vector2 _clickPosition = Vector2.zero;

    protected override void RestClick()
    {
        _clickInProgress = false;
        _frameRenderer.material.color = _inactiveColor;
    }

    protected override void HandleClick()
    {
        var hitObject = DidHitObject();

        if (Input.GetMouseButtonDown(0))
        {
            _timeoutTimer = 0;
            _clickPosition = Input.mousePosition;
        }

        var clickEvent = Input.GetMouseButtonDown(0)
            || Input.GetMouseButton(0)
            || Input.GetMouseButtonUp(0);

        if (clickEvent)
        {
            _timeoutTimer += 1 * Time.deltaTime;

            var clickDown = Input.GetMouseButtonDown(0) && !_clickInProgress && hitObject;
            var clickWentOutside = Input.GetMouseButton(0) && !hitObject;
            var clickUp = Input.GetMouseButtonUp(0) && _clickInProgress;
            var distanceBetweenClicks = Vector2.Distance(_clickPosition, Input.mousePosition) > _distanceRadius;
            var timeoutBetweenClicks = _timeoutTimer >= _releaseTimeout;
            var resetNeeded = clickWentOutside || timeoutBetweenClicks || distanceBetweenClicks;

            if (clickDown)
            {
                _clickInProgress = true;
                _frameRenderer.material.color = _activeColor;
            }
            else if (resetNeeded)
            {
                RestClick();
            }
            else if (clickUp)
            {
                RestClick();

                if (hitObject)
                {
                    ClickDetected();
                }
            }
        }
    }

    protected override void ClickDetected()
    {
        _anzuAd.Interact();
    }
}