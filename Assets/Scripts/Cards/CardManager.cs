using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance;
    public List<CardData> deck = new List<CardData>();
    public List<CardData> discardPile = new List<CardData>();
    public GameObject cardPrefab; // Asigna el prefab de UI
    public Transform cardParent; // Canvas o panel donde aparecerán

    void Awake() => Instance = this;

    public void DrawCard(PlayerTotem player)
    {
        if (deck.Count == 0) ReshuffleDiscardPile();
        CardData card = deck[0];
        deck.RemoveAt(0);
        ShowCard(card, player);
    }

    private void ReshuffleDiscardPile()
    {
        deck = new List<CardData>(discardPile);
        discardPile.Clear();
        // Barajar el mazo (Fisher-Yates)
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardData temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    private void ShowCard(CardData card, PlayerTotem player)
    {
        GameObject cardObj = Instantiate(cardPrefab, cardParent);
        cardObj.GetComponent<Image>().sprite = card.cardSprite;
        // Configurar botón para aplicar efecto
        cardObj.GetComponent<Button>().onClick.AddListener(() => {
            card.onPlay.Invoke(); // Ejecutar efecto
            Destroy(cardObj);
        });
    }
}