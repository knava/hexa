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
	private int maxCartasParaSeleccionar = 0;
	private int cartasDescartadasCount = 0; 
	private ManoJugador manoObjetivoActual;

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
		// ✅ VERIFICACIÓN CRÍTICA: Si ya se tiró el dado en este turno, NO mostrar botón
		if (GameManager.Instance != null && GameManager.Instance.dadoTiradoEnEsteTurno)
		{
			OcultarBotonUtilizar();
			//Debug.Log("⚠️ Botón Utilizar oculto - Ya se tiró el dado en este turno");
			return;
		}
		
		// Si estamos en medio de una acción de carta, no actualizar
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
		
		// ✅ DESACTIVAR BOTÓN DE TIRAR DADO INMEDIATAMENTE
		if (UIManager.Instance != null)
		{
			UIManager.Instance.SetDiceButtonVisibility(false);
		}
		
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
				// ✅ REACTIVAR BOTÓN DE DADO SI HAY ERROR
				if (UIManager.Instance != null)
				{
					UIManager.Instance.SetDiceButtonVisibility(true);
				}
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
		
		// Limpiar estado del descarte
		LimpiarEstadoDescarte();
		
		// Ocultar solo el texto (no el panel completo)
		OcultarMensaje();
		
		if (SistemaAvataresJugadores.Instance != null)
		{
			SistemaAvataresJugadores.Instance.ResaltarTodosLosAvatares(false);
		}
		
		esperandoSeleccionObjetivo = false;
		jugadorObjetivoID = -1;
		cartaEnUso = false;
		
		// ✅ REACTIVAR BOTÓN DE TIRAR DADO AL CANCELAR
		bool esTurnoJugador = GameManager.Instance != null && 
							  GameManager.Instance.currentPlayerIndex == 0 &&
							  GameManager.Instance.currentPhase == GamePhase.TotemMovement;
		
		if (esTurnoJugador && UIManager.Instance != null)
		{
			UIManager.Instance.SetDiceButtonVisibility(true);
		}
		
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
		Debug.Log($"🎯 Iniciando selección de {cartasADescartar} cartas para descarte INMEDIATO");
		
		// Guardar referencias y resetear contadores
		this.maxCartasParaSeleccionar = cartasADescartar;
		this.cartasDescartadasCount = 0;
		this.manoObjetivoActual = manoObjetivo;
		
		// Mostrar mensaje inicial
		MostrarMensaje($"Selecciona {cartasADescartar} carta(s) para descartar (0/{cartasADescartar})");
		
		// Habilitar las cartas del jugador objetivo para selección INMEDIATA
		manoObjetivo.HabilitarCartasParaSeleccionDinamita(cartasADescartar);
		
		Debug.Log("🖱️ Modo descarte inmediato activado - Cada clic descarta una carta");
	}
	
	private void ProcesarDescarteDinamita(ManoJugador manoObjetivo)
	{
		Debug.Log($"🎯 Iniciando proceso de descarte inmediato");
		// El descarte ahora se maneja carta por carta mediante clics inmediatos
	}

	// NUEVO: Corrutina para descarte inmediato carta por carta
	private IEnumerator ProcesarDescarteInmediatoCoroutine(List<GameObject> cartasSeleccionadas, ManoJugador manoObjetivo)
	{
		Debug.Log($"🎯 Iniciando descarte inmediato de {cartasSeleccionadas.Count} cartas...");
		
		// Mostrar mensaje inicial
		MostrarMensaje($"Descartando {cartasSeleccionadas.Count} cartas...");
		
		// Descarte carta por carta con animaciones (igual que la IA)
		for (int i = 0; i < cartasSeleccionadas.Count; i++)
		{
			GameObject carta = cartasSeleccionadas[i];
			if (carta != null && MazoDescarte.Instance != null)
			{
				// Mostrar progreso
				MostrarMensaje($"Descartando carta {i+1}/{cartasSeleccionadas.Count}");
				
				// Efecto visual de selección
				LeanTween.moveLocal(carta, carta.transform.localPosition + Vector3.up * 0.5f, 0.3f)
					.setEase(LeanTweenType.easeOutBack);
				
				yield return new WaitForSeconds(0.5f);
				
				// ✅ Asegurar que la carta tenga la escala correcta antes de mover al descarte
				Carta3D cartaScript = carta.GetComponent<Carta3D>();
				if (cartaScript != null)
				{
					cartaScript.SetEnManoIA(false); // Forzar escala estándar
					cartaScript.Deseleccionar(); // Quitar selección visual
				}
				
				// Animación al descarte
				Vector3 posicionDescarte = MazoDescarte.Instance.transform.position;
				LeanTween.move(carta, posicionDescarte, 0.8f)
					.setEase(LeanTweenType.easeInOutCubic);
				
				LeanTween.rotate(carta, new Vector3(90f, 0f, 0f), 0.5f);
				
				yield return new WaitForSeconds(0.8f);
				
				// Mover efectivamente al descarte
				MazoDescarte.Instance.AgregarCartaDescarte(carta);
				manoObjetivo.RemoverCarta(carta);
				
				yield return new WaitForSeconds(0.3f);
			}
		}
		
		// Mensaje final
		MostrarMensaje($"¡{cartasSeleccionadas.Count} cartas descartadas!");
		yield return new WaitForSeconds(1f);
		OcultarMensaje();
		
		// Terminar el uso de la Dinamita
		TerminarUsoDinamita();
	}
	
	
	
	public void CancelarSeleccionCartas()
	{
		CancelarSeleccionCartasSegura();
	}
	
	public void ProcesarDescarteInmediatoDeCarta(GameObject carta, ManoJugador manoObjetivo)
	{
		StartCoroutine(ProcesarDescarteIndividualCoroutine(carta, manoObjetivo));
	}

	// NUEVO: Corrutina para descarte individual
	private IEnumerator ProcesarDescarteIndividualCoroutine(GameObject carta, ManoJugador manoObjetivo)
	{
		Debug.Log($"🎯 Iniciando descarte inmediato de carta: {carta.name}");
		
		if (carta == null || MazoDescarte.Instance == null)
		{
			yield break;
		}
		
		// Incrementar contador de cartas descartadas
		cartasDescartadasCount++;
		
		Debug.Log($"📊 Progreso actual: {cartasDescartadasCount}/{maxCartasParaSeleccionar} cartas descartadas");
		
		// Mostrar mensaje de descarte
		MostrarMensaje($"Descartando carta {cartasDescartadasCount}/{maxCartasParaSeleccionar}");
		
		// Efecto visual antes del descarte
		LeanTween.moveLocal(carta, carta.transform.localPosition + Vector3.up * 1.0f, 0.3f)
			.setEase(LeanTweenType.easeOutBack);
		
		yield return new WaitForSeconds(0.3f);
		
		// ✅ Asegurar que la carta tenga la escala correcta
		Carta3D cartaScript = carta.GetComponent<Carta3D>();
		if (cartaScript != null)
		{
			cartaScript.SetEnManoIA(false);
			cartaScript.Deseleccionar();
		}
		
		// Animación al descarte
		Vector3 posicionDescarte = MazoDescarte.Instance.transform.position;
		LeanTween.move(carta, posicionDescarte, 0.8f)
			.setEase(LeanTweenType.easeInOutCubic);
		
		LeanTween.rotate(carta, new Vector3(90f, 0f, 0f), 0.5f);
		
		yield return new WaitForSeconds(0.8f);
		
		// Mover efectivamente al descarte
		MazoDescarte.Instance.AgregarCartaDescarte(carta);
		manoObjetivo.RemoverCarta(carta);
		
		// IMPORTANTE: Solo remover de la selección, no llamar a ActualizarMensajeProgreso
		if (manoObjetivo.CartasSeleccionadasParaDinamita.Contains(carta))
		{
			manoObjetivo.CartasSeleccionadasParaDinamita.Remove(carta);
		}
		
		yield return new WaitForSeconds(0.3f);
		
		// Verificar si ya se descartaron todas las cartas necesarias
		if (cartasDescartadasCount >= maxCartasParaSeleccionar)
		{
			Debug.Log($"✅ TODAS las cartas descartadas: {cartasDescartadasCount}/{maxCartasParaSeleccionar}");
			
			// Mensaje final
			MostrarMensaje($"¡Todas las cartas descartadas! ({maxCartasParaSeleccionar}/{maxCartasParaSeleccionar})");
			yield return new WaitForSeconds(1.5f);
			OcultarMensaje();
			
			// Limpiar selección
			if (manoObjetivoActual != null)
			{
				manoObjetivoActual.LimpiarSeleccionDinamita();
			}
			
			// Terminar el uso de la Dinamita
			TerminarUsoDinamita();
		}
		else
		{
			// Actualizar mensaje de progreso
			int cartasRestantes = maxCartasParaSeleccionar - cartasDescartadasCount;
			MostrarMensaje($"Selecciona {cartasRestantes} carta(s) más para descartar ({cartasDescartadasCount}/{maxCartasParaSeleccionar})");
		}
	}
	
	private void LimpiarEstadoDescarte()
	{
		cartasDescartadasCount = 0;
		maxCartasParaSeleccionar = 0;
		manoObjetivoActual = null;
	}

	public void DescarteCompletado()
	{
		Debug.Log("✅ Descarte completado - Terminando uso de Dinamita");
		
		StartCoroutine(TerminarDescarteCoroutine());
	}
	
	private IEnumerator TerminarDescarteCoroutine()
	{
		yield return new WaitForSeconds(1f);
		OcultarMensaje();
		TerminarUsoDinamita();
	}
	
    // Método: Terminar el uso de la Dinamita
    public void TerminarUsoDinamita()
	{
		Debug.Log("🔚 Terminando uso de Dinamita");
		
		LimpiarEstadoDescarte();
		
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

		// ✅ REACTIVAR BOTÓN DE TIRAR DADO (si todavía es nuestro turno)
		bool esTurnoJugador = GameManager.Instance != null && 
							  GameManager.Instance.currentPlayerIndex == 0 &&
							  GameManager.Instance.currentPhase == GamePhase.TotemMovement;
		
		if (esTurnoJugador && UIManager.Instance != null)
		{
			UIManager.Instance.SetDiceButtonVisibility(true);
		}

		// Terminar el turno
		cartaEnUso = false;
		TerminarTurnoDespuesDeUsarCarta();
	}
	
	public void CancelarSeleccionCartasSegura()
	{
		if (jugadorObjetivoID != -1)
		{
			// Obtener la mano del jugador objetivo
			if (MazoFisico.Instance != null && 
				MazoFisico.Instance.manosJugadores.TryGetValue(jugadorObjetivoID, out ManoJugador manoObjetivo))
			{
				// Si ya hay cartas seleccionadas, procesarlas antes de cancelar
				if (manoObjetivo.CartasSeleccionadasParaDinamita.Count > 0)
				{
					Debug.Log($"⚠️ Procesando {manoObjetivo.CartasSeleccionadasParaDinamita.Count} cartas seleccionadas antes de cancelar");
					ProcesarDescarteDinamita(manoObjetivo);
				}
				else
				{
					manoObjetivo.LimpiarSeleccionDinamita();
				}
			}
		}
		
		OcultarMensaje();
		Debug.Log("❌ Selección de cartas cancelada de forma segura");
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
	public void OnDiceActivated()
	{
		Debug.Log("🎲 Dado activado - Ocultando botón Utilizar y deseleccionando cartas");
		OcultarBotonUtilizar();
		
		// Deseleccionar cualquier carta seleccionada PERMANENTEMENTE
		if (manoJugadorActual != null)
		{
			manoJugadorActual.DeseleccionarTodasLasCartas();
			cartaSeleccionada = null;
		}
		
		// Asegurar que no se pueda volver a seleccionar cartas de acción
		if (GameManager.Instance != null)
		{
			GameManager.Instance.dadoTiradoEnEsteTurno = true;
		}
	}

	public void OnDiceDeactivated()
	{
		Debug.Log("🎲 Dado desactivado - Revisando estado del botón Utilizar");
		// Esperar un frame para que se actualice el estado del juego
		StartCoroutine(RevisarEstadoBotonDespuesDeDado());
	}

	private IEnumerator RevisarEstadoBotonDespuesDeDado()
	{
		yield return null; // Esperar un frame
		
		// Solo actualizar si no estamos en medio de una acción de carta
		if (!esperandoSeleccionObjetivo && !cartaEnUso)
		{
			ActualizarEstadoBoton();
		}
	}
}