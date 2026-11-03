using UnityEngine;

namespace Escape4Now.Arena
{
    // Frame the room first, so nearby players cannot make the camera crop playable routes.
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaSharedCamera : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        private ArenaMatch match;
        [SerializeField]
        private RectTransform hud;
        [Header("HUD Clearance (player-follow option)")]
        [SerializeField, Min(0)]
        private float interfaceMarginPixels = 12;
        private Camera view;
        private ArenaLayout framedLayout;
        private float framedAspect;
        private void Awake()
        {
            view = GetComponent<Camera>();
            view.orthographic = true;
        }

        private void LateUpdate()
        {
            if (match == null || match.Map.Layout == null)
                return;
            var settings = match.Map.Settings;
            view.backgroundColor = settings.background;
            var layout = match.Map.Layout;
            Bounds players = new Bounds();
            bool first = true;
            foreach (var p in match.Players)
                if (p.Alive)
                {
                    if (first)
                    {
                        players = new Bounds(p.transform.position, Vector3.zero);
                        first = false;
                    }
                    else
                        players.Encapsulate(p.transform.position);
                }

            if (first)
                return;
            Bounds bounds = players;
            Rect safe = settings.cameraSafeViewport;
            if (settings.frameWholeMap)
            {
                bounds = new Bounds(match.Map.World(new Vector2(-.5f, -.5f)), Vector3.zero);
                bounds.Encapsulate(match.Map.World(new Vector2(layout.Width - .5f, -.5f)));
                bounds.Encapsulate(match.Map.World(new Vector2(-.5f, layout.Height - .5f)));
                bounds.Encapsulate(match.Map.World(new Vector2(layout.Width - .5f, layout.Height - .5f)));
                bounds.Encapsulate(bounds.max + Vector3.up * settings.cameraPropHeadroom);
                bounds.Encapsulate(players.min);
                bounds.Encapsulate(players.max);
            }
            else if (hud != null && hud.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                hud.GetWorldCorners(corners);
                float left = Mathf.Max(safe.xMin, (corners[2].x + interfaceMarginPixels) / Mathf.Max(1, Screen.width));
                safe = Rect.MinMaxRect(Mathf.Min(left, .8f), safe.yMin, safe.xMax, safe.yMax);
            }

            float fit = Mathf.Max((bounds.extents.y + settings.cameraPadding.y) / Mathf.Max(.1f, safe.height), (bounds.extents.x + settings.cameraPadding.x) / (view.aspect * Mathf.Max(.1f, safe.width)));
            float spread = players.size.magnitude / Mathf.Max(1, bounds.size.magnitude);
            float target = Mathf.Clamp(fit * (1 + spread * settings.cameraSpreadZoom), settings.minimumZoom, Mathf.Max(settings.minimumZoom, settings.maximumZoom));
            // On narrow screens, fitting the map takes priority over a designer's preferred zoom ceiling.
            target = Mathf.Max(fit, target);
            Vector2 shift = new Vector2((safe.center.x - .5f) * 2 * target * view.aspect, (safe.center.y - .5f) * 2 * target);
            Vector3 position = new Vector3(bounds.center.x - shift.x + settings.cameraFocusOffset.x, bounds.center.y - shift.y + settings.cameraFocusOffset.y, -30);
            // Refit immediately after a window resize; easing from the old aspect would briefly crop routes.
            bool newMap = framedLayout != layout || Mathf.Abs(framedAspect - view.aspect) > .001f;
            float t = newMap ? 1 : 1 - Mathf.Exp(-settings.cameraResponse * Time.deltaTime);
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, target, t);
            transform.position = Vector3.Lerp(transform.position, position, t);
            framedLayout = layout;
            framedAspect = view.aspect;
        }
    }
}
