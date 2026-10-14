using Escape4Now.GameModes;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.UI
{
    //Shows the on-screen tutorial for Tutorial_Functionality: a step card that explains what to do,
    //a highlight around the thing to look at, and buttons to restart, skip, or continue each section.
    public sealed class TutorialOverlay : MonoBehaviour
    {
        [SerializeField] private Tutorial_Functionality tutorial;
        //Which part of the tutorial this scene is, for the "PART 1 OF 3" label and progress dots.
        [SerializeField, Min(1)] private int partNumber = 1;
        [SerializeField, Min(1)] private int partCount = 3;
        //Name of the next scene's lesson, shown when this part is done. Empty on the last part.
        [SerializeField] private string nextPartName = "";
        //Scene that "Play again" goes back to. Empty restarts this scene from step 1.
        [SerializeField] private string firstTutorialScene = "";
        [SerializeField, Min(260f)] private float cardWidth = 400f;
        //How long the screen stays dimmed around a new highlight before fading so the map is easy to see.
        [SerializeField, Min(0f)] private float dimSeconds = 1.6f;
        [SerializeField] private Color cardColor = new Color(0.03f, 0.06f, 0.12f, 0.96f);
        [SerializeField] private Color accentColor = new Color(0.4f, 0.72f, 1f, 1f);
        [SerializeField] private Color doneColor = new Color(0.3f, 0.82f, 0.48f, 1f);
        [SerializeField] private Color highlightColor = new Color(0.5f, 0.77f, 1f, 1f);

        //The turn panel drawn by PlayerCharacter sits at a fixed place on screen.
        private static readonly Rect TurnPanelRect = new Rect(16f, 85f, 360f, 235f);

        private PlayerCharacter player;
        private IsometricMapTemplate map;
        private ItemPickupBar itemBar;
        private ObstacleStorage locker;
        private EventTileMarker warpMarker;
        private Vector3? exitWorld;

        private string lastStepKey = "";
        private float stepStartTime;

        private GUIStyle stepStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle keyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle primaryButtonStyle;

        //What the card says right now, and what it points at.
        private struct Step
        {
            public string key;
            public string title;
            public string body;
            public string[] keys;
            public Rect? target;
        }

        private void Start()
        {
            if (tutorial == null) tutorial = FindFirstObjectByType<Tutorial_Functionality>();
            player = FindFirstObjectByType<PlayerCharacter>();
            map = FindFirstObjectByType<IsometricMapTemplate>();
            itemBar = FindFirstObjectByType<ItemPickupBar>();
        }

        private void OnGUI()
        {
            if (tutorial == null || player == null) return;
            CreateStyles();
            Matrix4x4 oldMatrix = HudScale.Apply();

            Step step = BuildStep();
            float now = Time.unscaledTime;
            if (step.key != lastStepKey)
            {
                lastStepKey = step.key;
                stepStartTime = now;
            }
            float sinceStep = now - stepStartTime;

            if (step.target.HasValue) DrawHighlight(step.target.Value, sinceStep, now);
            //The finish screen dims the whole game behind the card.
            if (tutorial.IsSectionComplete && tutorial.IsFinalSection)
                DrawBox(new Rect(0f, 0f, HudScale.Width, HudScale.Height), new Color(0.01f, 0.02f, 0.05f, 0.7f * Mathf.Clamp01(sinceStep / 0.4f)));
            DrawCard(step, sinceStep);
            GUI.matrix = oldMatrix;
        }

        //Picks the card for the current section, based on what the player has already done in it.
        private Step BuildStep()
        {
            TutorialSection section = tutorial.CurrentSection;
            if (tutorial.IsSectionComplete) return BuildCompleteStep(section);

            switch (section)
            {
                case TutorialSection.Movement:
                    if (!player.HasRolled)
                        return MakeStep("move-roll", "Roll the dice",
                            "Welcome to Escape 4 Now! Every turn starts with a dice roll. The number you roll is how many tiles you can move.",
                            new[] { "SPACE" }, TurnPanelRect);
                    return MakeStep("move-walk", "Move around",
                        "Move one tile per key press. Use up all your moves to finish this step. Moves left: " + player.MovesRemaining + ".",
                        new[] { "W", "A", "S", "D" }, PlayerRect());

                case TutorialSection.Items:
                    bool holdingItem = player.Inventory != null && player.Inventory.Items.Count > 0;
                    Item floorItem = tutorial.TutorialItem;
                    //First find the item on the floor and walk onto it.
                    if (!holdingItem && floorItem != null && !tutorial.TutorialItemPickedUp)
                    {
                        if (!player.HasRolled)
                            return MakeStep("items-find", "Find an item",
                                "Items help you escape. See the red Double Dice on the floor? Roll the dice, then walk onto it to pick it up.",
                                new[] { "SPACE" }, ItemRect(floorItem));
                        return MakeStep("items-pickup", "Pick up the item",
                            "Walk onto the Double Dice. It will go straight into your inventory in the bottom-left. Moves left: " + player.MovesRemaining + ".",
                            new[] { "W", "A", "S", "D" }, ItemRect(floorItem));
                    }
                    if (holdingItem && !player.HasRolled)
                        return MakeStep("items-use", "Use your item",
                            player.name + " has a Double Dice in their inventory (bottom-left). Items are used at the start of your turn, before you roll. Press E to use it: it rolls two dice and adds them together.",
                            new[] { "E" }, ItemBarRect());
                    if (holdingItem)
                        return MakeStep("items-later", "Use it next turn",
                            "You rolled before using your item. Items can only be used before you roll, so use up your moves now, then press E at the start of your next turn.",
                            new[] { "W", "A", "S", "D" }, ItemBarRect());
                    return MakeStep("items-walk", "Move with your bonus roll",
                        "The Double Dice is used up and has left your inventory. Use up all your bonus moves to finish this step. Moves left: " + player.MovesRemaining + ".",
                        new[] { "W", "A", "S", "D" }, PlayerRect());

                case TutorialSection.Obstacles:
                    if (locker == null && tutorial != null) locker = tutorial.TutorialObstacle;
                    if (locker != null && locker.IsMenuOpen)
                        return MakeStep("obstacles-take", "Take the item",
                            "The locker is open. Press ENTER again to take what's inside. It goes into your inventory.",
                            new[] { "ENTER" }, null);
                    if (IsNextToLocker())
                        return MakeStep("obstacles-open", "Open the locker",
                            "You're next to the locker. Press ENTER to open it.",
                            new[] { "ENTER" }, LockerRect());
                    return MakeStep("obstacles-find", "Find the locker",
                        "Red blocks are obstacles: you can't walk through them. Lockers can hold items you can take. Stand right next to the locker.",
                        player.HasRolled ? new[] { "W", "A", "S", "D" } : new[] { "SPACE" }, LockerRect());

                case TutorialSection.EventTriggers:
                    if (tutorial.ShowsAllEventTypes)
                        return MakeStep("events-all", "Special events",
                            "Colored tiles are special events. Green Lucky Roll lets you roll twice, purple Warp teleports you, orange Random is a surprise, blue Freeze skips your next turn, and gray Blackout darkens the map. Roll and step on any of them.",
                            player.HasRolled ? new[] { "W", "A", "S", "D" } : new[] { "SPACE" }, NearestEventRect());
                    return MakeStep("events", "Step on an event tile",
                        "Colored tiles set off events. This purple one is a Warp: it teleports you to another part of the map. Roll and walk onto it.",
                        player.HasRolled ? new[] { "W", "A", "S", "D" } : new[] { "SPACE" }, WarpRect());

                default:
                    return MakeStep("exit", "Escape!",
                        "Last step: get out! Walk through the green EXIT door on the outside wall to escape and win.",
                        player.HasRolled ? new[] { "W", "A", "S", "D" } : new[] { "SPACE" }, ExitRect());
            }
        }

        private Step BuildCompleteStep(TutorialSection section)
        {
            if (tutorial.IsFinalSection)
                return MakeStep("done-all", section == TutorialSection.Exit ? "You escaped!" : "Tutorial complete!",
                    "Tutorial complete! You know how to roll, move, use items, open lockers, use Warp tiles, and escape through the exit. Press ENTER to start the real game. Good luck!",
                    new[] { "ENTER" }, null);

            string[] names = { "", "Movement", "Items", "Lockers", "Warp", "Exit" };
            string next = !tutorial.UsesSceneParts ? "the next step"
                : string.IsNullOrEmpty(nextPartName) ? "the next part" : "part " + (partNumber + 1) + ": " + nextPartName;
            return MakeStep("done-" + section, names[(int)section] + " complete!",
                "Press ENTER to go to " + next + ".", new[] { "ENTER" }, null);
        }

        private static Step MakeStep(string key, string title, string body, string[] keys, Rect? target)
        {
            return new Step { key = key, title = title, body = body, keys = keys, target = target };
        }

        //Dims everything except the target for a moment, then leaves a pulsing outline around it.
        private void DrawHighlight(Rect target, float sinceStep, float now)
        {
            Rect ring = new Rect(target.x - 6f, target.y - 6f, target.width + 12f, target.height + 12f);
            float dim = dimSeconds <= 0f ? 0f : 0.65f * Mathf.Clamp01(1f - (sinceStep - dimSeconds) / 0.8f);
            if (dim > 0.01f)
            {
                Color shade = new Color(0.01f, 0.02f, 0.05f, dim);
                DrawBox(new Rect(0f, 0f, HudScale.Width, ring.y), shade);
                DrawBox(new Rect(0f, ring.yMax, HudScale.Width, HudScale.Height - ring.yMax), shade);
                DrawBox(new Rect(0f, ring.y, ring.x, ring.height), shade);
                DrawBox(new Rect(ring.xMax, ring.y, HudScale.Width - ring.xMax, ring.height), shade);
            }

            Color line = highlightColor;
            line.a = 0.55f + 0.45f * Mathf.Sin(now * 5f);
            DrawOutline(ring, 3f, line);
        }

        //Draws the step card next to the target, or in the lower middle of the screen when there is none.
        private void DrawCard(Step step, float sinceStep)
        {
            bool complete = tutorial.IsSectionComplete;
            float padding = 18f;
            float innerWidth = cardWidth - padding * 2f;
            float bodyHeight = bodyStyle.CalcHeight(new GUIContent(step.body), innerWidth);
            float height = padding + 18f + 32f + bodyHeight + 12f + (step.keys.Length > 0 ? 40f : 0f) + 38f + padding;
            //When the step points at something on the map, the card sits on the left so it never covers the player.
            //"Part complete" cards go there too, since the player is still standing on the map.
            bool onMap = step.key == "move-walk" || step.key.StartsWith("items-walk") || step.key.StartsWith("obstacles")
                || step.key.StartsWith("events") || step.key == "exit" || (complete && !tutorial.IsFinalSection);
            Rect card = onMap ? DockLeft(height) : PlaceCard(step.target, height);

            //Slides up slightly when a new card appears.
            float slide = Mathf.Clamp01(sinceStep / 0.25f);
            card.y += (1f - slide) * 12f;

            //Dark game panel with a glowing border in the step's color, like the other HUD panels.
            Color edge = complete ? doneColor : accentColor;
            DrawBox(new Rect(card.x + 5f, card.y + 7f, card.width, card.height), new Color(0f, 0f, 0f, 0.45f));
            DrawBox(new Rect(card.x - 2f, card.y - 2f, card.width + 4f, card.height + 4f), edge);
            DrawBox(card, cardColor);
            DrawBox(new Rect(card.x, card.y, card.width, 4f), new Color(edge.r, edge.g, edge.b, 0.35f));

            float x = card.x + padding;
            float y = card.y + padding;
            stepStyle.normal.textColor = complete ? doneColor : accentColor;
            //Three-scene tutorial counts parts; Cobin's single scene counts its five sections.
            bool parts = tutorial.UsesSceneParts;
            int number = parts ? partNumber : (int)tutorial.CurrentSection;
            int total = parts ? partCount : 5;
            string unit = parts ? "PART " : "STEP ";
            string label = complete
                ? (tutorial.IsFinalSection ? "TUTORIAL DONE" : unit + number + " OF " + total + " DONE")
                : unit + number + " OF " + total;
            GUI.Label(new Rect(x, y, innerWidth, 18f), label, stepStyle);
            y += 18f;
            GUI.Label(new Rect(x, y, innerWidth, 32f), step.title, titleStyle);
            y += 32f;
            GUI.Label(new Rect(x, y, innerWidth, bodyHeight), step.body, bodyStyle);
            y += bodyHeight + 12f;

            //Keys to press, drawn as keyboard keys.
            if (step.keys.Length > 0)
            {
                float kx = x;
                foreach (string key in step.keys)
                {
                    float w = Mathf.Max(34f, keyStyle.CalcSize(new GUIContent(key)).x + 18f);
                    //Light keycaps with a darker bottom edge stand out on the dark card.
                    DrawBox(new Rect(kx, y + 3f, w, 30f), new Color(0.45f, 0.52f, 0.6f, 1f));
                    DrawBox(new Rect(kx, y, w, 30f), new Color(0.91f, 0.94f, 0.97f, 1f));
                    GUI.Label(new Rect(kx, y, w, 30f), key, keyStyle);
                    kx += w + 6f;
                }
                y += 40f;
            }

            //Progress dots: done parts green, current one wide.
            int current = number;
            float dx = x;
            for (int i = 1; i <= total; i++)
            {
                bool isCurrent = i == current;
                Color dot = i < current || (isCurrent && complete) ? doneColor
                    : isCurrent ? accentColor : new Color(1f, 1f, 1f, 0.2f);
                float w = isCurrent ? 22f : 8f;
                DrawBox(new Rect(dx, y + 13f, w, 8f), dot);
                dx += w + 6f;
            }

            DrawButtons(new Rect(x, y, innerWidth, 34f), complete);
        }

        //Restart and Skip while a section is running, Continue (or Play again) once it is done.
        private void DrawButtons(Rect row, bool complete)
        {
            if (complete)
            {
                if (tutorial.IsFinalSection)
                {
                    if (GUI.Button(new Rect(row.xMax - 230f, row.y, 110f, 34f), "Play again", buttonStyle))
                    {
                        if (string.IsNullOrEmpty(firstTutorialScene)) tutorial.GoToSection(TutorialSection.Movement);
                        else tutorial.LoadTutorialScene(firstTutorialScene);
                    }
                    if (GUI.Button(new Rect(row.xMax - 112f, row.y, 112f, 34f), "Finish", primaryButtonStyle))
                        tutorial.AdvanceToNextSection();
                }
                else if (GUI.Button(new Rect(row.xMax - 112f, row.y, 112f, 34f), "Continue", primaryButtonStyle))
                {
                    tutorial.AdvanceToNextSection();
                }
                return;
            }

            if (GUI.Button(new Rect(row.xMax - 196f, row.y, 90f, 34f), "Restart", buttonStyle))
                tutorial.RestartCurrentSection();
            if (GUI.Button(new Rect(row.xMax - 98f, row.y, 98f, 34f), "Skip step", buttonStyle))
                tutorial.CompleteCurrentSection();
        }

        //Puts the card to the right of the target, else to its left, else above or below it.
        //Left side of the screen, under the turn panel and above the inventory.
        private Rect DockLeft(float height)
        {
            float margin = 16f;
            float inventoryTop = itemBar != null && itemBar.PanelRect.height > 0f ? itemBar.PanelRect.y : HudScale.Height - 150f;
            float y = Mathf.Clamp(TurnPanelRect.yMax + 12f, margin, Mathf.Max(margin, inventoryTop - height - 12f));
            Rect card = new Rect(margin, y, cardWidth, height);
            //If the player is standing behind the left card (for example after a Warp), it moves to the right edge instead.
            Rect? playerRect = PlayerRect();
            if (playerRect.HasValue && card.Overlaps(playerRect.Value))
                card.x = HudScale.Width - cardWidth - margin;
            return card;
        }

        private Rect PlaceCard(Rect? target, float height)
        {
            float margin = 16f;
            if (!target.HasValue)
                return new Rect((HudScale.Width - cardWidth) * 0.5f, HudScale.Height * 0.5f - height * 0.5f, cardWidth, height);

            Rect t = target.Value;
            float y = Mathf.Clamp(t.center.y - height * 0.5f, margin, HudScale.Height - height - margin);
            if (t.xMax + 24f + cardWidth < HudScale.Width - margin) return new Rect(t.xMax + 24f, y, cardWidth, height);
            if (t.x - 24f - cardWidth > margin) return new Rect(t.x - 24f - cardWidth, y, cardWidth, height);

            float x = Mathf.Clamp(t.center.x - cardWidth * 0.5f, margin, HudScale.Width - cardWidth - margin);
            float above = t.y - 24f - height;
            return new Rect(x, above > margin ? above : Mathf.Min(t.yMax + 24f, HudScale.Height - height - margin), cardWidth, height);
        }

        private Rect? PlayerRect()
        {
            return WorldRect(player.transform.position, 0.9f, 1.1f, 0.1f);
        }

        //Box around an item lying on the floor.
        private static Rect? ItemRect(Item item)
        {
            return item == null || !item.gameObject.activeInHierarchy ? null : WorldRect(item.transform.position, 1.1f, 1f, -0.3f);
        }

        private Rect? ItemBarRect()
        {
            if (itemBar == null || itemBar.PanelRect.width <= 0f) return null;
            return itemBar.PanelRect;
        }

        private Rect? LockerRect()
        {
            if (locker == null && tutorial != null) locker = tutorial.TutorialObstacle;
            return locker == null ? null : BoundsRect(locker.gameObject);
        }

        //Box that fits tightly around everything drawn for an object, such as the locker block.
        private static Rect? BoundsRect(GameObject target)
        {
            Camera camera = Camera.main;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (camera == null || renderers.Length == 0) return null;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 min = camera.WorldToScreenPoint(bounds.min);
            Vector3 max = camera.WorldToScreenPoint(bounds.max);
            return HudScale.FromScreen(Rect.MinMaxRect(min.x, Screen.height - max.y, max.x, Screen.height - min.y));
        }

        private Rect? WarpRect()
        {
            if (warpMarker == null)
            {
                foreach (EventTileMarker marker in FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None))
                {
                    if (marker.EventType == MapEventType.Warp) warpMarker = marker;
                }
            }
            return warpMarker == null ? null : WorldRect(warpMarker.transform.position, 1.4f, 0.8f, -0.4f);
        }

        //Highlights the event tile closest to the player.
        private Rect? NearestEventRect()
        {
            EventTileMarker nearest = null;
            float best = float.MaxValue;
            foreach (EventTileMarker marker in FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None))
            {
                float distance = (marker.transform.position - player.transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = marker;
                }
            }
            return nearest == null ? null : WorldRect(nearest.transform.position, 1.4f, 0.8f, -0.4f);
        }

        private Rect? ExitRect()
        {
            if (!exitWorld.HasValue && map != null)
            {
                for (int y = 0; y < map.Height && !exitWorld.HasValue; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (map.IsExit(new Vector2Int(x, y)))
                        {
                            exitWorld = map.GridToWorld(new Vector2Int(x, y));
                            break;
                        }
                    }
                }
            }
            return exitWorld.HasValue ? WorldRect(exitWorld.Value, 1.6f, 0.9f, -0.45f) : null;
        }

        //Turns a box in world units (width, height, measured up from the bottom) into a screen rectangle.
        private static Rect? WorldRect(Vector3 bottomCenter, float width, float height, float bottomOffset)
        {
            Camera camera = Camera.main;
            if (camera == null) return null;
            Vector3 a = camera.WorldToScreenPoint(bottomCenter + new Vector3(-width * 0.5f, bottomOffset, 0f));
            Vector3 b = camera.WorldToScreenPoint(bottomCenter + new Vector3(width * 0.5f, bottomOffset + height, 0f));
            if (a.z < 0f || b.z < 0f) return null;
            return HudScale.FromScreen(Rect.MinMaxRect(a.x, Screen.height - b.y, b.x, Screen.height - a.y));
        }

        private bool IsNextToLocker()
        {
            if (locker == null) return false;
            ObstacleGridControl grid = locker.GetComponent<ObstacleGridControl>();
            if (grid == null) return false;
            Vector2Int offset = player.GridPosition - grid.GridPosition;
            return Mathf.Abs(offset.x) + Mathf.Abs(offset.y) == 1;
        }

        private static void DrawBox(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawOutline(Rect rect, float thickness, Color color)
        {
            DrawBox(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawBox(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawBox(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawBox(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void CreateStyles()
        {
            if (stepStyle != null) return;
            stepStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = Color.white;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.78f, 0.84f, 0.91f, 1f);
            keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            keyStyle.normal.textColor = new Color(0.04f, 0.07f, 0.13f, 1f);

            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            buttonStyle.normal.background = MakeTexture(new Color(1f, 1f, 1f, 0.1f));
            buttonStyle.hover.background = MakeTexture(new Color(1f, 1f, 1f, 0.2f));
            buttonStyle.active.background = buttonStyle.hover.background;
            buttonStyle.normal.textColor = buttonStyle.hover.textColor = buttonStyle.active.textColor = new Color(0.85f, 0.9f, 0.95f, 1f);

            //Green go button, like a game's "continue".
            primaryButtonStyle = new GUIStyle(buttonStyle);
            primaryButtonStyle.normal.background = MakeTexture(doneColor);
            primaryButtonStyle.hover.background = MakeTexture(Color.Lerp(doneColor, Color.white, 0.2f));
            primaryButtonStyle.active.background = primaryButtonStyle.hover.background;
            primaryButtonStyle.normal.textColor = primaryButtonStyle.hover.textColor = primaryButtonStyle.active.textColor = new Color(0.02f, 0.1f, 0.05f, 1f);
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
