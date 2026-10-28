// Shows the current lesson, its control hint and target highlight while the tutorial manages progress.

using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Map;
using Escape4Now.Items;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using Escape4Now.Input;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Escape4Now.UI
{
    public sealed class TutorialOverlay : MonoBehaviour
    {
        [Header("Lesson and Scene References")]
        [SerializeField] private Tutorial_Functionality tutorial;
        [SerializeField] private MatchUIContext context;
        [SerializeField] private TutorialLessonCatalog lessons;
        [SerializeField] private MatchHudUI hud;
        [SerializeField] private ItemPickupBar inventory;
        [Header("Unity UI References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text progress;
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [SerializeField] private RectTransform canvas;
        [SerializeField] private Text[] keycaps;
        [SerializeField] private Image[] progressDots;
        [SerializeField] private Image cardBorder;
        [SerializeField] private Text nextLabel;
        [SerializeField] private GameObject restart;
        [SerializeField] private GameObject skip;
        [SerializeField] private GameObject replay;
        [SerializeField] private GameObject next;
        [SerializeField] private TutorialHighlighter highlighter;
        [Header("Lesson Progress")]
        [SerializeField, Min(1)] private int partNumber = 1;
        [SerializeField, Min(1)] private int partCount = 3;
        [SerializeField] private string nextPartName;
        [SerializeField] private string firstTutorialScene;
        [SerializeField] private string stepFormat = "STEP {0} OF {1}";
        [SerializeField] private string partFormat = "PART {0} OF {1}";
        [SerializeField] private string doneFormat = "{0} DONE";
        [SerializeField] private string nextStepText = "the next step";
        [SerializeField] private string nextPartText = "the next part";
        [SerializeField] private string nextPartFormat = "part {0}: {1}";
        [SerializeField] private string tutorialDoneText = "TUTORIAL DONE";
        [SerializeField] private string finalExitTitle = "You escaped!";
        [SerializeField] private string continueText = "Continue";
        [SerializeField] private string finishText = "Finish";
        [SerializeField] private string[] sectionNames = { "Movement", "Items", "Lockers", "Warp", "Exit" };
        [Header("Input Hints")]
        [SerializeField] private string bindingGroup = "Keyboard&Mouse";
        [SerializeField, Min(0f)] private float keycapPadding = 18f;
        [Header("Card Placement")]
        [SerializeField, Min(0f)] private float screenMargin = 16f;
        [SerializeField, Min(0f)] private float targetGap = 24f;
        [SerializeField, Min(0f)] private float dockGap = 12f;
        [SerializeField, Min(0f)] private float slideDistance = 12f;
        [SerializeField, Min(0.01f)] private float slideSeconds = 0.25f;
        [Header("Progress Appearance")]
        [SerializeField] private Color accentColor = new Color(0.4f, 0.72f, 1f);
        [SerializeField] private Color doneColor = new Color(0.3f, 0.82f, 0.48f);
        [SerializeField] private Color pendingColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField, Min(1f)] private float currentDotWidth = 22f;
        [SerializeField, Min(1f)] private float dotWidth = 8f;
        [Header("World Target Framing")]
        [SerializeField] private Vector2 targetPadding = new Vector2(0.2f, 0.2f);
        private string lastStep;
        private float stepStarted;
        private readonly List<string> bindingLabels = new List<string>();

        private void LateUpdate()
        {
            var player = context.ActivePlayer;
            panel.SetActive(tutorial != null && player != null);
            if (!panel.activeSelf) return;
            string id = ResolveStep(player);
            var lesson = lessons != null ? lessons.Find(id) : null;
            if (lesson == null) return;
            if (id != lastStep) { lastStep = id; stepStarted = Time.unscaledTime; }
            int number = tutorial.UsesSceneParts ? partNumber : (int)tutorial.CurrentSection;
            int total = tutorial.UsesSceneParts ? partCount : System.Enum.GetValues(typeof(TutorialSection)).Length;
            string label = string.Format(tutorial.UsesSceneParts ? partFormat : stepFormat, number, total);
            progress.text = tutorial.IsSectionComplete ? tutorial.IsFinalSection ? tutorialDoneText : string.Format(doneFormat, label) : label;
            Color accent = tutorial.IsSectionComplete ? doneColor : accentColor;
            progress.color = cardBorder.color = accent;
            int sectionIndex = (int)tutorial.CurrentSection - 1;
            string sectionName = sectionIndex >= 0 && sectionIndex < sectionNames.Length
                ? sectionNames[sectionIndex] : tutorial.CurrentSection.ToString();
            title.text = tutorial.IsSectionComplete && tutorial.IsFinalSection && tutorial.CurrentSection == TutorialSection.Exit
                ? finalExitTitle : lesson.title.Replace("{section}", sectionName);
            string nextName = !tutorial.UsesSceneParts ? nextStepText : string.IsNullOrEmpty(nextPartName)
                ? nextPartText : string.Format(nextPartFormat, partNumber + 1, nextPartName);
            var action = ResolveAction(lesson.action);
            InputBindingHints.GetPrimaryLabels(action, bindingGroup, bindingLabels);
            string keys = string.Join(" / ", bindingLabels);
            body.text = lesson.body.Replace("{player}", player.name).Replace("{moves}", player.MovesRemaining.ToString())
                .Replace("{binding}", keys).Replace("{next}", nextName);
            for (int i = 0; i < keycaps.Length; i++)
            {
                bool show = i < bindingLabels.Count;
                keycaps[i].transform.parent.gameObject.SetActive(show);
                if (!show) continue;
                keycaps[i].text = bindingLabels[i];
                var layout = keycaps[i].transform.parent.GetComponent<LayoutElement>();
                layout.preferredWidth = Mathf.Max(layout.minWidth, keycaps[i].preferredWidth + keycapPadding);
            }
            for (int i = 0; i < progressDots.Length; i++)
            {
                progressDots[i].gameObject.SetActive(i < total);
                progressDots[i].color = i + 1 < number || (i + 1 == number && tutorial.IsSectionComplete)
                    ? doneColor : i + 1 == number ? accentColor : pendingColor;
                progressDots[i].GetComponent<LayoutElement>().preferredWidth = i + 1 == number ? currentDotWidth : dotWidth;
            }
            restart.SetActive(!tutorial.IsSectionComplete);
            skip.SetActive(!tutorial.IsSectionComplete);
            replay.SetActive(tutorial.IsSectionComplete && tutorial.IsFinalSection);
            next.SetActive(tutorial.IsSectionComplete);
            nextLabel.text = tutorial.IsFinalSection ? finishText : continueText;
            Rect? target = ResolveTarget(lesson.target, player);
            highlighter.Show(target, Time.unscaledTime - stepStarted,
                tutorial.IsSectionComplete && tutorial.IsFinalSection);
            Rect? placementTarget = target;
            if (lesson.target == TutorialTarget.Inventory && target.HasValue && inventory.NoticeRect.width > 0f)
            {
                Rect notice = inventory.NoticeRect, anchor = target.Value;
                placementTarget = Rect.MinMaxRect(Mathf.Min(anchor.xMin, notice.xMin), Mathf.Min(anchor.yMin, notice.yMin),
                    Mathf.Max(anchor.xMax, notice.xMax), Mathf.Max(anchor.yMax, notice.yMax));
            }
            PositionCard(id, placementTarget, player);
        }

        // Place the lesson card around its current target instead of covering the area being demonstrated.
        private void PositionCard(string id, Rect? target, PlayerCharacter player)
        {
            var rect = (RectTransform)panel.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float fit = Mathf.Min(1f, (canvas.rect.width - screenMargin * 2f) / rect.rect.width,
                (canvas.rect.height - screenMargin * 2f) / rect.rect.height);
            rect.localScale = Vector3.one * Mathf.Max(0.01f, fit);
            float scale = canvas.lossyScale.x * rect.localScale.x;
            float width = rect.rect.width * scale, height = rect.rect.height * scale;
            float margin = screenMargin * canvas.lossyScale.x;
            float gap = targetGap * canvas.lossyScale.x;
            float dock = dockGap * canvas.lossyScale.x;
            bool docked = id == "move-walk" || id == "items-walk" || id.StartsWith("obstacles")
                || id.StartsWith("events") || id == "exit" || (tutorial.IsSectionComplete && !tutorial.IsFinalSection);
            Rect card;
            if (docked)
            {
                float inventoryTop = inventory.PanelRect.height > 0f ? inventory.PanelRect.yMin : Screen.height - margin;
                float y = Mathf.Clamp(hud.OccupiedRect.yMax + dock, margin, Mathf.Max(margin, inventoryTop - height - dock));
                card = new Rect(margin, y, width, height);
                var playerRect = BoundsRect(player.gameObject);
                if (playerRect.HasValue && card.Overlaps(playerRect.Value)) card.x = Screen.width - width - margin;
            }
            else if (!target.HasValue) card = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            else
            {
                Rect anchor = target.Value;
                float y = Mathf.Clamp(anchor.center.y - height * .5f, margin, Mathf.Max(margin, Screen.height - height - margin));
                if (anchor.xMax + gap + width < Screen.width - margin) card = new Rect(anchor.xMax + gap, y, width, height);
                else if (anchor.xMin - gap - width > margin) card = new Rect(anchor.xMin - gap - width, y, width, height);
                else
                {
                    float x = Mathf.Clamp(anchor.center.x - width * .5f, margin, Mathf.Max(margin, Screen.width - width - margin));
                    float above = anchor.yMin - gap - height;
                    card = new Rect(x, above > margin ? above : Mathf.Min(anchor.yMax + gap, Screen.height - height - margin), width, height);
                }
            }
            card.y += (1f - Mathf.Clamp01((Time.unscaledTime - stepStarted) / slideSeconds)) * slideDistance * scale;
            card.y = Mathf.Clamp(card.y, margin, Mathf.Max(margin, Screen.height - height - margin));
            if (docked && card.Overlaps(hud.OccupiedRect)) card.x = Screen.width - width - margin;
            rect.pivot = new Vector2(0f, 1f);
            rect.position = new Vector3(card.xMin, Screen.height - card.yMin, 0f);
        }

        private string ResolveStep(PlayerCharacter player)
        {
            if (tutorial.IsSectionComplete) return tutorial.IsFinalSection ? "done-all" : "done-section";
            switch (tutorial.CurrentSection)
            {
                case TutorialSection.Movement: return player.HasRolled ? "move-walk" : "move-roll";
                case TutorialSection.Items:
                    bool holding = player.Inventory != null && player.Inventory.Items.Count > 0;
                    if (!holding && tutorial.TutorialItem != null && !tutorial.TutorialItemPickedUp)
                        return player.HasRolled ? "items-pickup" : "items-find";
                    return holding ? (player.HasRolled ? "items-later" : "items-use") : "items-walk";
                case TutorialSection.Obstacles:
                    var storage = tutorial.TutorialObstacle;
                    if (storage != null && storage.IsMenuOpen) return "obstacles-take";
                    if (storage != null)
                    {
                        var delta = player.GridPosition - storage.GridPosition;
                        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1) return "obstacles-open";
                    }
                    return "obstacles-find";
                case TutorialSection.EventTriggers: return tutorial.ShowsAllEventTypes ? "events-all" : "events";
                default: return "exit";
            }
        }

        // Read lesson controls from the same action references as gameplay so rebinding also updates the tutorial.
        private InputAction ResolveAction(TutorialAction action)
        {
            var input = GameplayInputContext.Current;
            if (input == null) return null;
            switch (action)
            {
                case TutorialAction.Roll: return input.RollDice;
                case TutorialAction.Move:
                    return context.ActivePlayer != null && !context.ActivePlayer.HasRolled ? input.RollDice : input.Move;
                case TutorialAction.Interact: return input.Interact;
                case TutorialAction.UseItem: return input.UseItem;
                default: return null;
            }
        }

        private Rect? ResolveTarget(TutorialTarget target, PlayerCharacter player)
        {
            switch (target)
            {
                case TutorialTarget.Hud: return hud.OccupiedRect;
                case TutorialTarget.Player: return BoundsRect(player.gameObject);
                case TutorialTarget.Item: return tutorial.TutorialItem != null ? BoundsRect(tutorial.TutorialItem.gameObject) : null;
                case TutorialTarget.Inventory: return inventory.PanelRect.width > 0f ? inventory.PanelRect : (Rect?)null;
                case TutorialTarget.Storage: return tutorial.TutorialObstacle != null ? BoundsRect(tutorial.TutorialObstacle.gameObject) : null;
                case TutorialTarget.Event:
                    EventTileMarker nearest = null;
                    float best = float.MaxValue;
                    foreach (var marker in FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None))
                    {
                        float distance = (marker.transform.position - player.transform.position).sqrMagnitude;
                        if (distance < best) { best = distance; nearest = marker; }
                    }
                    return nearest != null ? BoundsRect(nearest.gameObject) : null;
                case TutorialTarget.Exit:
                    var map = player.GridMap;
                    if (map == null) return null;
                    for (int y = 0; y < map.Height; y++)
                        for (int x = 0; x < map.Width; x++)
                            if (map.IsExit(new Vector2Int(x, y)))
                                return WorldRect(new Bounds(map.GridToWorld(new Vector2Int(x, y)), Vector3.one));
                    return null;
                default: return null;
            }
        }

        private Rect? BoundsRect(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            bounds.Expand(new Vector3(targetPadding.x, targetPadding.y, 0f));
            return WorldRect(bounds);
        }

        private Rect? WorldRect(Bounds bounds)
        {
            var camera = context.WorldCamera;
            if (camera == null) return null;
            var min = camera.WorldToScreenPoint(bounds.min);
            var max = camera.WorldToScreenPoint(bounds.max);
            if (min.z <= 0f || max.z <= 0f) return null;
            return Rect.MinMaxRect(min.x, Screen.height - max.y, max.x, Screen.height - min.y);
        }

        public void Restart() { tutorial.RestartCurrentSection(); }
        public void Skip() { tutorial.CompleteCurrentSection(); }
        public void Continue()
        {
            //Interact already advances lessons; UI Submit can share that physical binding.
            var interact = GameplayInputContext.Current != null ? GameplayInputContext.Current.Interact : null;
            if (tutorial.IsSectionComplete && (interact == null || !interact.WasPressedThisFrame()))
                tutorial.AdvanceToNextSection();
        }
        public void Replay()
        {
            if (string.IsNullOrEmpty(firstTutorialScene)) tutorial.GoToSection(TutorialSection.Movement);
            else tutorial.LoadTutorialScene(firstTutorialScene);
        }
    }
}
