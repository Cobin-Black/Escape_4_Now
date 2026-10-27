
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public class MonsterToggleColor : MonoBehaviour
    {
        [SerializeField] private Toggle monsterToggle;
        [SerializeField] private Image background;

        [SerializeField] private Color selectedColor = Color.green;
        [SerializeField] private Color unselectedColor = Color.white;

        // Updates the background color when the Toggle selection changes.
        private void Start()
        {
            monsterToggle.onValueChanged.AddListener(UpdateColor);
            UpdateColor(monsterToggle.isOn);
        }

        // Sets the background color based on whether the Toggle is selected.
        private void UpdateColor(bool isSelected)
        {
            background.color = isSelected ? selectedColor : unselectedColor;
        }

        // Removes the listener when this object is disabled.
        private void OnDestroy()
        {
            monsterToggle.onValueChanged.RemoveListener(UpdateColor);
        }
    }
}
