using UnityEngine;

namespace Escape4Now.UI
{
    //Scales on-screen panels with the window size, so the HUD looks the same at 720p, 1080p, or 4K.
    //Panels are laid out on a virtual screen that is always 720 units tall.
    public static class HudScale
    {
        public const float ReferenceHeight = 720f;

        public static float Factor => Mathf.Clamp(Screen.height / ReferenceHeight, 0.6f, 4f);
        //Size of the virtual screen panels are laid out on.
        public static float Width => Screen.width / Factor;
        public static float Height => Screen.height / Factor;

        //Call at the start of OnGUI. Returns the previous matrix so it can be restored at the end.
        public static Matrix4x4 Apply()
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Factor, Factor, 1f));
            return previous;
        }

        //Converts a rectangle in real screen pixels, such as from a camera, to the scaled HUD.
        public static Rect FromScreen(Rect screenRect)
        {
            float f = Factor;
            return new Rect(screenRect.x / f, screenRect.y / f, screenRect.width / f, screenRect.height / f);
        }
    }
}
