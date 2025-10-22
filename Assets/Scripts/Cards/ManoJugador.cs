using System.Collections.Generic;
using UnityEngine;

public class ManoJugador : MonoBehaviour
{
    [Header("Configuraci贸n de Mano")]
    public int playerID;
    public bool esIA = false;
    public Transform centroMano;
    public float separacionCartas = 0.5f;
    public float escalaCartasIA = 0.8f;
    
    [Header("Escalas de Cartas")]
    public Vector3 escalaMazoYJugador = new Vector3(1.2f, 1.6f, 2f);
    public Vector3 escalaIA = new Vector3(0.84f, 1.12f, 1.4f);
	
	[Header("Selecci贸n para Dinamita")]
	public HashSet<GameObject> CartasSeleccionadasParaDinamita { get; private set; } = new HashSet<GameObject>();
	private int maxCartasParaSeleccionar = 0;
	public bool seleccionDinamitaHabilitada = false;

    private List<GameObject> cartasEnMano = new List<GameObject>();
    private MazoFisico mazo;
    
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
        
        // Obtener la escala objetivo seg煤n si es IA o humano
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
		
		Debug.Log($"馃幆 Habilitada selecci贸n de hasta {maxSeleccion} cartas para Dinamita");
		
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

	// NUEVO: M茅todo para manejar clic en carta durante selecci贸n de Dinamita
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
			Debug.Log($"馃敶 Carta deseleccionada para Dinamita - Total: {CartasSeleccionadasParaDinamita.Count}/{maxCartasParaSeleccionar}");
			
