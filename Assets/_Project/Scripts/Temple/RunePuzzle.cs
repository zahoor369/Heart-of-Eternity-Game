using System.Collections;
using UnityEngine;
using NED2024.Core;

namespace NED2024.Temple
{
    /// <summary>
    /// The rune chamber puzzle. Four pedestals carry the symbols Sun, Moon, Serpent and Eye. They must be woken in the order shown by
    /// the hidden mural (visible with the Echo Lens). A wrong pedestal discharges a shock and resets the sequence.
    /// </summary>
    public class RunePuzzle : MonoBehaviour
    {
        [SerializeField] private int[] solution = { 1, 3, 0, 2 };      // Moon, Eye, Sun, Serpent
        [SerializeField] private TempleDoor gate;
        [SerializeField] private RunePedestal[] pedestals;
        [SerializeField] private float shockDamage = 15f;

        private int progress;
        public bool Solved { get; private set; }
        public int[] Solution => solution;

        public void Press(RunePedestal pedestal)
        {
            var gm = GameManager.Instance;
            if (Solved || gm == null) return;
            if (pedestal.Glyph == solution[progress])
            {
                pedestal.Light(true);
                Sfx.PlayAt("rune_" + pedestal.Glyph, pedestal.transform.position, 1f);
                progress++;
                if (progress >= solution.Length) StartCoroutine(Succeed());
                else gm.ShowMessage("The rune hums.  (" + progress + "/" + solution.Length + ")", 1.8f);
            }
            else
            {
                StartCoroutine(Fail(pedestal));
            }
        }

        private IEnumerator Fail(RunePedestal pedestal)
        {
            progress = 0;
            Sfx.PlayAt("zap", pedestal.transform.position, 1f);
            Sfx.Play("rune_fail", 0.7f);
            pedestal.Flash(true);
            var gm = GameManager.Instance;
            gm.ShowMessage("Wrong rune! A shock runs through the floor. The sequence resets.", 3f);
            var player = gm.PlayerTransform;
            if (player != null && Vector3.Distance(player.position, pedestal.transform.position) < 8f)
            {
                var hp = player.GetComponent<Health>();
                if (hp != null) hp.TakeDamage(new DamageInfo { Amount = shockDamage, Point = player.position, Direction = Vector3.up });
            }
            yield return new WaitForSeconds(0.6f);
            foreach (var p in pedestals) { p.Light(false); p.Flash(false); }
        }

        private IEnumerator Succeed()
        {
            Solved = true;
            yield return new WaitForSeconds(0.5f);
            Sfx.Play("rune_ok", 1f);
            var gm = GameManager.Instance;
            gm.ShowMessage("The runes align. The gate to the Trap Gallery opens!  (+" + GameManager.PointsPerRune + ")", 4f);
            gm.RunesSolved();
            if (gate != null) gate.SetOpen(true);
        }

        /// <summary>Used when a saved game is loaded after the puzzle was already solved.</summary>
        public void RestoreSolved()
        {
            Solved = true; progress = solution.Length;
            foreach (var p in pedestals) p.Light(true);
        }
    }
}
