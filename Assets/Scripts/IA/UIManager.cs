using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public Button diceButton; // Asigna el botón "Tirar Dado" en el Inspector
	public TextMeshProUGUI phase1Text; // Asigna el texto temporal de la Fase 1
	public TextMeshProUGUI phase2Text; // Asigna el texto temporal de la Fase 2
    //public float phase1TextDuration = 2f; // Duración en segundos
	public GameObject phase1UI; // Asigna el panel de UI para la fase 1 (si existe)
    public GameObject phase2UI; // Asigna el panel de UI para la fase 2.
	public GameObject phase2Panel; // Panel padre de los botones
	public GameObject dado;

    void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
	
	public void ShowTemporaryMessage(TextMeshProUGUI textElement, float duration = 2f)
	{
		if (textElement != null)
		{
			textElement.gameObject.SetActive(true);
			StartCoroutine(HideTextAfterDelay(textElement, duration));
		}
	}

    private IEnumerator HideTextAfterDelay(TextMeshProUGUI textElement, float duration)
	{
		yield return new WaitForSeconds(duration);
		textElement.gameObject.SetActive(false);
	}

    public void SetDiceButtonVisibility(bool isVisible)
    {
        if (diceButton != null)
        {
            diceButton.interactable = isVisible;
        }
    }
	
	public void SetPhaseUI(GamePhase phase)
	{
		switch (phase)
		{
			case GamePhase.BoardConstruction:
				// Ocultar Panel de Fase 2 (movimiento) si existe
				if (phase2Panel != null) 
					phase2Panel.SetActive(false);
					dado.SetActive(false);
				break;

			case GamePhase.TotemMovement:
				
				
				if (phase2UI != null) phase2UI.SetActive(true);
				// Mostrar Panel de Fase 2 (con botones)
				if (phase2Panel != null) 
					phase2Panel.SetActive(true);
					dado.SetActive(true);
				
				// Asegurar que el texto de Fase 1 está oculto
				if (phase1Text != null) 
					phase1Text.gameObject.SetActive(false);
				break;
		}
	}
	
	
}
