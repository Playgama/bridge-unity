using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL
using Playgama;
#endif

namespace Examples
{
    public class DevicePanel : MonoBehaviour
    {
        [SerializeField] private Text _type;

#if UNITY_WEBGL
        private void Start()
        {
            _type.text = $"Type: { Bridge.device.type }";
        }
#endif
    }
}