using System.Collections.Generic;
using UnityEngine;

public class ManoJugador : MonoBehaviour
{
    [Header("Configuración de Mano")]
    public int playerID;
    public bool esIA = false;
    public Transform centroMano;
    public float separacionCartas = 0.5f;
    public float escalaCartasIA = 0.8f;
    
    [Header("Escalas de Cartas")]
    public Vector3 escalaMazoYJugador = new Vector3(1.2f, 1.6f, 2f);
    public Vector3 escalaIA = new Vector3(0.84f, 1.12f, 1.4f);
	
	[Header("Selección para Dinamita")]
	public HashSet<GameObject> CartasSeleccionadasParaDinamita { get; private set; } = new HashSet<GameObject>();
	private int maxCartasParaSeleccionar = 0;
	public bool seleccionDinamitaHabilitada = false;

    private List<GameObject> cartasEnMano = new List<GameObject>();
    private MazoFisico mazo;
    private bool cartasHabilitadasParaRobo = false;
    
    // NUEVO: Referencia a la carta actualmente seleccionada
    private GameObject cartaSeleccionadaActual = null;

    void Awake()
    {
        mazo = FindObjectOfType<MazoFisico>();
    }

    public void AgregarCarta(GameObject carta)
    {
        if (carta == null) return;

        cartasEnMano.Add(carta);
        carta.transform.SetParent(centroMano);
        Quaternion rotacionMano = Quaternion.Euler(90f, 0f, 0f);
        
        // Obtener la escala objetivo según si es IA o humano
        Vector3 escalaObjetivo = esIA ? escalaIA : escalaMazoYJugador;
        
        // Aplicar la escala correspondiente
        carta.transform.localScale = escalaObjetivo;
        carta.transform.localRotation = rotacionMano;
        
        Carta3D cartaScript = carta.GetComponent<Carta3D>();
        if (cartaScript != null)
        {
            // Actualizar el estado de la carta
            cartaScript.SetEnManoIA(esIA);
            
            if (esIA)
            {
                cartaScript.MostrarDorso();
                cartaScript.SetPuedeGirar(false);
            }
            else
            {
                cartaScript.MostrarFrente();
            }
        }

        ReorganizarMano();
    }
	
	public void HabilitarCartasParaSeleccionDinamita(int maxSeleccion)
	{
		maxCartasParaSeleccionar = maxSeleccion;
		seleccionDinamitaHabilitada = true;
		CartasSeleccionadasParaDinamita.Clear();
		
		Debug.Log($"🎯 Habilitada selección de hasta {maxSeleccion} cartas para Dinamita");
		
		// Aplicar efecto visual a las cartas
		foreach (GameObject carta in cartasEnMano)
		{
			if (carta != null)
			{
				// Feedback visual para indicar que son seleccionables
				LeanTween.moveLocal(carta, carta.transform.localPosition + Vector3.up * 0.3f, 0.3f)
					.setEase(LeanTweenType.easeOutBack);
			}
		}
	}

	// NUEVO: Método para manejar clic en carta durante selección de Dinamita
	public void ProcesarClicCartaDinamita(GameObject carta)
	{
		if (!seleccionDinamitaHabilitada) return;
		
		Carta3D cartaScript = carta.GetComponent<Carta3D>();
		if (cartaScript == null) return;
		
		if (CartasSeleccionadasParaDinamita.Contains(carta))
		{
			// Deseleccionar carta
			CartasSeleccionadasParaDinamita.Remove(carta);
			cartaScript.EstaSeleccionada = false;
			Debug.Log($"🔴 Carta deseleccionada para Dinamita - Total: {CartasSeleccionadasParaDinamita.Count}/{maxCartasParaSeleccionar}");
			
			// Efecto visual de deselección
			LeanTween.moveLocal(carta, Vector3.zero, 0.2f)
				.setEase(LeanTweenType.easeOutBack);
		}
		else if (CartasSeleccionadasParaDinamita.Count < maxCartasParaSeleccionar)
		{
			// Seleccionar carta
			CartasSeleccionadasParaDinamita.Add(carta);
			cartaScript.EstaSeleccionada = true;
			Debug.Log($"🟢 Carta seleccionada para Dinamita - Total: {CartasSeleccionadasParaDinamita.Count}/{maxCartasParaSeleccionar}");
			
			// Efecto visual de selección
			LeanTween.moveLocal(carta, carta.transform.localPosition + Vector3.up * 0.5f, 0.2f)
				.setEase(LeanTweenType.easeOutBack);
		}
		else
		{
			Debug.Log($"⚠️ Ya has seleccionado el máximo de {maxCartasParaSeleccionar} cartas");
		}
	}

