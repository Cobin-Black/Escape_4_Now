using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Escape4Now.GameModes
{
    //Lists the five tutorial sections in the order they are played.
    public enum TutorialSection { Movement = 1, Items = 2, Obstacles = 3, EventTriggers = 4, Exit = 5 }

    //Runs the tutorial one section at a time. Each section reloads the scene so the player starts back on their default tile,
    //then turns the map's events, obstacles, and exit on or off for that lesson.
    public sealed class Tutorial_Functionality : MonoBehaviour
    {
        //Scene pieces the tutorial controls. Found in the scene when left empty.
        [SerializeField] private IsometricMapTemplate map;
        [SerializeField] private MapEventController mapEvents;
        [SerializeField] private ObstacleSpawner obstacleSpawner;
        [SerializeField] private ItemSpawner itemSpawner;
        [SerializeField] private PlayerCharacter player;

        //Section used when Play mode starts, so a single section can be tested.
        [SerializeField] private TutorialSection startingSection = TutorialSection.Movement;

        //Item placed in the inventory for the Items section.
        [SerializeField] private DoubleDiceItem doubleDicePrefab;

        //Where the forced obstacle and Warp tile go, counted from the player's starting tile.
        [SerializeField] private Vector2Int obstacleOffset = new Vector2Int(1, 0);
        [SerializeField] private Vector2Int warpOffset = new Vector2Int(0, 2);

        //Exit tile for the final section. The exit is hidden in earlier sections so it cannot end them early.
        [SerializeField] private Vector2Int exitPosition = new Vector2Int(9, 0);
        [SerializeField] private bool exitOnlyInFinalSection = true;

        //Keeps the random Double Dice and Custom Dice off the floor so they do not mix with the lessons.
        [SerializeField] private bool disableFloorItems = true;

        //Scene loaded when the tutorial is finished. Empty stays on the tutorial.
        [SerializeField] private string sceneAfterTutorial = "";

        //Section to load next. It survives the scene reload, and is cleared when Play mode starts.
        private static TutorialSection? pendingSection;

        private TutorialSection currentSection;
        private bool sectionComplete;
        private bool playerHadRolled;
        private bool tutorialItemUsed;
        private Item tutorialItem;
        private ObstacleStorage tutorialObstacle;
        private int completedFrame;

        //Interact action from the project-wide Input System asset, used to move on once a section is complete.
        private InputAction interactAction;

        //Tells the tutorial UI what is happening.
        public event System.Action<TutorialSection> SectionStarted;
        public event System.Action<TutorialSection> SectionCompleted;
        public event System.Action TutorialCompleted;

        //Read-only tutorial state for the UI.
        public TutorialSection CurrentSection => currentSection;
        public bool IsSectionComplete => sectionComplete;
        public bool IsFinalSection => currentSection == TutorialSection.Exit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            pendingSection = null;
        }

        //Sets the section's rules before the map, events, and obstacles run their own setup.
        private void Awake()
        {
            //Reaching the exit pauses the game, so undo that from any earlier section.
            Time.timeScale = 1f;
            currentSection = pendingSection ?? startingSection;
            interactAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Interact") : null;
            if (interactAction == null) Debug.LogWarning($"{name}: Input action 'Player/Interact' was not found.");
            FindSceneReferences();
            ApplySectionRules();
        }

        //Listens for the Warp tile being used.
        private void OnEnable()
        {
            if (mapEvents != null) mapEvents.EventTriggered += OnEventTriggered;
        }

        private void OnDisable()
        {
            if (mapEvents != null) mapEvents.EventTriggered -= OnEventTriggered;
        }

        //Gives the section's starting item once the inventory is ready, then announces the section.
        private void Start()
        {
            if (currentSection == TutorialSection.Items) GiveTutorialItem();
            Debug.Log($"[Tutorial] Section {(int)currentSection}: {currentSection}.");
            SectionStarted?.Invoke(currentSection);
        }

        //Checks whether the player has finished the current lesson.
        private void Update()
        {
            if (player == null) return;
            bool finishedMoving = FinishedMovingThisFrame();
            if (sectionComplete)
            {
                //Skip the frame the section ended, so the press that finished it (like opening the obstacle) does not also advance.
                if (Time.frameCount > completedFrame && interactAction != null && interactAction.WasPressedThisFrame())
                    AdvanceToNextSection();
                return;
            }

            switch (currentSection)
            {
                case TutorialSection.Movement:
                    if (finishedMoving) CompleteCurrentSection();
                    break;
                case TutorialSection.Items:
                    if (tutorialItem != null && player.Inventory != null && !player.Inventory.HasItem(tutorialItem))
                        tutorialItemUsed = true;
                    if (tutorialItemUsed && finishedMoving) CompleteCurrentSection();
                    break;
                case TutorialSection.Obstacles:
                    if (tutorialObstacle == null) tutorialObstacle = FindFirstObjectByType<ObstacleStorage>();
                    if (tutorialObstacle != null && !tutorialObstacle.HasItem) CompleteCurrentSection();
                    break;
                case TutorialSection.Exit:
                    if (player.HasReachedExit) CompleteCurrentSection();
                    break;
            }
        }

        //Ends the current section and locks the player until Interact is pressed. Called automatically when the lesson is done, or by the UI to skip it.
        public void CompleteCurrentSection()
        {
            if (sectionComplete) return;
            sectionComplete = true;
            completedFrame = Time.frameCount;
            //The menu lock stops rolling, moving, using items, and opening obstacles. The reload gives the next section a fresh player.
            if (player != null) player.SetInMenu(true);
            RemoveTutorialItem();
            Debug.Log($"[Tutorial] Section {(int)currentSection} complete.");
            SectionCompleted?.Invoke(currentSection);

            if (IsFinalSection)
            {
                Debug.Log("[Tutorial] Tutorial complete.");
                TutorialCompleted?.Invoke();
            }
        }

        //Loads the next section, or leaves the tutorial after the last one.
        public void AdvanceToNextSection()
        {
            if (IsFinalSection)
            {
                pendingSection = null;
                if (!string.IsNullOrEmpty(sceneAfterTutorial))
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(sceneAfterTutorial);
                }
                return;
            }

            GoToSection(currentSection + 1);
        }

        //Starts the current section over from the beginning.
        public void RestartCurrentSection()
        {
            GoToSection(currentSection);
        }

        //Reloads the scene on the chosen section, which puts the player back on their default tile.
        public void GoToSection(TutorialSection section)
        {
            pendingSection = section;
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.name);
        }

        //Fills in any scene piece that was not assigned in the Inspector.
        private void FindSceneReferences()
        {
            if (map == null) map = FindFirstObjectByType<IsometricMapTemplate>();
            if (mapEvents == null && map != null) mapEvents = map.GetComponent<MapEventController>();
            if (obstacleSpawner == null) obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
            if (itemSpawner == null) itemSpawner = FindFirstObjectByType<ItemSpawner>();
            if (player == null) player = FindFirstObjectByType<PlayerCharacter>();
        }

        //Turns events, obstacles, floor items, and the exit on or off for the current section.
        private void ApplySectionRules()
        {
            //The player has not moved yet, so this is their default tile.
            Vector2Int start = player != null ? player.GridPosition : Vector2Int.zero;

            bool useEvents = currentSection == TutorialSection.EventTriggers;
            if (mapEvents != null)
            {
                mapEvents.enabled = useEvents;
                if (useEvents)
                {
                    mapEvents.OnlyPlaceEvents(MapEventType.Warp);
                    mapEvents.ForceEventTile(MapEventType.Warp, start + warpOffset);
                }
            }

            bool useObstacles = currentSection == TutorialSection.Obstacles;
            if (obstacleSpawner != null)
            {
                obstacleSpawner.enabled = useObstacles;
                if (useObstacles)
                {
                    obstacleSpawner.SetObstacleCount(1);
                    obstacleSpawner.SetInteractableChance(1f);
                    obstacleSpawner.ForceObstacleTile(start + obstacleOffset);
                }
            }

            if (itemSpawner != null && disableFloorItems) itemSpawner.enabled = false;

            if (map != null)
            {
                bool isExitSection = currentSection == TutorialSection.Exit;
                map.SetHasExit(isExitSection || !exitOnlyInFinalSection);
                if (isExitSection) map.SetExitPosition(exitPosition);
            }
        }

        //Puts a Double Dice straight into the player's inventory for the Items section.
        private void GiveTutorialItem()
        {
            if (doubleDicePrefab == null || player == null || player.Inventory == null)
            {
                Debug.LogWarning("[Tutorial] Assign the Double Dice prefab and a player with an inventory for the Items section.");
                return;
            }

            DoubleDiceItem item = Instantiate(doubleDicePrefab, transform);
            item.PickUp(player.Inventory);
            if (player.Inventory.HasItem(item))
            {
                tutorialItem = item;
            }
            else
            {
                Destroy(item.gameObject);
            }
        }

        //Takes the tutorial's Double Dice back out of the inventory if it was not used.
        private void RemoveTutorialItem()
        {
            if (tutorialItem == null) return;
            if (player != null && player.Inventory != null) player.Inventory.RemoveItem(tutorialItem);
            Destroy(tutorialItem.gameObject);
            tutorialItem = null;
        }

        //A move ends when the player had a roll last frame and the roll has now been used up.
        private bool FinishedMovingThisFrame()
        {
            bool finished = playerHadRolled && !player.HasRolled;
            playerHadRolled = player.HasRolled;
            return finished;
        }

        //Ends the Event Triggers section once the player lands on the Warp tile.
        private void OnEventTriggered(PlayerCharacter triggeringPlayer, MapEventType type)
        {
            if (currentSection == TutorialSection.EventTriggers && type == MapEventType.Warp && triggeringPlayer == player)
            {
                CompleteCurrentSection();
            }
        }
    }
}
