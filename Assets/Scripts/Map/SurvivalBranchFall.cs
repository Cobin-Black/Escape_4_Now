//Animates the branch first. The map callback still validates whether it may block the tile when it lands.
using System;
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalBranchFall : MonoBehaviour
    {
        [SerializeField] private Transform branch;
        [SerializeField] private AudioSource impact;
        [SerializeField, Min(.05f)] private float fallSeconds = .4f;
        [SerializeField, Min(0)] private float fallHeight = .8f;
        [SerializeField] private float startAngle = 25f;
        private Func<bool> land;
        private Action rejected;
        private float elapsed;
        private Vector3 home;
        private bool finished;
        public void Configure(Func<bool> tryLand,Action failure)
        {
            land = tryLand; rejected = failure;
            home = branch.localPosition;
            branch.localPosition = home + Vector3.up*fallHeight;
        }
        private void Update()
        {
            if (finished || land == null || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed/fallSeconds);
            branch.localPosition = home + Vector3.up*fallHeight*(1-progress*progress);
            branch.localRotation = Quaternion.Euler(0,0,startAngle*(1-progress));
            if (progress < 1) return;
            finished = true;
            if (!land()) { rejected(); return; }
            if (impact != null) impact.Play();
        }
    }
}
