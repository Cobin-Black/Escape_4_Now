// Stores editable lesson text and targets, looked up by their tutorial step identifier.

using System;
using UnityEngine;

namespace Escape4Now.UI
{
    public enum TutorialTarget { None, Hud, Player, Item, Inventory, Storage, Event, Exit }
    public enum TutorialAction { None, Roll, Move, Interact, UseItem }
    [CreateAssetMenu(menuName = "Escape 4 Now/Tutorial Lesson Catalog")]
    public sealed class TutorialLessonCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Lesson
        {
            public string id;
            public string title;
            [TextArea(3, 8)] public string body;
            public TutorialTarget target;
            public TutorialAction action;
        }
        [SerializeField] private Lesson[] lessons;
        public Lesson Find(string id)
        {
            if (lessons != null) foreach (var lesson in lessons) if (lesson.id == id) return lesson;
            return null;
        }
    }
}