			// Efecto visual de deselecci贸n
			LeanTween.moveLocal(carta, Vector3.zero, 0.2f)
				.setEase(LeanTweenType.easeOutBack);
		}
		else if (CartasSeleccionadasParaDinamita.Count < maxCartasParaSeleccionar)
		{
			// Seleccionar carta
			CartasSeleccionadasParaDinamita.Add(carta);
			cartaScript.EstaSeleccionada = true;
			Debug.Log($"馃煝 Carta seleccionada para Dinamita - Total: {CartasSeleccionadasParaDinamita.Count}/{maxCartasParaSeleccionar}");
			
			// Efecto visual de selecci贸n
			LeanTween.moveLocal(carta, carta.transform.localPosition + Vector3.up * 0.5f, 0.2f)
				.setEase(LeanTweenType.easeOutBack);
		}
		else
		{
			Debug.Log($"鈿狅笍 Ya has seleccionado el m谩ximo de {maxCartasParaSeleccionar} cartas");
		}
	}

	// NUEVO: M茅todo para limpiar selecci贸n de Dinamita
	public void LimpiarSeleccionDinamita()
	{
		foreach (GameObject carta in CartasSeleccionadasParaDinamita)
		{
			Carta3D cartaScript = carta.GetComponent<Carta3D>();
			if (cartaScript != null)
			{
				cartaScript.EstaSeleccionada = false;
			}
			
			// Restaurar posici贸n original
			LeanTween.moveLocal(carta, Vector3.zero, 0.3f)
				.setEase(LeanTweenType.easeOutBack);
		}
		
		CartasSeleccionadasParaDinamita.Clear();
		seleccionDinamitaHabilitada = false;
		maxCartasParaSeleccionar = 0;
	}

	// NUEVO: Modificar el m茅todo OnMouseDown en Carta3D para manejar selecci贸n de Dinamita
	// (Esto se agregar铆a en Carta3D.cs, pero lo menciono aqu铆 para contexto)

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
    
    // NUEVO M脡TODO: Seleccionar una carta (selecci贸n 煤nica)
    public void SeleccionarCarta(GameObject carta)
	{
		// ? VERIFICAR: Si ya se tiró el dado en este turno, no permitir seleccionar cartas de acción
		if (GameManager.Instance != null && GameManager.Instance.dadoTiradoEnEsteTurno)
		{
			// Usar un nombre diferente para evitar conflicto
			Carta3D cartaVerificacion = carta?.GetComponent<Carta3D>();
			if (cartaVerificacion != null && cartaVerificacion.EsCartaDeAccion())
			{
				Debug.Log("?? No se puede seleccionar carta de acción - Ya se tiró el dado en este turno");
				return;
			}
		}
		
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
			Debug.Log($"?? Carta deseleccionada - Jugador {playerID}");
		}
		else
		{
			// Es una carta diferente - seleccionar
			cartaScript.EstaSeleccionada = true;
			cartaSeleccionadaActual = carta;
			Debug.Log($"?? Carta seleccionada - Jugador {playerID}");
		}
		
		// Notificar al sistema de botones que la selección cambió
		if (GestionBotonesCartas.Instance != null)
		{
			GestionBotonesCartas.Instance.ForzarActualizacionBoton();
		}
	}
    
    // NUEVO M脡TODO: Deseleccionar la carta actual
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
    
    // NUEVO M脡TODO: Obtener la carta actualmente seleccionada
    public GameObject GetCartaSeleccionada()
    {
        return cartaSeleccionadaActual;
    }
    
    // NUEVO M脡TODO: Deseleccionar todas las cartas
    public void DeseleccionarTodasLasCartas()
	{
		// Deseleccionar la carta actual primero
		if (cartaSeleccionadaActual != null)
		{
			Carta3D cartaScript = cartaSeleccionadaActual.GetComponent<Carta3D>();
			if (cartaScript != null)
			{
				cartaScript.Deseleccionar();
				Debug.Log($"馃敶 Carta {cartaSeleccionadaActual.name} deseleccionada forzadamente");
			}
			cartaSeleccionadaActual = null;
		}
		
		// Asegurarse de que todas las cartas est茅n deseleccionadas
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
		
		Debug.Log($"鉁?Todas las cartas deseleccionadas - Jugador {playerID}");
	}

    public void HabilitarCartasParaRobo()
    {
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
                    
                    // Feedback visual m谩s pronunciado
                    LeanTween.moveLocal(carta, carta.transform.localPosition + Vector3.up * 0.2f, 0.3f)
                        .setEase(LeanTweenType.easeOutBack);
                    
                    cartasHabilitadas++;
                }
            }
        }
        
        Debug.Log($"馃儚 Habilitadas {cartasHabilitadas} cartas del jugador {playerID} para robo");
    }

    public void DeshabilitarCartasParaRobo()
    {
        
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

    // M茅todo para remover carta de la mano
    public void RemoverCarta(GameObject carta)
	{
		if (cartasEnMano.Contains(carta))
		{
			cartasEnMano.Remove(carta);
			
			// 鉁?Resetear el estado de selecci贸n de la carta antes de removerla
			Carta3D cartaScript = carta.GetComponent<Carta3D>();
			if (cartaScript != null)
			{
				cartaScript.Deseleccionar();
				cartaScript.SetEnManoIA(false); // Resetear a escala est谩ndar
			}
			
			// Si la carta removida era la seleccionada, limpiar la referencia
			if (cartaSeleccionadaActual == carta)
			{
				cartaSeleccionadaActual = null;
			}
			
			// 鉁?Remover de la selecci贸n de Dinamita si est谩 ah铆
			if (CartasSeleccionadasParaDinamita.Contains(carta))
			{
				CartasSeleccionadasParaDinamita.Remove(carta);
			}
			
			// 鉁?Asegurar que se reorganiza la mano despu茅s de remover
			ReorganizarMano();
			
			Debug.Log($"馃棏锔?Carta removida de jugador {playerID}. Cartas restantes: {cartasEnMano.Count}");
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
    
    // NUEVO: M茅todo para debug
    [ContextMenu("Debug Estado Selecci贸n")]
    public void DebugEstadoSeleccion()
    {
        Debug.Log($"馃幆 Estado selecci贸n - Jugador {playerID}:");
        Debug.Log($"- Cartas en mano: {cartasEnMano.Count}");
        Debug.Log($"- Carta seleccionada: {(cartaSeleccionadaActual != null ? cartaSeleccionadaActual.name : "Ninguna")}");
    }
	
	
}