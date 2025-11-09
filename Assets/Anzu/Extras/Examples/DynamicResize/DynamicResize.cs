using UnityEngine;
using Anzu;

public class DynamicResize : MonoBehaviour
{
    public AnzuAd _anzuAd = null;
    public Transform _screenObject = null;

    private void Awake()
    {
        _anzuAd.OnChannelPlay += AdjustFrameToAd;
    }

    private void OnEnable()
    {
        AdjustFrameToAd();
    }

    private void OnDestroy()
    {
        _anzuAd.OnChannelPlay -= AdjustFrameToAd;
    }

    private void AdjustFrameToAd()
    {
        var scaleAddition = new Vector3(0.5f, 0.5f, 0);
        _screenObject.localScale = _anzuAd.transform.localScale + scaleAddition;
    }
}