using UnityEngine;
using UnityEngine.UI;

public class Hold : InteractBase
{
    public float _holdDuration = 0.55f;
    public Image _progressIndicator = null;
    public Vector3 _indicatorOffset = new Vector3(50, 50, 0);

    private float _holdProgressTime = 0;
    private bool _isHoldComplete = false;
    private bool _isHoldingActive = false;

    protected override void RestClick()
    {
        _holdProgressTime = 0f;
        _isHoldComplete = false;
        _progressIndicator.gameObject.SetActive(false);
        _frameRenderer.material.color = _inactiveColor;
    }

    protected override void HandleClick()
    {
        var isMouseHeld = Input.GetMouseButton(0);
        var shouldStartHold = Input.GetMouseButtonDown(0) && DidHitObject();
        var shouldUpdateHold = _isHoldingActive && isMouseHeld && DidHitObject();
        var shouldCancelHold = _isHoldingActive && (isMouseHeld == false || DidHitObject() == false);
        var shouldResetHold = Input.GetMouseButtonUp(0) && !_isHoldComplete;

        if (shouldStartHold)
        {
            _isHoldingActive = true;
            StartHold();
            UpdateProgress();
        }

        if (shouldUpdateHold)
        {
            UpdateProgress();
        }

        if (shouldCancelHold)
        {
            CancelHold();
        }

        if (shouldResetHold)
        {
            RestClick();
            _isHoldingActive = false;
        }
    }

    protected override void ClickDetected()
    {
        _holdProgressTime = 0f;
        _isHoldComplete = true;
        _anzuAd.Interact();
        _progressIndicator.gameObject.SetActive(false);
        _frameRenderer.material.color = _inactiveColor;
    }

    private void StartHold()
    {
        _progressIndicator.gameObject.SetActive(true);
        _frameRenderer.material.color = _activeColor;
    }

    private void UpdateProgress()
    {
        var offsetPosition = Input.mousePosition + _indicatorOffset;
        _progressIndicator.rectTransform.position = offsetPosition;

        _holdProgressTime += 1 * Time.deltaTime;
        var progress = _holdProgressTime / _holdDuration;
        _progressIndicator.fillAmount = progress;

        if (progress >= 1f)
        {
            ClickDetected();
        }
    }

    private void CancelHold()
    {
        _isHoldingActive = false;
        RestClick();
    }
}