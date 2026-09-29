using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Escape4Now.TurnSystem;

public class DiceRollController : MonoBehaviour
{
    
    [SerializeField] private TMP_Text diceResultText;
    [SerializeField] private TurnSystemController turnSystem;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    
    {
      
      diceResultText.text = "Press Space to Roll";
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RollDice();
        }
    }

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
