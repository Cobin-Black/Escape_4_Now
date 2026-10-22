using Escape4Now.TurnSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DiceRollController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private TMP_Text diceResultText;
    [SerializeField] private TurnSystemController turnSystem;
    [Header("Input")]
    [SerializeField] private InputActionReference rollAction;
    [Header("Copy")]
    [SerializeField] private string prompt = "Roll the dice";
    [SerializeField] private string resultFormat = "You rolled a {0}!";
    private bool ownsAction;

    private void OnEnable()
    {
        ownsAction = rollAction != null && !rollAction.action.enabled;
        if (ownsAction) rollAction.action.Enable();
    }

    private void OnDisable()
    {
        if (ownsAction && rollAction != null) rollAction.action.Disable();
        ownsAction = false;
    }

    private void Start()
    {
        if (diceResultText != null) diceResultText.text = prompt;
    }

    private void Update()
    {
        if (rollAction != null && rollAction.action.WasPressedThisFrame())
        {
            RollDice();
        }
    }

    private void RollDice()
    {
        int roll = Random.Range(1, 7);
        if (diceResultText != null) diceResultText.text = string.Format(resultFormat, roll);

        if (turnSystem != null)
        {
            turnSystem.AdvanceTurn();
        }
    }
}
