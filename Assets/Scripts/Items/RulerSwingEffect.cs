
using UnityEngine;

namespace Escape4Now.Items
{
    public sealed class RulerSwingEffect : MonoBehaviour
    {
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private int segments = 16;
        [SerializeField] private Color swingColor = new Color(1f, 0.85f, 0.3f, 0.8f);

        private LineRenderer line;

        // Draw a temporary arc in the direction of the attack.
        public void Initialize(Vector2 direction, float range, float angle)
        {
            line = GetComponent<LineRenderer>();

            if (line == null)
                line = gameObject.AddComponent<LineRenderer>();

            line.useWorldSpace = false;
            line.positionCount = segments + 1;
            line.startWidth = 0.06f;
            line.endWidth = 0.06f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = swingColor;
            line.endColor = swingColor;
            line.sortingOrder = 100;

            float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i <= segments; i++)
            {
                float currentAngle = centerAngle - angle / 2f +
                                     angle * i / segments;

                float radians = currentAngle * Mathf.Deg2Rad;

                line.SetPosition(i, new Vector3(
                    Mathf.Cos(radians) * range,
                    Mathf.Sin(radians) * range,
                    0f
                ));
            }

            Destroy(gameObject, duration);
        }
    }
}
