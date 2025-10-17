using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GestionBotonesCartas : MonoBehaviour
{
    public static GestionBotonesCartas Instance;
    
    [Header("Referencias UI")]
    public Button botonUtilizar;
    public TextMeshProUGUI textoBotonUtilizar;
    public TextMeshProUGUI textoMensaje; // SOLO el TextMeshPro, no el panel completo
    
    [Header("Configuración")]
    public Color colorNormal = Color.white;
    public Color colorDeshabilitado = Color.gray;
    
    private ManoJugador manoJugadorActual;
    private GameObject cartaSeleccionada;
	private bool cartaEnUso = false;
    
    // Estados para la selección de objetivo
    private bool esperandoSeleccionObjetivo = false;
    private int jugadorObjetivoID = -1;
    private CardType tipoCartaEnUso;

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

    void Start()
    {
        // Configurar botón inicialmente deshabilitado
        if (botonUtilizar != null)
        {
            botonUtilizar.gameObject.SetActive(false);
            botonUtilizar.onClick.AddListener(UtilizarCartaSeleccionada);
        }
        
        // NUEVO: Ocultar el texto al inicio (pero el panel permanece activo)
        if (textoMensaje != null)
        {
            textoMensaje.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Actualizar estado del botón cada frame
        ActualizarEstadoBoton();
        
        // Manejar clics en avatares durante selección de objetivo
        if (esperandoSeleccionObjetivo && Input.GetMouseButtonDown(0))
        {
            ManejarClicEnAvatar();
        }
    }

    public void ActualizarEstadoBoton()
    {
		// Si es turno de IA, ocultar botón
		if (GameManager.Instance != null && GameManager.Instance.IsCurrentPlayerAI())
		{
			OcultarBotonUtilizar();
			return;
		}
        // Si estamos en medio de una selección, no actualizar el botón normal
        if (esperandoSeleccionObjetivo || cartaEnUso) 
		{
			OcultarBotonUtilizar();
			return;
		}

        // Verificar si es el turno del jugador humano
        bool esTurnoJugador = GameManager.Instance != null && 
                              GameManager.Instance.currentPlayerIndex == 0 &&
                              GameManager.Instance.currentPhase == GamePhase.TotemMovement;

        if (!esTurnoJugador)
        {
            OcultarBotonUtilizar();
            return;
        }

        // Buscar la mano del jugador actual si no la tenemos
        if (manoJugadorActual == null)
        {
            manoJugadorActual = BuscarManoJugadorActual();
        }

        // Verificar si hay una carta de acción seleccionada
        bool puedeMostrarBoton = esTurnoJugador && 
                                manoJugadorActual != null && 
                                manoJugadorActual.GetCartaSeleccionada() != null;

        if (puedeMostrarBoton)
        {
            GameObject carta = manoJugadorActual.GetCartaSeleccionada();
            Carta3D cartaScript = carta?.GetComponent<Carta3D>();
            
            if (cartaScript != null && cartaScript.EsCartaDeAccion())
            {
                MostrarBotonUtilizar();
                cartaSeleccionada = carta;
                tipoCartaEnUso = cartaScript.GetTipoCarta();
            }
            else
            {
                OcultarBotonUtilizar();
            }
        }
        else
        {
            OcultarBotonUtilizar();
        }
    }

    private ManoJugador BuscarManoJugadorActual()
    {
        ManoJugador[] todasLasManos = FindObjectsOfType<ManoJugador>();
        
        foreach (ManoJugador mano in todasLasManos)
        {
            if (mano.playerID == 1 && !mano.esIA)
            {
                return mano;
            }
        }
        
        return null;
    }

    private void MostrarBotonUtilizar()
    {
        if (botonUtilizar != null && !botonUtilizar.gameObject.activeInHierarchy)
        {
            botonUtilizar.gameObject.SetActive(true);
            botonUtilizar.interactable = true;
            
            if (textoBotonUtilizar != null)
            {
                textoBotonUtilizar.color = colorNormal;
            }
            
            Debug.Log("✅ Botón UTILIZAR activado");
        }
    }

    private void OcultarBotonUtilizar()
    {
        if (botonUtilizar != null && botonUtilizar.gameObject.activeInHierarchy)
        {
            botonUtilizar.gameObject.SetActive(false);
            //cartaSeleccionada = null;
            Debug.Log("❌ Botón UTILIZAR ocultado");
        }
    }

    // NUEVO: Mostrar solo el texto (no el panel completo)
    public void MostrarMensaje(string mensaje)
    {
        if (textoMensaje != null)
        {
            textoMensaje.text = mensaje;
            textoMensaje.gameObject.SetActive(true);
            Debug.Log($"📢 Mensaje mostrado: {mensaje}");
        }
        else
        {
            Debug.LogWarning("⚠️ Texto de mensaje no asignado");
        }
    }

    // NUEVO: Ocultar solo el texto (no el panel completo)
    public void OcultarMensaje()
    {
        if (textoMensaje != null)
        {
            textoMensaje.gameObject.SetActive(false);
            Debug.Log("📢 Mensaje ocultado");
        }
    }

    public void UtilizarCartaSeleccionada()
    {
        if (cartaSeleccionada == null || cartaEnUso)
        {
            Debug.LogWarning("⚠️ No hay carta seleccionada para utilizar");
            return;
        }

        Carta3D cartaScript = cartaSeleccionada.GetComponent<Carta3D>();
        if (cartaScript == null)
        {
            Debug.LogError("❌ La carta seleccionada no tiene script Carta3D");
            return;
        }

        Debug.Log($"🎯 Utilizando carta: {cartaScript.GetTipoCarta()}");
        tipoCartaEnUso = cartaScript.GetTipoCarta();
		
		cartaEnUso = true;

        // Ejecutar la acción según el tipo de carta
        switch (tipoCartaEnUso)
        {
            case CardType.Dinamita:
                IniciarSeleccionObjetivoDinamita();
                break;
            default:
                Debug.LogWarning($"⚠️ Acción no implementada para: {cartaScript.GetTipoCarta()}");
				cartaEnUso = false;
                break;
        }
    }

    // Método: Iniciar selección de objetivo para Dinamita
    private void IniciarSeleccionObjetivoDinamita()
    {
        Debug.Log("💥 Iniciando selección de objetivo para DINAMITA");
        
        // Ocultar botón utilizar temporalmente
        OcultarBotonUtilizar();
        
        // Activar modo selección de objetivo
        esperandoSeleccionObjetivo = true;
        
        // Mostrar mensaje (solo el texto)
        MostrarMensaje("Selecciona un jugador objetivo para la DINAMITA");
        
        // Resaltar avatares de jugadores disponibles (excluyendo al jugador actual)
        List<int> jugadoresDisponibles = ObtenerJugadoresObjetivo();
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarJugadoresDisponibles(jugadoresDisponibles);
        }
        
        Debug.Log("🎯 Modo selección activado - Haz clic en un avatar objetivo");
    }

    // Método: Obtener jugadores que pueden ser objetivo
    private List<int> ObtenerJugadoresObjetivo()
    {
        List<int> jugadoresDisponibles = new List<int>();
        
        if (SistemaAvataresJugadores.Instance != null)
        {
            var todosJugadores = SistemaAvataresJugadores.Instance.GetJugadoresDisponibles();
            foreach (int playerID in todosJugadores)
            {
                // Excluir al jugador actual (Jugador 1)
                if (playerID != 1)
                {
                    // Verificar que el jugador objetivo tiene cartas
                    if (MazoFisico.Instance != null && 
                        MazoFisico.Instance.manosJugadores.TryGetValue(playerID, out ManoJugador manoObjetivo))
                    {
                        if (manoObjetivo.CantidadCartas > 0)
                        {
                            jugadoresDisponibles.Add(playerID);
                            Debug.Log($"✅ Jugador {playerID} disponible como objetivo - Cartas: {manoObjetivo.CantidadCartas}");
                        }
                        else
                        {
                            Debug.Log($"❌ Jugador {playerID} no tiene cartas - No puede ser objetivo");
                        }
                    }
                }
            }
        }
        
        if (jugadoresDisponibles.Count == 0)
        {
            Debug.LogWarning("⚠️ No hay jugadores disponibles como objetivo");
            CancelarSeleccionObjetivo();
        }
        
        return jugadoresDisponibles;
    }

    // Método: Manejar clic en avatar durante selección
    private void ManejarClicEnAvatar()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            AvatarJugador avatar = hit.collider.GetComponent<AvatarJugador>();
            if (avatar != null)
            {
                int playerIDObjetivo = avatar.GetPlayerID();
                
                // Verificar que el jugador objetivo es válido
                List<int> jugadoresDisponibles = ObtenerJugadoresObjetivo();
                if (jugadoresDisponibles.Contains(playerIDObjetivo))
                {
                    Debug.Log($"🎯 Jugador {playerIDObjetivo} seleccionado como objetivo");
                    jugadorObjetivoID = playerIDObjetivo;
                    ConfirmarSeleccionObjetivo();
                }
                else
                {
                    Debug.LogWarning($"⚠️ Jugador {playerIDObjetivo} no puede ser objetivo");
                }
            }
        }
    }

    // Método: Confirmar selección y ejecutar la acción
    private void ConfirmarSeleccionObjetivo()
    {
        Debug.Log($"💥 Confirmando Dinamita contra Jugador {jugadorObjetivoID}");
        
        // Ocultar solo el texto (no el panel completo)
        OcultarMensaje();
        
        // Quitar resaltado de avatares
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarTodosLosAvatares(false);
        }
        
        // Ejecutar la acción de la Dinamita
        EjecutarDinamita(jugadorObjetivoID);
        
        // Resetear estado
        esperandoSeleccionObjetivo = false;
        jugadorObjetivoID = -1;
    }

    // Método: Cancelar selección
    private void CancelarSeleccionObjetivo()
    {
        Debug.Log("❌ Selección de objetivo cancelada");
        
        // Ocultar solo el texto (no el panel completo)
        OcultarMensaje();
        
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarTodosLosAvatares(false);
        }
        
        esperandoSeleccionObjetivo = false;
        jugadorObjetivoID = -1;
		cartaEnUso = false;
        
        // Volver a mostrar botón utilizar
        ActualizarEstadoBoton();
    }

    // ... (los métodos EjecutarDinamita, ProcesoDescarteDinamita, TerminarUsoDinamita 
    // y TerminarTurnoDespuesDeUsarCarta se mantienen IGUAL)

    // Método: Ejecutar la lógica de la Dinamita
    private void EjecutarDinamita(int jugadorObjetivoID)
	{
		Debug.Log($"💥 EJECUTANDO DINAMITA contra Jugador {jugadorObjetivoID}");
		
		// Obtener la mano del jugador objetivo
		if (MazoFisico.Instance != null && 
			MazoFisico.Instance.manosJugadores.TryGetValue(jugadorObjetivoID, out ManoJugador manoObjetivo))
		{
			int cartasTotales = manoObjetivo.CantidadCartas;
			
			// Calcular cuántas cartas descartar (mitad, redondeando hacia arriba)
			int cartasADescartar = Mathf.CeilToInt(cartasTotales / 2f);
			
			Debug.Log($"📊 Jugador {jugadorObjetivoID} tiene {cartasTotales} cartas - A descartar: {cartasADescartar}");
			
			if (cartasADescartar > 0)
			{
				// NUEVO: Iniciar proceso de selección de cartas para descartar
				IniciarSeleccionCartasParaDescarte(manoObjetivo, cartasADescartar);
			}
			else
			{
				Debug.Log("⚠️ No hay cartas para descartar");
				TerminarUsoDinamita();
			}
		}
		else
		{
			Debug.LogError($"❌ No se encontró mano del jugador objetivo {jugadorObjetivoID}");
			TerminarUsoDinamita();
		}
	}
	
	private void IniciarSeleccionCartasParaDescarte(ManoJugador manoObjetivo, int cartasADescartar)
	{
		Debug.Log($"🎯 Iniciando selección de {cartasADescartar} cartas para descartar");
		
		// Mostrar mensaje al jugador
		MostrarMensaje($"Selecciona {cartasADescartar} cartas del Jugador {manoObjetivo.playerID} para descartar");
		
		// Habilitar las cartas del jugador objetivo para selección
		manoObjetivo.HabilitarCartasParaSeleccionDinamita(cartasADescartar);
		
		// Iniciar corrutina que espera la selección
		StartCoroutine(EsperarSeleccionCartas(manoObjetivo, cartasADescartar));
	}
	
	private IEnumerator EsperarSeleccionCartas(ManoJugador manoObjetivo, int cartasADescartar)
	{
		// Esperar a que se seleccionen las cartas requeridas
		while (manoObjetivo.CartasSeleccionadasParaDinamita.Count < cartasADescartar)
		{
			yield return null;
		}
		
		Debug.Log($"✅ Selección completada - {cartasADescartar} cartas seleccionadas");
		
		// Proceder con el descarte de las cartas seleccionadas
		ProcesarDescarteDinamita(manoObjetivo);
	}
	
	private void ProcesarDescarteDinamita(ManoJugador manoObjetivo)
	{
		Debug.Log($"🗑️ Procesando descarte de cartas seleccionadas...");
		
		// Obtener las cartas seleccionadas
		List<GameObject> cartasSeleccionadas = new List<GameObject>(manoObjetivo.CartasSeleccionadasParaDinamita);
		
		// Mover cada carta seleccionada al descarte
		foreach (GameObject carta in cartasSeleccionadas)
		{
			if (MazoDescarte.Instance != null)
			{
				// ✅ Asegurar que la carta tenga la escala correcta antes de mover al descarte
				Carta3D cartaScript = carta.GetComponent<Carta3D>();
				if (cartaScript != null)
				{
					cartaScript.SetEnManoIA(false); // Forzar escala estándar
				}
				
				MazoDescarte.Instance.AgregarCartaDescarte(carta);
				manoObjetivo.RemoverCarta(carta);
				Debug.Log($"🗑️ Carta descartada: {carta.name}");
			}
		}
		
		// Limpiar la selección de dinamita
		manoObjetivo.LimpiarSeleccionDinamita();
		
		// Ocultar mensaje
		OcultarMensaje();
		
		// Terminar el uso de la Dinamita
		TerminarUsoDinamita();
	}
	
	
	
	public void CancelarSeleccionCartas()
	{
		if (jugadorObjetivoID != -1)
		{
			// Obtener la mano del jugador objetivo
			if (MazoFisico.Instance != null && 
				MazoFisico.Instance.manosJugadores.TryGetValue(jugadorObjetivoID, out ManoJugador manoObjetivo))
			{
				manoObjetivo.LimpiarSeleccionDinamita();
			}
		}
		
		OcultarMensaje();
		Debug.Log("❌ Selección de cartas cancelada");
	}

    // Método: Proceso de descarte con la Dinamita
    private IEnumerator ProcesoDescarteDinamita(ManoJugador manoObjetivo, int cartasADescartar)
    {
        Debug.Log($"🃏 Iniciando descarte de {cartasADescartar} cartas...");
        
        // Obtener todas las cartas de la mano objetivo
        List<GameObject> cartasEnMano = manoObjetivo.GetCartas();
        
        // Para el Jugador 1 (humano), permitir seleccionar qué cartas descartar
        // Por ahora, descartamos aleatoriamente (luego implementaremos selección)
        for (int i = 0; i < cartasADescartar && cartasEnMano.Count > 0; i++)
        {
            // Seleccionar carta aleatoria para descartar
            int indiceAleatorio = Random.Range(0, cartasEnMano.Count);
            GameObject cartaADescartar = cartasEnMano[indiceAleatorio];
            
            // Mover al descarte
            if (MazoDescarte.Instance != null)
            {
                MazoDescarte.Instance.AgregarCartaDescarte(cartaADescartar);
                manoObjetivo.RemoverCarta(cartaADescartar);
                Debug.Log($"🗑️ Carta {i+1}/{cartasADescartar} descartada");
            }
            
            // Pequeño delay entre descartes
            yield return new WaitForSeconds(0.3f);
            
            // Actualizar lista
            cartasEnMano = manoObjetivo.GetCartas();
        }
        
        Debug.Log($"✅ Descarte completado - {cartasADescartar} cartas descartadas");
        
        // Terminar el uso de la Dinamita
        TerminarUsoDinamita();
    }

    // Método: Terminar el uso de la Dinamita
    private void TerminarUsoDinamita()
    {
        Debug.Log("🔚 Terminando uso de Dinamita");
        
        // Mover la carta de dinamita al descarte
        if (cartaSeleccionada != null && manoJugadorActual != null)
        {
            if (MazoDescarte.Instance != null)
            {
                // Deseleccionar antes de mover
                Carta3D cartaScript = cartaSeleccionada.GetComponent<Carta3D>();
                if (cartaScript != null)
                {
                    cartaScript.Deseleccionar();
                }
                
                MazoDescarte.Instance.AgregarCartaDescarte(cartaSeleccionada);
                manoJugadorActual.RemoverCarta(cartaSeleccionada);
                Debug.Log("✅ Dinamita utilizada y movida al descarte");
            }
        }

        // Deseleccionar todas las cartas
        if (manoJugadorActual != null)
        {
            manoJugadorActual.DeseleccionarTodasLasCartas();
        }

        // Terminar el turno
		cartaEnUso = false;
        TerminarTurnoDespuesDeUsarCarta();
    }

    private void TerminarTurnoDespuesDeUsarCarta()
    {
        Debug.Log("🔄 Terminando turno después de usar carta de acción...");
        StartCoroutine(TerminarTurnoCoroutine());
    }

    private IEnumerator TerminarTurnoCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndTurn();
        }
    }

    public void ForzarActualizacionBoton()
    {
        ActualizarEstadoBoton();
    }

    [ContextMenu("Debug Estado Botón")]
    public void DebugEstadoBoton()
    {
        Debug.Log("=== DEBUG BOTÓN UTILIZAR ===");
        Debug.Log($"- Botón activo: {botonUtilizar?.gameObject.activeInHierarchy}");
        Debug.Log($"- Carta seleccionada: {cartaSeleccionada?.name}");
        Debug.Log($"- Esperando selección: {esperandoSeleccionObjetivo}");
        Debug.Log($"- Jugador objetivo: {jugadorObjetivoID}");
        Debug.Log($"- Texto mensaje activo: {textoMensaje?.gameObject.activeInHierarchy}");
    }
}