//Animates a saved puzzle piece. It never moves a player or changes a puzzle answer.
using UnityEngine;
using UnityEngine.UI;
using Escape4Now.Puzzles;

namespace Escape4Now.UI
{
    public sealed class PuzzlePieceMotion : MonoBehaviour
    {
        [Header("Saved Piece Parts")]
        [SerializeField] private RectTransform artwork;
        [SerializeField] private RectTransform mechanism;
        [SerializeField] private RectTransform lever;
        [SerializeField] private Image lamp;
        [SerializeField] private CanvasGroup artworkGroup;
        [SerializeField] private GameObject batteryShape, supplyShape;
        [Header("Response")]
        [SerializeField, Min(1)] private float response = 18f;
        [SerializeField] private float hoverScale = 1.035f;
        [SerializeField] private float pressedScale = .94f;
        [SerializeField, Min(.01f)] private float moveSeconds = .22f;
        [SerializeField, Min(.01f)] private float pressSeconds = .1f;
        [SerializeField] private float leverTravel = 9f;
        [SerializeField] private Color poweredColor = new Color(.25f,1,.7f,1);
        [SerializeField] private Color unpoweredColor = new Color(.2f,.27f,.3f,1);
        private bool hovered, pressed, dragging, moving, inserted;
        private Vector3 destination;
        private float moveRemaining, pressRemaining, powerPhase;
        public RectTransform Artwork => artwork;
        public void SetTheme(SurvivalPuzzleType type)
        {
            if (lever != null) lever.parent.gameObject.SetActive(type == SurvivalPuzzleType.PowerSwitches || type == SurvivalPuzzleType.Breaker);
            if (mechanism != null) mechanism.gameObject.SetActive(type == SurvivalPuzzleType.Circuit);
            if (lamp != null) lamp.gameObject.SetActive(type != SurvivalPuzzleType.ArrangeNumbers);
            //Matching icons now live in the piece view, in their own space beside the caption.
            if (batteryShape != null) batteryShape.SetActive(false);
            if (supplyShape != null) supplyShape.SetActive(false);
        }

        public void Hover(bool value) { hovered = value; }
        public void Press(bool value) { pressed = value; }
        public void Pulse() { pressRemaining = pressSeconds; }
        public void Rotate() { if (mechanism != null) mechanism.localRotation = Quaternion.Euler(0,0,-90); }
        public void SetLever(bool on)
        {
            if (lever != null) lever.anchoredPosition = Vector2.Lerp(lever.anchoredPosition,
                new Vector2(0,on ? leverTravel : -leverTravel),Time.deltaTime*response);
        }
        public void SetPowered(bool on)
        {
            if (lamp != null) lamp.color = on ? Color.Lerp(poweredColor,Color.white,(Mathf.Sin(powerPhase)+1)*.15f) : unpoweredColor;
        }

        //The layout slot stays fixed while its visible piece moves with the pointer.
        public void Drag(Vector2 screen, Camera camera)
        {
            if (artwork == null) return;
            dragging = true; moving = false;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)artwork.parent,screen,camera,out var position))
                artwork.position = position;
        }
        public void ReturnHome() { dragging = false; MoveTo(transform.position,false); }
        public void Insert(Vector3 slot) { dragging = false; MoveTo(slot,true); }
        public void AnimateFrom(Vector3 position)
        { if (artwork == null) return; artwork.position = position; MoveTo(transform.position,false); }
        private void MoveTo(Vector3 target,bool hideWhenDone)
        { destination = target; moving = true; inserted = hideWhenDone; moveRemaining = moveSeconds; }
        public void ResetPiece()
        {
            moving = dragging = inserted = hovered = pressed = false;
            if (artwork != null) { artwork.anchoredPosition = Vector2.zero; artwork.localScale = Vector3.one; }
            if (artworkGroup != null) artworkGroup.alpha = 1;
        }
        private void Update()
        {
            if (Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            powerPhase += Time.deltaTime*response;
            pressRemaining -= Time.deltaTime;
            if (artwork != null)
            {
                float scale = pressed || pressRemaining > 0 ? pressedScale : hovered ? hoverScale : 1;
                artwork.localScale = Vector3.Lerp(artwork.localScale,Vector3.one*scale,Time.deltaTime*response);
                if (moving && !dragging)
                {
                    moveRemaining -= Time.deltaTime;
                    artwork.position = Vector3.Lerp(artwork.position,destination,Time.deltaTime*response);
                    if (moveRemaining <= 0) { artwork.position = destination; moving = false; }
                }
                if (artworkGroup != null && inserted && !moving) artworkGroup.alpha = 0;
            }
            if (mechanism != null) mechanism.localRotation = Quaternion.Lerp(mechanism.localRotation,Quaternion.identity,Time.deltaTime*response);
        }
        private void OnDisable() { hovered = pressed = false; }
    }
}
