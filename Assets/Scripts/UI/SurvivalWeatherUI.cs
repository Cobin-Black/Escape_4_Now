//Adds a configurable rain tint below the gameplay UI. Blackout keeps its existing visibility effect.
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalWeatherUI : MonoBehaviour
    {
        [SerializeField] private SurvivalPuzzleUI view;
        [SerializeField] private Image rainShade;
        [SerializeField] private Color rainColor = new Color(.04f,.12f,.2f,.14f);
        [SerializeField, Min(.1f)] private float fadeSpeed = 2f;
        private void Update()
        {
            bool raining = view.Playing && view.EventSource != null && view.EventSource.RainActive;
            var target = rainColor; if (!raining) target.a = 0;
            if (!Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
                rainShade.color = Color.Lerp(rainShade.color,target,Time.deltaTime*fadeSpeed);
        }
    }
}
