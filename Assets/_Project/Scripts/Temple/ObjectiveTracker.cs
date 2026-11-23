using UnityEngine;
using NED2024.Core;

namespace NED2024.Temple
{
    /// <summary>Knows where the current objective is. The map and the Echo Lens arrow read it.</summary>
    public class ObjectiveTracker : MonoBehaviour
    {
        public static ObjectiveTracker Instance { get; private set; }

        [SerializeField] private Transform[] stageTargets;   // one per MissionStage

        public Transform Current { get; private set; }

        private void Awake() { Instance = this; }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.ObjectiveChanged += OnObjective;
            OnObjective(gm.Stage, "");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var gm = GameManager.Instance;
            if (gm != null) gm.ObjectiveChanged -= OnObjective;
        }

        private void OnObjective(MissionStage stage, string text)
        {
            int i = Mathf.Clamp((int)stage, 0, stageTargets.Length - 1);
            Current = stageTargets.Length > 0 ? stageTargets[i] : null;
        }
    }
}
