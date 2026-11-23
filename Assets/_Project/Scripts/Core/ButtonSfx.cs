using UnityEngine;
using UnityEngine.UI;

namespace NED2024.Core
{
    /// <summary>Plays a click sound when the button is pressed (OnClick audio).</summary>
    [RequireComponent(typeof(Button))]
    public class ButtonSfx : MonoBehaviour
    {
        [SerializeField] private AudioClip clickClip;

        public AudioClip ClickClip { get => clickClip; set => clickClip = value; }

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(PlayClick);
        }

        private void PlayClick()
        {
            AudioManager.Instance.PlaySfx(clickClip, 0.8f);
        }
    }
}
