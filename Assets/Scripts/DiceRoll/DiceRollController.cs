using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Escape4Now.TurnSystem;

public class DiceRollController : MonoBehaviour
{
    [SerializeField] private Button rollDiceButton;
    [SerializeField] private TMP_Text diceResultText;
    [SerializeField] private TurnSystemController turnSystem;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
      rollDiceButton.onClick.AddListener(RollDice);
      diceResultText.text = "Roll the dice!";
    }

    // Update is called once per frame
    private void RollDice()
    {
        int roll = Random.Range(1, 7);
        diceResultText.text = $"You rolled a {roll}!";

if (turnSystem != null)
        {
            turnSystem.AdvanceTurn();
        }
    }
}