	// NUEVO: Método para limpiar selección de Dinamita
	public void LimpiarSeleccionDinamita()
	{
		foreach (GameObject carta in CartasSeleccionadasParaDinamita)
		{
			Carta3D cartaScript = carta.GetComponent<Carta3D>();
			if (cartaScript != null)
			{
				cartaScript.EstaSeleccionada = false;
			}
			
			// Restaurar posición original
			LeanTween.moveLocal(carta, Vector3.zero, 0.3f)
				.setEase(LeanTweenType.easeOutBack);
		}
		
		CartasSeleccionadasParaDinamita.Clear();
		seleccionDinamitaHabilitada = false;
		maxCartasParaSeleccionar = 0;
	}

	// NUEVO: Modificar el método OnMouseDown en Carta3D para manejar selección de Dinamita
	// (Esto se agregaría en Carta3D.cs, pero lo menciono aquí para contexto)

    public void ReorganizarMano()
    {
        if (centroMano == null || cartasEnMano.Count == 0) return;

        float posicionInicial = -((cartasEnMano.Count - 1) * separacionCartas) / 2f;

        for (int i = 0; i < cartasEnMano.Count; i++)
        {
            GameObject carta = cartasEnMano[i];
            if (carta == null) continue;

            Vector3 nuevaPosicion = new Vector3(posicionInicial + (i * separacionCartas), 0, 0);
            Quaternion nuevaRotacion = Quaternion.Euler(90f, 0f, 0f);

            LeanTween.moveLocal(carta, nuevaPosicion, 0.5f)
            .setEase(LeanTweenType.easeOutBack);
            
            LeanTween.rotateLocal(carta, nuevaRotacion.eulerAngles, 0.5f)
            .setEase(LeanTweenType.easeOutBack);
        }
    }

    public int CantidadCartas
    {
        get { return cartasEnMano.Count; }
    }

    public void LimpiarMano()
    {
        foreach (GameObject carta in cartasEnMano)
        {
            if (carta != null)
            {
                Destroy(carta);
            }
        }
        cartasEnMano.Clear();
        cartaSeleccionadaActual = null;
    }
    
    // NUEVO MÉTODO: Seleccionar una carta (selección única)
    public void SeleccionarCarta(GameObject carta)
	{
		if (carta == null || !cartasEnMano.Contains(carta)) return;
		
		Carta3D cartaScript = carta.GetComponent<Carta3D>();
		if (cartaScript == null) return;
		
		// Si ya hay una carta seleccionada, deseleccionarla primero
		if (cartaSeleccionadaActual != null && cartaSeleccionadaActual != carta)
		{
			DeseleccionarCartaActual();
		}
		
		// Toggle: si es la misma carta, deseleccionar; si es diferente, seleccionar
		if (cartaSeleccionadaActual == carta)
		{
			// Es la misma carta - deseleccionar
			cartaScript.Deseleccionar();
			cartaSeleccionadaActual = null;
			Debug.Log($"🔴 Carta deseleccionada - Jugador {playerID}");
		}
		else
		{
			// Es una carta diferente - seleccionar
			cartaScript.EstaSeleccionada = true;
			cartaSeleccionadaActual = carta;
			Debug.Log($"🟢 Carta seleccionada - Jugador {playerID}");
		}
		
		// NUEVO: Notificar al sistema de botones que la selección cambió
		if (GestionBotonesCartas.Instance != null)
		{
			GestionBotonesCartas.Instance.ForzarActualizacionBoton();
		}
	}
    
    // NUEVO MÉTODO: Deseleccionar la carta actual
    private void DeseleccionarCartaActual()
    {
        if (cartaSeleccionadaActual != null)
        {
            Carta3D cartaScript = cartaSeleccionadaActual.GetComponent<Carta3D>();
            if (cartaScript != null)
            {
                cartaScript.Deseleccionar();
            }
            cartaSeleccionadaActual = null;
        }
    }
    
    // NUEVO MÉTODO: Obtener la carta actualmente seleccionada
    public GameObject GetCartaSeleccionada()
    {
        return cartaSeleccionadaActual;
    }
    
