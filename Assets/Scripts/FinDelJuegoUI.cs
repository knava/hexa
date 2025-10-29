using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class FinDelJuegoUI : MonoBehaviour
{
    [Header("Paneles UI")]
    public GameObject panelRanking;
    public Transform contenedorRanking;
    public GameObject prefabEntradaJugador;
    public TextMeshProUGUI textoTituloRanking;
    
    [Header("Botón Salir")]
    public Button botonSalir;
    
    [Header("Configuración")]
    public string textoRanking = "RANKING FINAL";
    
    [Header("Tiempos")]
    public float tiempoMostrarBoton = 5f;
    
    private List<JugadorPuntuacion> rankingJugadores = new List<JugadorPuntuacion>();

    void Awake()
    {
        Debug.Log("🔧 FinDelJuegoUI - Awake()");
        
        if (panelRanking != null)
            panelRanking.SetActive(false);
            
        if (botonSalir != null)
            botonSalir.gameObject.SetActive(false);
            
        gameObject.SetActive(true);
    }

    public void MostrarRanking(List<PlayerTotem> jugadores)
    {
        Debug.Log("🏆 MostrarRanking llamado - Activando panelRanking");
        
        if (jugadores == null || jugadores.Count == 0)
        {
            Debug.LogError("❌ Lista de jugadores está vacía o es null");
            return;
        }

        if (panelRanking == null)
        {
            Debug.LogError("❌ panelRanking no está asignado!");
            return;
        }

        // ACTIVAR el panel de ranking
        panelRanking.SetActive(true);
        gameObject.SetActive(true);

        // Calcular puntuaciones
        CalcularPuntuaciones(jugadores);
        
        // Ordenar por puntuación (mayor a menor)
        rankingJugadores = rankingJugadores.OrderByDescending(j => j.puntuacion).ToList();
        
        // Configurar título
        if (textoTituloRanking != null)
        {
            textoTituloRanking.text = textoRanking;
        }
        
        // Limpiar contenedor anterior
        foreach (Transform child in contenedorRanking)
        {
            Destroy(child.gameObject);
        }
        
        // Crear entradas de ranking
        for (int i = 0; i < rankingJugadores.Count; i++)
        {
            CrearEntradaRanking(rankingJugadores[i], i + 1);
        }
        
        Debug.Log($"✅ Ranking mostrado con {rankingJugadores.Count} jugadores");
        
        // Programar mostrar el botón de salir después de un tiempo
        Invoke("MostrarBotonSalir", tiempoMostrarBoton);
    }

    private void MostrarBotonSalir()
    {
        Debug.Log("🔄 Mostrando botón de salir");
        
        if (botonSalir != null)
        {
            botonSalir.gameObject.SetActive(true);
            
            CanvasGroup botonCanvasGroup = botonSalir.GetComponent<CanvasGroup>();
            if (botonCanvasGroup == null)
                botonCanvasGroup = botonSalir.gameObject.AddComponent<CanvasGroup>();
                
            botonCanvasGroup.alpha = 0f;
            LeanTween.alphaCanvas(botonCanvasGroup, 1f, 0.5f)
                .setEase(LeanTweenType.easeInOutQuad);
        }
        else
        {
            Debug.LogError("❌ botonSalir no está asignado!");
        }
    }

    private void CalcularPuntuaciones(List<PlayerTotem> jugadores)
    {
        rankingJugadores.Clear();
        
        foreach (PlayerTotem jugador in jugadores)
        {
            int puntuacion = CalcularPuntuacionJugador(jugador.playerID);
            rankingJugadores.Add(new JugadorPuntuacion(jugador.playerID, puntuacion, jugador.playerColor));
            Debug.Log($"🎯 Jugador {jugador.playerID} - {puntuacion} puntos");
        }
    }

    private int CalcularPuntuacionJugador(int playerID)
	{
		int puntuacion = 0;
		
		// Puntos por cartas de oro
		if (MazoFisico.Instance != null && 
			MazoFisico.Instance.manosJugadores.TryGetValue(playerID, out ManoJugador mano))
		{
			foreach (GameObject cartaObj in mano.GetCartas())
			{
				Carta3D carta3D = cartaObj.GetComponent<Carta3D>();
				if (carta3D != null)
				{
					Material frenteMaterial = carta3D.GetFrenteMaterial();
					if (frenteMaterial == MazoFisico.Instance.frenteOro)
					{
						puntuacion += 1;
					}
				}
			}
		}

		return puntuacion;
	}

    private void CrearEntradaRanking(JugadorPuntuacion jugador, int posicion)
    {
        GameObject entrada = Instantiate(prefabEntradaJugador, contenedorRanking);
        EntradaRankingUI entradaUI = entrada.GetComponent<EntradaRankingUI>();
        
        if (entradaUI != null)
        {
            entradaUI.Configurar(posicion, jugador);
        }
    }

    public void BotonSalir()
    {
        Debug.Log("🚪 Botón salir presionado");
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SalirAlMenu();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MenuPrincipal");
        }
    }
}

// CLASE JUGADORPUNTUACION - AÑADIDA AL FINAL DEL ARCHIVO
[System.Serializable]
public class JugadorPuntuacion
{
    public int playerID;
    public int puntuacion;
    public Color colorJugador;
    
    public JugadorPuntuacion(int id, int pts, Color color)
    {
        playerID = id;
        puntuacion = pts;
        colorJugador = color;
    }
}