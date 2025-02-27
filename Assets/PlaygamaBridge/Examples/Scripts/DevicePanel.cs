using Playgama;
using UnityEngine;
using UnityEngine.UI;

namespace Examples
{
    public class DevicePanel : MonoBehaviour
    {
        [SerializeField] private Text _type;

        private void Start()
        {
            _type.text = $"Type: { Bridge.device.type }";
        }
    }
}