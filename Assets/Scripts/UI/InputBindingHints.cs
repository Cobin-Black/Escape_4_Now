using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Escape4Now.UI
{
    public static class InputBindingHints
    {
        public static void GetPrimaryLabels(InputAction action, string group, List<string> labels)
        {
            labels.Clear();
            if (action == null) return;
            ReadBindings(action, group, labels);
            if (labels.Count == 0 && !string.IsNullOrEmpty(group)) ReadBindings(action, null, labels);
        }

        private static void ReadBindings(InputAction action, string group, List<string> labels)
        {
            var mask = InputBinding.MaskByGroup(group);
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isPartOfComposite) continue;
                if (binding.isComposite)
                {
                    var parts = new List<int>();
                    for (int part = i + 1; part < action.bindings.Count && action.bindings[part].isPartOfComposite; part++)
                        if ((string.IsNullOrEmpty(group) || mask.Matches(action.bindings[part]))
                            && !parts.Exists(existing => action.bindings[existing].name == action.bindings[part].name)) parts.Add(part);
                    parts.Sort((left, right) => DirectionOrder(action.bindings[left].name).CompareTo(DirectionOrder(action.bindings[right].name)));
                    foreach (int part in parts) AddLabel(action, part, labels);
                }
                else if (string.IsNullOrEmpty(group) || mask.Matches(binding)) AddLabel(action, i, labels);
                if (labels.Count > 0) return;
            }
        }

        private static int DirectionOrder(string name)
        {
            switch (name)
            {
                case "up": return 0;
                case "left": return 1;
                case "down": return 2;
                case "right": return 3;
                default: return 4;
            }
        }

        private static void AddLabel(InputAction action, int index, List<string> labels)
        {
            string label = action.GetBindingDisplayString(index, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (!string.IsNullOrEmpty(label) && !labels.Contains(label)) labels.Add(label.ToUpperInvariant());
        }
    }
}
