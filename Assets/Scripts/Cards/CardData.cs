using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/CardData")]
public class CardData : ScriptableObject
{
    public string cardName;
    public Sprite cardSprite;
    [TextArea] public string description;
    public int pointValue;
    public UnityEvent onPlay;
    
    // Nuevo: Tipo de carta para identificación más fácil
    public CardType cardType;
}

// Nuevo enum para tipos de carta
public enum CardType
{
    Oro,
    Piedra,
	Dinamita,
	Diamante
}