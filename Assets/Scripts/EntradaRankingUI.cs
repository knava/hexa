using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EntradaRankingUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public TextMeshProUGUI textoPosicion;
    public TextMeshProUGUI textoJugador;
    public TextMeshProUGUI textoPuntuacion;
    public Image iconoJugador;
    
    [Header("Colores")]
    public Color colorPrimerLugar = new Color(1f, 0.8f, 0f); // Dorado
    public Color colorSegundoLugar = new Color(0.7f, 0.7f, 0.7f); // Plata
    public Color colorTercerLugar = new Color(0.8f, 0.5f, 0.2f); // Bronce
    public Color colorResto = new Color(0.2f, 0.2f, 0.3f); // Azul oscuro
    
    private Image fondoPanel;

    void Awake()
    {
        fondoPanel = GetComponent<Image>();
    }
    
    public void Configurar(int posicion, JugadorPuntuacion jugador)
    {
        // Configurar textos
        if (textoPosicion != null)
            textoPosicion.text = GetTextoPosicion(posicion);
        
        if (textoJugador != null)
            textoJugador.text = $"Jugador {jugador.playerID}";
        
        if (textoPuntuacion != null)
            textoPuntuacion.text = $"{jugador.puntuacion} punto{(jugador.puntuacion != 1 ? "s" : "")}";
        
        // Configurar icono con color del jugador
        if (iconoJugador != null)
        {
            iconoJugador.color = jugador.colorJugador;
        }
        
        // Configurar color de fondo según posición
        if (fondoPanel != null)
        {
            fondoPanel.color = GetColorPorPosicion(posicion);
        }
    }
    
    private string GetTextoPosicion(int posicion)
    {
        switch (posicion)
        {
            case 1: return "1º";
            case 2: return "2º";
            case 3: return "3º";
            default: return $"{posicion}º";
        }
    }
    
    private Color GetColorPorPosicion(int posicion)
    {
        switch (posicion)
        {
            case 1: return colorPrimerLugar;
            case 2: return colorSegundoLugar;
            case 3: return colorTercerLugar;
            default: return colorResto;
        }
    }
}