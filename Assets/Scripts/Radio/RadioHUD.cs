using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace RageRunGames.Audio
{
    public class RadioHUD : MonoBehaviour
    {
        [SerializeField] private RadioManager radio;
        [SerializeField] private TextMeshProUGUI stationLabel;
        [SerializeField] private GameObject muteIcon;

        [Header("NEW")]
        [SerializeField] private Image stationImage;   // drag a UIImage here

        void Update()
        {
            if (!radio) return;

            if (stationLabel)  stationLabel.text = radio.CurrentChannelName;
            if (muteIcon)      muteIcon.SetActive(radio.IsMuted);

            if (stationImage)
                stationImage.sprite = radio.CurrentChannelIcon;   // NEW
        }
    }
}