    // NUEVO MÉTODO: Deseleccionar todas las cartas
    public void DeseleccionarTodasLasCartas()
	{
		// Deseleccionar la carta actual primero
		if (cartaSeleccionadaActual != null)
		{
			Carta3D cartaScript = cartaSeleccionadaActual.GetComponent<Carta3D>();
			if (cartaScript != null)
			{
				cartaScript.Deseleccionar();
				Debug.Log($"🔴 Carta {cartaSeleccionadaActual.name} deseleccionada forzadamente");
			}
			cartaSeleccionadaActual = null;
		}
		
		// Asegurarse de que todas las cartas estén deseleccionadas
		foreach (GameObject carta in cartasEnMano)
		{
			if (carta != null)
			{
				Carta3D cartaScript = carta.GetComponent<Carta3D>();
				if (cartaScript != null && cartaScript.EstaSeleccionada)
				{
					cartaScript.Deseleccionar();
				}
			}
		}
		
		Debug.Log($"✅ Todas las cartas deseleccionadas - Jugador {playerID}");
	}

    public void HabilitarCartasParaRobo()
    {
        cartasHabilitadasParaRobo = true;
        int cartasHabilitadas = 0;
        
        foreach (GameObject carta in cartasEnMano)
        {
            if (carta != null)
            {
                Carta3D cartaScript = carta.GetComponent<Carta3D>();
                if (cartaScript != null)
                {
                    cartaScript.SetPuedeGirar(true);
                    cartaScript.SetEsRoboPorComer(true);
                    
                    // Feedback visual más pronunciado
                    LeanTween.moveLocal(carta, carta.transform.localPosition + Vector3.up * 0.2f, 0.3f)
                        .setEase(LeanTweenType.easeOutBack);
                    
                    cartasHabilitadas++;
                }
            }
        }
        
        Debug.Log($"🃏 Habilitadas {cartasHabilitadas} cartas del jugador {playerID} para robo");
    }

    public void DeshabilitarCartasParaRobo()
    {
        cartasHabilitadasParaRobo = false;
        
        foreach (GameObject carta in cartasEnMano)
        {
            if (carta != null)
            {
                Carta3D cartaScript = carta.GetComponent<Carta3D>();
                if (cartaScript != null)
                {
                    cartaScript.SetPuedeGirar(false);
                    cartaScript.SetEsRoboPorComer(false);
                    
                    // Quitar feedback visual
                    LeanTween.moveLocal(carta, Vector3.zero, 0.3f)
                        .setEase(LeanTweenType.easeOutBack);
                }
            }
        }
    }

    // Método para remover carta de la mano
    public void RemoverCarta(GameObject carta)
	{
		if (cartasEnMano.Contains(carta))
		{
			cartasEnMano.Remove(carta);
			
			// ✅ Resetear el estado de selección de la carta antes de removerla
			Carta3D cartaScript = carta.GetComponent<Carta3D>();
			if (cartaScript != null)
			{
				cartaScript.Deseleccionar();
				cartaScript.SetEnManoIA(false); // Resetear a escala estándar
			}
			
			// Si la carta removida era la seleccionada, limpiar la referencia
			if (cartaSeleccionadaActual == carta)
			{
				cartaSeleccionadaActual = null;
			}
			
			// ✅ Remover de la selección de Dinamita si está ahí
			if (CartasSeleccionadasParaDinamita.Contains(carta))
			{
				CartasSeleccionadasParaDinamita.Remove(carta);
			}
			
			// ✅ Asegurar que se reorganiza la mano después de remover
			ReorganizarMano();
			
			Debug.Log($"🗑️ Carta removida de jugador {playerID}. Cartas restantes: {cartasEnMano.Count}");
		}
	}
    
    public bool ContieneCarta(GameObject carta)
    {
        return cartasEnMano.Contains(carta);
    }
    
    public GameObject GetPrimeraCarta()
    {
        if (cartasEnMano.Count > 0)
        {
            return cartasEnMano[0]; // Devuelve la primera carta disponible
        }
        return null;
    }
    
    public List<GameObject> GetCartas()
    {
        return new List<GameObject>(cartasEnMano);
    }
    
    // NUEVO: Método para debug
    [ContextMenu("Debug Estado Selección")]
    public void DebugEstadoSeleccion()
    {
        Debug.Log($"🎯 Estado selección - Jugador {playerID}:");
        Debug.Log($"- Cartas en mano: {cartasEnMano.Count}");
        Debug.Log($"- Carta seleccionada: {(cartaSeleccionadaActual != null ? cartaSeleccionadaActual.name : "Ninguna")}");
    }
	
	
}