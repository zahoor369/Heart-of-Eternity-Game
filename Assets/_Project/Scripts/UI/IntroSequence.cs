using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NED2024.Core;

namespace NED2024.UI
{
    /// <summary>
    /// Opening cinematic: a black screen with story text that fades in and out while the camera slowly moves,
    /// then hands control to the player. Any key skips a line.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private Text titleText;
        [SerializeField] private Text lineText;
        [SerializeField] private Transform introCameraRig;
        [SerializeField] private GameObject playerCameras;
        [SerializeField] private Camera introCamera;
        [SerializeField] private AudioClip stingClip;

        [TextArea(2, 4)]
        [SerializeField] private string[] lines =
        {
            "An old map led you to a temple lost in the jungle.",
            "Legend says it hides the Heart of Eternity.",
            "Find the keys. Wake the runes. Take the Heart. Get out before the temple seals itself."
        };

        private void Start()
        {
            if (GameManager.Instance.ResumedFromCheckpoint)
            {
                // Coming back from a checkpoint: skip the cinematic.
                FinishImmediately();
                return;
            }
            StartCoroutine(Run());
        }

        private void FinishImmediately()
        {
            overlay.alpha = 0f;
            overlay.gameObject.SetActive(false);
            if (introCamera != null) introCamera.gameObject.SetActive(false);
            if (playerCameras != null) playerCameras.SetActive(true);
            GameManager.Instance.BeginPlay();
        }

        private IEnumerator Run()
        {
            overlay.gameObject.SetActive(true);
            overlay.alpha = 1f;
            titleText.text = "HEART OF ETERNITY";
            lineText.text = "";
            if (playerCameras != null) playerCameras.SetActive(false);
            AudioManager.Instance.PlaySfx(stingClip, 0.8f);

            yield return new WaitForSeconds(2.2f);
            titleText.text = "";

            Coroutine cameraMove = introCameraRig != null ? StartCoroutine(MoveIntroCamera()) : null;

            foreach (string line in lines)
            {
                lineText.text = line;
                yield return Fade(lineText, 0f, 1f, 0.8f);
                float t = 0f;
                while (t < 3.4f && !GameInput.SkipDown)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                yield return Fade(lineText, 1f, 0f, 0.6f);
            }
            lineText.text = "";

            // Fade from black into the world.
            float a = 1f;
            while (a > 0f)
            {
                a -= Time.deltaTime / 1.5f;
                overlay.alpha = Mathf.Max(0f, a);
                yield return null;
            }
            if (cameraMove != null) StopCoroutine(cameraMove);
            if (introCamera != null) introCamera.gameObject.SetActive(false);
            if (playerCameras != null) playerCameras.SetActive(true);
            overlay.gameObject.SetActive(false);
            GameManager.Instance.BeginPlay();
        }

        private IEnumerator Fade(Text text, float from, float to, float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / seconds;
                Color c = text.color;
                c.a = Mathf.Lerp(from, to, t);
                text.color = c;
                yield return null;
            }
        }

        private IEnumerator MoveIntroCamera()
        {
            // Slow dolly forward along the camera's own forward direction.
            while (true)
            {
                introCameraRig.position += introCameraRig.forward * 0.6f * Time.deltaTime;
                yield return null;
            }
        }
    }
}
