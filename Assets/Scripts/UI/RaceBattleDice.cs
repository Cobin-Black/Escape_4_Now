// Two dice tumble above the players in a Race key battle and land on the real rolls before the result screen opens.
// The rolls are already decided by RaceKeyBattle; this only shows them. Online, the host sends who is battling and the rolls.
// The dice are drawn on the match canvas at a fixed screen size, so they stay readable however far the camera is zoomed out.

using System;
using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.UI
{
    public sealed class RaceBattleDice : MonoBehaviour
    {
        [Serializable]
        public sealed class Look
        {
            [Min(8f), Tooltip("Width of each die on screen, in canvas units.")]
            public float size = 64f;
            [Min(0f), Tooltip("Gap between the centers of the two dice, in canvas units.")]
            public float spacing = 84f;
            [Tooltip("How far above the top of the taller player the dice sit, in canvas units. Leaves room for the name tag.")]
            public float raise = 78f;
            [Min(0f), Tooltip("How high the dice hop while tumbling, in canvas units.")]
            public float hopHeight = 26f;
            [Min(0f), Tooltip("Tilt in degrees while tumbling.")]
            public float wobble = 22f;
            [Range(0f, 1f), Tooltip("How faded the losing die gets once both have landed.")]
            public float loserAlpha = 0.45f;
            [Min(.01f), Tooltip("Seconds for the dice to grow into view.")]
            public float popInSeconds = .18f;
            [Min(.01f), Tooltip("Seconds for the small bounce after landing.")]
            public float landPopSeconds = .25f;
            [Min(0f), Tooltip("Space between the battle dice, the HUD and the screen edge.")]
            public float screenPadding = 12f;
        }

        //True while the dice are tumbling or showing their landed faces, so the result screen and key markers wait.
        public static bool Rolling { get; private set; }

        private MatchUIContext context;
        private RaceKeyBattle battle;
        private DiceDisplay template;
        private Look look;
        private RectTransform layer;
        private Die challengerDie, opponentDie;
        private int shownBattle = -1;
        private bool animating;
        private float startTime;
        private PlayerCharacter challenger, opponent;
        private int challengerRoll, opponentRoll;

        //Called once by RaceBattleUI right after adding this component.
        public void Setup(MatchUIContext matchContext, RaceKeyBattle keyBattle, DiceDisplay dieTemplate, Look settings)
        {
            context = matchContext;
            battle = keyBattle;
            template = dieTemplate;
            look = settings ?? new Look();
        }

        //Timing comes from RaceKeyBattle, so the dice and the result screen always agree.
        private float RollSeconds => battle != null ? battle.DiceRollSeconds : 1.4f;
        private float HoldSeconds => battle != null ? battle.DiceHoldSeconds : 0.7f;

        private void Update()
        {
            if (ReadBattle(out int number, out PlayerCharacter a, out PlayerCharacter b, out int rollA, out int rollB))
            {
                if (number != shownBattle)
                {
                    shownBattle = number;
                    challenger = a;
                    opponent = b;
                    challengerRoll = rollA;
                    opponentRoll = rollB;
                    startTime = Time.time;
                    animating = true;
                }
            }
            else
            {
                //The battle ended or was cancelled, so the dice go with it.
                animating = false;
            }

            float t = Time.time - startTime;
            if (animating && (challenger == null || opponent == null || t >= RollSeconds + HoldSeconds))
                animating = false;
            Rolling = animating;
            if (!animating || !Draw(t)) Hide();
        }

        //Reads the current battle from the host's shared state online, or from the local battle otherwise.
        private bool ReadBattle(out int number, out PlayerCharacter a, out PlayerCharacter b, out int rollA, out int rollB)
        {
            number = rollA = rollB = 0;
            a = b = null;
            var lan = LanMultiplayerSession.Instance;
            if (lan != null && lan.UsesRaceMatchUI)
            {
                LanSnapshot state = lan.LatestState;
                var players = context != null && context.Session != null ? context.Session.Participants : null;
                if (state == null || players == null || string.IsNullOrEmpty(state.battleMessage)
                    || state.battleChallenger < 0 || state.battleChallenger >= players.Count
                    || state.battleOpponent < 0 || state.battleOpponent >= players.Count) return false;
                number = state.battleNumber;
                a = players[state.battleChallenger];
                b = players[state.battleOpponent];
                rollA = state.battleChallengerRoll;
                rollB = state.battleOpponentRoll;
                return a != null && b != null;
            }
            if (battle == null || LanMultiplayerSession.Active || battle.Challenger == null || battle.Opponent == null) return false;
            number = battle.BattleNumber;
            a = battle.Challenger;
            b = battle.Opponent;
            rollA = battle.ChallengerRoll;
            rollB = battle.OpponentRoll;
            return true;
        }

        //Places both dice above the two players on screen. Returns false when they can't be placed this frame.
        private bool Draw(float t)
        {
            var camera = context != null ? context.WorldCamera : null;
            if (camera == null || template == null || !EnsureLayer()) return false;

            //The players share a tile, so the dice sit side by side above the taller one, each on its owner's side.
            Vector3 top = Mathf.Max(Top(challenger), Top(opponent)) * Vector3.up;
            Vector3 middle = (challenger.transform.position + opponent.transform.position) * 0.5f;
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(middle.x, top.y, middle.z));
            if (screen.z <= 0f) return false;
            float side = camera.WorldToScreenPoint(challenger.transform.position).x
                <= camera.WorldToScreenPoint(opponent.transform.position).x ? -1f : 1f;

            float scale = layer.lossyScale.x > 0f ? layer.lossyScale.x : 1f;
            Vector3 center = screen + Vector3.up * look.raise * scale;
            // Keep both dice on screen when a battle happens near an edge. They stay over the players even beside
            // the HUD, which they draw on top of.
            float halfWidth = (look.spacing + look.size * 1.5f) * .5f * scale;
            float halfHeight = (look.size + look.hopHeight) * scale;
            float padding = look.screenPadding * scale;
            center.x = Mathf.Clamp(center.x, halfWidth + padding, Mathf.Max(halfWidth + padding, Screen.width - halfWidth - padding));
            center.y = Mathf.Clamp(center.y, halfHeight + padding, Mathf.Max(halfHeight + padding, Screen.height - halfHeight - padding));
            Vector3 offset = Vector3.right * look.spacing * 0.5f * scale;
            bool challengerWins = challengerRoll > opponentRoll;
            if (challengerDie == null) challengerDie = new Die("Challenger Die", layer, template);
            if (opponentDie == null) opponentDie = new Die("Opponent Die", layer, template);
            challengerDie.Show(center + offset * side, challenger.IdentityColor, challengerRoll, challengerWins, 0, t, scale, this);
            opponentDie.Show(center - offset * side, opponent.IdentityColor, opponentRoll, !challengerWins, 1, t, scale, this);
            layer.SetAsLastSibling();
            return true;
        }

        //Top of the player's body in world space, so the dice clear tall and short skins alike.
        private static float Top(PlayerCharacter player)
        {
            var body = player.GetComponent<SpriteRenderer>();
            return body != null ? body.bounds.max.y : player.transform.position.y + 1f;
        }

        //A full-screen layer on the match canvas that holds the dice above the other match UI.
        private bool EnsureLayer()
        {
            if (layer != null) return true;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            var root = new GameObject("Battle Dice", typeof(RectTransform));
            layer = (RectTransform)root.transform;
            layer.SetParent(canvas.rootCanvas.transform, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            return true;
        }

        private void Hide()
        {
            challengerDie?.SetVisible(false);
            opponentDie?.SetVisible(false);
        }

        private void OnDisable()
        {
            Rolling = false;
            Hide();
        }

        private void OnDestroy()
        {
            Rolling = false;
            if (layer != null) Destroy(layer.gameObject);
        }

        //Grows a little past full size and settles back, for the pop when the dice appear.
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // Clone the HUD die so its frame, pips and spacing stay identical.
        private sealed class Die
        {
            private readonly RectTransform root;
            private readonly DiceDisplay display;
            private readonly CanvasGroup group;
            private readonly Vector2 size;

            public Die(string name, RectTransform parent, DiceDisplay template)
            {
                display = Instantiate(template, parent, false);
                display.name = name;
                root = (RectTransform)display.transform;
                size = root.rect.size;
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
                root.sizeDelta = size;
                group = root.GetComponent<CanvasGroup>();
                if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = group.interactable = false;
                root.gameObject.SetActive(false);
            }

            //Draws this die at a screen position for time t since the battle started.
            public void Show(Vector3 screen, Color owner, int result, bool winner, int index, float t, float canvasScale, RaceBattleDice dice)
            {
                Look look = dice.look;
                float roll = dice.RollSeconds;
                root.gameObject.SetActive(true);

                float scale = EaseOutBack(Mathf.Clamp01(t / look.popInSeconds));
                float alpha = 1f;
                float tilt = 0f;
                float hop = 0f;
                int shown;
                if (t < roll)
                {
                    //Tumbling: hops that shrink, a wobble that settles, and faces that change more slowly near the end.
                    float calm = 1f - t / Mathf.Max(0.01f, roll);
                    float phase = index * 1.3f;
                    hop = Mathf.Abs(Mathf.Sin(t * 11f + phase)) * look.hopHeight * calm;
                    tilt = Mathf.Sin(t * 23f + phase) * look.wobble * calm;
                    int step = Mathf.FloorToInt(Mathf.Sqrt(t) * 15f);
                    shown = (step * 5 + index * 3 + result) % 6 + 1;
                    if (shown == result && t > roll * 0.8f) shown = shown % 6 + 1;
                }
                else
                {
                    //Landed: a quick squash, then the winner grows a little and the loser fades.
                    shown = result;
                    float landed = t - roll;
                    scale *= 1f + 0.25f * (1f - Mathf.Clamp01(landed / look.landPopSeconds));
                    float settle = Mathf.Clamp01((landed - look.landPopSeconds) / 0.2f);
                    scale *= winner ? Mathf.Lerp(1f, 1.15f, settle) : Mathf.Lerp(1f, 0.9f, settle);
                    if (!winner) alpha = Mathf.Lerp(1f, look.loserAlpha, settle);
                }

                display.Show(shown, false);
                display.SetPlayerColor(owner);
                group.alpha = alpha;
                root.position = screen + Vector3.up * hop * canvasScale;
                root.localRotation = Quaternion.Euler(0f, 0f, tilt);
                // Scale the complete saved die, including its face and padding.
                root.localScale = Vector3.one * (look.size / Mathf.Max(1f, size.x)) * Mathf.Max(.001f, scale);
            }

            public void SetVisible(bool visible)
            {
                if (root != null && root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            }
        }
    }
}
