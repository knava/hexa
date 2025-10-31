using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AIController : MonoBehaviour
{
    // Referencias
    private PlayerTotem myTotem;
    private PlayerTotem[] cachedAllTotems;
    private bool isMyTurn = false;

    void Awake()
    {
        myTotem = GetComponent<PlayerTotem>();
    }

    void Start()
    {
        // Cachear todos los totems al inicio
        cachedAllTotems = FindObjectsByType<PlayerTotem>(FindObjectsSortMode.None);
    }

    /// <summary>
	/// Inicia el turno de la IA en fase de movimiento (versión mejorada)
	/// </summary>
	public void StartAITurn()
	{
		if (GameManager.Instance != null && GameManager.Instance.juegoTerminado) return;
		if (myTotem == null) return;
		
		isMyTurn = true;
		
		// Evaluar uso de cartas de acción al inicio (INCLUYE DIAMANTE)
		EvaluarUsoCartasAccion();
		
		// Si usó carta de acción, terminar turno (ya se maneja en las corrutinas)
		if (!isMyTurn) return;
		
		// Pequeño delay para simular pensamiento y para que el jugador vea qué pasa
		StartCoroutine(DelayedAITurn());
	}
	
	private IEnumerator DelayedAITurn()
	{
		// Delay inicial para simular pensamiento
		yield return new WaitForSeconds(1f);
		
		// Evaluar uso de cartas de acción al inicio
		EvaluarUsoCartasAccion();
		
		// Si usó carta de acción, terminar turno
		if (!isMyTurn) yield break;
		
		// Esperar a que termine cualquier robo de carta previo
		while (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
		{
			yield return new WaitForSeconds(0.5f);
		}
		
		yield return new WaitForSeconds(0.5f);
		
		// Tirar el dado automáticamente
		GameManager.Instance.ForceDiceRollForAI();
		
		// Esperar resultado del dado
		while (GameManager.Instance.waitingForDiceRoll)
		{
			yield return null;
		}
		
		// Manejar robo de carta si cayó en casilla especial
		if (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
		{
			while (GameManager.Instance.esperandoRoboCarta)
			{
				yield return null;
			}
			
			GameManager.Instance.EndTurn();
			yield break;
		}
		
		// Continuar con movimiento normal
		MakeDecision();
	}

    /// <summary>
    /// Inicia el turno de construcción de la IA
    /// </summary>
    public void StartAIMaker()
    {
        if (GameManager.Instance != null && GameManager.Instance.juegoTerminado) return;
        StartCoroutine(AIMakerRoutine());
    }

    /// <summary>
    /// Rutina principal para la fase de construcción de la IA
    /// </summary>
    private IEnumerator AIMakerRoutine()
    {
        yield return new WaitForSeconds(1f);

        // Voltear un hexágono aleatorio
        HexagonPiece hexToFlip = GameManager.Instance.GetRandomUnflippedHexagon();
        if (hexToFlip == null)
        {
            GameManager.Instance.EndConstructionTurn();
            yield break;
        }

        yield return StartCoroutine(hexToFlip.FlipPiece(true));

        // Esperar a que termine la animación
        while (hexToFlip.isAnimating)
        {
            yield return null;
        }

        // Conectar el hexágono al tablero
        yield return StartCoroutine(ConnectHexagonToBoard(hexToFlip));

        yield return null;
        GameManager.Instance.EndConstructionTurn();
    }

    /// <summary>
    /// Conecta un hexágono al tablero usando el sistema de imanes
    /// </summary>
    private IEnumerator ConnectHexagonToBoard(HexagonPiece hexToConnect)
    {
        // Obtener imanes disponibles
        List<Transform> availableMagnets = MagnetSystem.Instance.allMagnets
            .Where(m => MagnetSystem.Instance.IsMagnetAvailableForAI(m))
            .ToList();

        if (availableMagnets.Count == 0) yield break;

        // Seleccionar y bloquear imán
        Transform targetMagnet = null;
        int attempts = 0;
        const int maxAttempts = 5;

        while (attempts < maxAttempts && targetMagnet == null)
        {
            Transform candidate = availableMagnets[Random.Range(0, availableMagnets.Count)];
            
            if (MagnetSystem.Instance.TryLockMagnet(candidate))
            {
                targetMagnet = candidate;
            }
            else
            {
                attempts++;
                yield return null;
            }
        }

        if (targetMagnet == null) yield break;

        // Obtener información de conexión
        HexagonPiece targetHex = MagnetSystem.Instance.GetPieceForMagnet(targetMagnet);
        string cleanMagnetName = targetMagnet.name.Split(' ')[0];

        if (!hexToConnect.magnetConnections.ContainsKey(cleanMagnetName))
        {
            MagnetSystem.Instance.UnlockMagnet(targetMagnet);
            yield break;
        }

        string hexagonMagnetName = hexToConnect.magnetConnections[cleanMagnetName];
        Transform hexagonMagnet = hexToConnect.transform.Find(hexagonMagnetName);

        if (hexagonMagnet == null || !MagnetSystem.Instance.VerifyMagnetForConnection(targetMagnet))
        {
            MagnetSystem.Instance.UnlockMagnet(targetMagnet);
            yield break;
        }

        // Mover y conectar
        yield return StartCoroutine(MoveHexagonToConnect(hexToConnect, targetMagnet, hexagonMagnet));
        MagnetSystem.Instance.UnlockMagnet(targetMagnet);
    }

    /// <summary>
    /// Mueve un hexágono para conectarlo con otro
    /// </summary>
    private IEnumerator MoveHexagonToConnect(HexagonPiece hex, Transform targetMagnet, Transform hexMagnet)
    {
        if (targetMagnet == null || hexMagnet == null) yield break;
        if (!MagnetSystem.Instance.IsMagnetLockedByAI(targetMagnet)) yield break;

        Vector3 connectionOffset = hexMagnet.position - hex.transform.position;
        Vector3 targetPosition = targetMagnet.position - connectionOffset;
        Vector3 startPos = hex.transform.position;
        Vector3 raisedPosition = startPos + Vector3.up * 1.0f;

        float liftDuration = 0.3f;
        float moveDuration = 0.6f;
        float descendDuration = 0.3f;

        hex.SetCollidersEnabled(false);

        // Animación en tres fases: subir, mover, bajar
        LeanTween.move(hex.gameObject, raisedPosition, liftDuration).setEase(LeanTweenType.easeOutQuad);
        yield return new WaitForSeconds(liftDuration);

        LeanTween.move(hex.gameObject, new Vector3(targetPosition.x, raisedPosition.y, targetPosition.z), moveDuration)
                 .setEase(LeanTweenType.easeInOutQuad);
        yield return new WaitForSeconds(moveDuration);

        LeanTween.move(hex.gameObject, targetPosition, descendDuration).setEase(LeanTweenType.easeInQuad);
        yield return new WaitForSeconds(descendDuration);

        hex.transform.position = targetPosition;
        hex.isConnected = true;
        
        // Confirmar conexión
        HexagonPiece targetPiece = MagnetSystem.Instance.GetPieceForMagnet(targetMagnet);
        if (MagnetSystem.Instance.ConfirmAIConnection(targetMagnet, hexMagnet) && targetPiece != null)
        {
            hex.RegisterConnection(targetPiece);
            MagnetSystem.Instance.ProcessNewConnection(hex, hexMagnet);
        }

        hex.SetCollidersEnabled(true);
        hex.SetMagnetsVisibility(true);
        MagnetSystem.Instance.UpdateMagnetOccupancyFromPhysics();
        targetPiece?.ForcePhysicalConnectionCheck();
        hex.ForcePhysicalConnectionCheck();
        MagnetSystem.Instance.UnlockMagnet(targetMagnet);
    }

    /// <summary>
    /// Procesa el turno completo de la IA en fase de movimiento
    /// </summary>
    private IEnumerator ProcessAITurn()
    {
        // Evaluar uso de cartas de acción al inicio
        EvaluarUsoCartasAccion();
        
        // Si usó carta de acción, terminar turno
        if (!isMyTurn) yield break;
        
        // Esperar a que termine cualquier robo de carta previo
        while (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        yield return new WaitForSeconds(1f);
        
        // Tirar el dado automáticamente
        GameManager.Instance.ForceDiceRollForAI();
        
        // Esperar resultado del dado
        while (GameManager.Instance.waitingForDiceRoll)
        {
            yield return null;
        }
        
        // Manejar robo de carta si cayó en casilla especial
        if (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
        {
            while (GameManager.Instance.esperandoRoboCarta)
            {
                yield return null;
            }
            
            GameManager.Instance.EndTurn();
            yield break;
        }
        
        // Continuar con movimiento normal
        MakeDecision();
    }

    /// <summary>
	/// Modificar MakeDecision para incluir debug
	/// </summary>
	private void MakeDecision()
	{
		if (!isMyTurn) return;
		if (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta) return;
		
		List<HexagonPiece> selectableHexagons = GameManager.Instance.selectableHexagons;
		
		// Debug de la decisión
		DebugAIDecision(selectableHexagons);
		
		HexagonPiece targetHex = ChooseBestHexagon();
		if (targetHex != null)
		{
			Debug.Log($"🎯 IA seleccionó: {targetHex.name} (Enemigo: {HasEnemyTotem(targetHex)}, RobarCarta: {targetHex.isStealCardPiece})");
			GameManager.Instance.SelectHexagon(targetHex);
		}
		else
		{
			Debug.Log("🎯 IA no encontró hexágono adecuado - Pasando turno");
			GameManager.Instance.EndTurn();
		}
	}
	
	/// <summary>
	/// Muestra información de debug sobre las decisiones de la IA
	/// </summary>
	private void DebugAIDecision(List<HexagonPiece> selectableHexagons)
	{
		if (selectableHexagons == null) return;
		
		Debug.Log($"=== 🤖 DECISIÓN IA Jugador {myTotem.playerID} ===");
		
		int priority1 = selectableHexagons.Count(hex => !hex.isMainPiece && HasEnemyTotem(hex) && hex.isStealCardPiece);
		int priority2 = selectableHexagons.Count(hex => !hex.isMainPiece && HasEnemyTotem(hex) && !hex.isStealCardPiece);
		int priority3 = selectableHexagons.Count(hex => hex.isStealCardPiece && !HasEnemyTotem(hex));
		
		Debug.Log($"- Prioridad 1 (Enemigo + Robar carta): {priority1}");
		Debug.Log($"- Prioridad 2 (Solo enemigo): {priority2}");
		Debug.Log($"- Prioridad 3 (Solo robar carta): {priority3}");
		Debug.Log($"- Total hexágonos disponibles: {selectableHexagons.Count}");
	}

    /// <summary>
    /// Elige el mejor hexágono para moverse según estrategia de IA
    /// </summary>
    private HexagonPiece ChooseBestHexagon()
	{
		List<HexagonPiece> selectableHexagons = GameManager.Instance.selectableHexagons;
		
		if (selectableHexagons == null || selectableHexagons.Count == 0)
		{
			return null;
		}

		// 🔥 PRIORIDAD MÁXIMA: Buscar hexágonos DIAMANTE (termina el juego inmediatamente)
		List<HexagonPiece> hexagonsDiamante = selectableHexagons
			.Where(hex => hex.isDiamondPiece)
			.ToList();

		if (hexagonsDiamante.Count > 0)
		{
			Debug.Log($"💎 IA: PRIORIDAD MÁXIMA - ¡DIAMANTE ENCONTRADO! Moviéndose para terminar el juego");
			return hexagonsDiamante[0]; // Devuelve el primero que encuentre
		}

		// PRIORIDAD 1: Buscar hexágonos con ENEMIGO + CASILLA DE ROBAR CARTA
		List<HexagonPiece> hexagonsWithEnemyAndStealCard = selectableHexagons
			.Where(hex => !hex.isMainPiece && HasEnemyTotem(hex) && hex.isStealCardPiece)
			.ToList();

		if (hexagonsWithEnemyAndStealCard.Count > 0)
		{
			Debug.Log($"🎯 IA: Prioridad 1 - Enemigo + Robar carta encontrado");
			return hexagonsWithEnemyAndStealCard[0];
		}

		// PRIORIDAD 2: Buscar hexágonos con ENEMIGO (sin casilla de robar carta)
		List<HexagonPiece> hexagonsWithEnemies = selectableHexagons
			.Where(hex => !hex.isMainPiece && HasEnemyTotem(hex))
			.ToList();

		if (hexagonsWithEnemies.Count > 0)
		{
			Debug.Log($"🎯 IA: Prioridad 2 - Enemigo encontrado");
			return hexagonsWithEnemies[0];
		}

		// PRIORIDAD 3: Buscar hexágonos con CASILLA DE ROBAR CARTA (sin enemigo)
		HexagonPiece stealCardHex = selectableHexagons
			.FirstOrDefault(hex => hex.isStealCardPiece && !HasEnemyTotem(hex));

		if (stealCardHex != null)
		{
			Debug.Log($"🎯 IA: Prioridad 3 - Casilla robar carta encontrada");
			return stealCardHex;
		}

		// PRIORIDAD 4: Estrategia original (mismo color, etc.)
		Debug.Log($"🎯 IA: Prioridad 4 - Aplicando estrategia por color");
		return ChooseBestHexagonByColor(selectableHexagons);
	}
	
	/// <summary>
	/// Estrategia por color cuando no hay objetivos prioritarios
	/// </summary>
	private HexagonPiece ChooseBestHexagonByColor(List<HexagonPiece> selectableHexagons)
	{
		HexagonPiece chosenHex = selectableHexagons[0];
		
		// Buscar hexágonos del mismo color
		foreach (var hex in selectableHexagons)
		{
			if (hex.PieceColor == myTotem.playerColor)
			{
				chosenHex = hex;
				break;
			}
		}

		// Si no hay del mismo color, buscar el que esté más cerca del centro o tenga mejor posición estratégica
		if (chosenHex == selectableHexagons[0])
		{
			// Estrategia adicional: priorizar hexágonos conectados a más piezas
			chosenHex = selectableHexagons
				.OrderByDescending(hex => GetHexagonStrategicValue(hex))
				.FirstOrDefault();
		}

		return chosenHex;
	}
	
	/// <summary>
	/// Calcula el valor estratégico de un hexágono basado en conexiones y posición
	/// </summary>
	private int GetHexagonStrategicValue(HexagonPiece hex)
	{
		int value = 0;
		
		// Valor por conexiones (cuantas más conexiones, mejor)
		if (hex.connectedPieces != null)
		{
			value += hex.connectedPieces.Count * 2;
		}
		
		// Valor por proximidad al centro (asumiendo que el centro es (0,0,0))
		float distanceToCenter = Vector3.Distance(hex.transform.position, Vector3.zero);
		value += Mathf.RoundToInt(10f - distanceToCenter); // Más cerca = más valor
		
		// Penalizar piezas principales (si es relevante)
		if (hex.isMainPiece)
		{
			value -= 5;
		}
		
		return value;
	}

    /// <summary>
	/// Verifica si un hexágono tiene totems enemigos (versión mejorada)
	/// </summary>
	private bool HasEnemyTotem(HexagonPiece hex)
	{
		if (cachedAllTotems == null)
		{
			cachedAllTotems = FindObjectsByType<PlayerTotem>(FindObjectsSortMode.None);
		}
		
		return cachedAllTotems.Any(totem => 
			totem != myTotem && 
			totem.currentHexagon == hex && 
			!totem.currentHexagon.isMainPiece);
	}

    /// <summary>
    /// Decide qué robar durante el robo por comer
    /// </summary>
    public void DecidirRoboPorComer()
    {
        if (!isMyTurn) return;
        
        // Porcentajes: 70% robar del jugador comido, 30% robar del mazo
        bool robarDelJugador = Random.Range(0, 100) < 70;
        
        if (robarDelJugador)
        {
            RobarCartaDelJugadorComido();
        }
        else
        {
            RobarCartaDelMazo();
        }
    }

    /// <summary>
    /// Roba carta del mazo durante robo por comer
    /// </summary>
    private void RobarCartaDelMazo()
    {
        GameObject cartaSuperior = MazoFisico.Instance?.GetCartaSuperior();
        if (cartaSuperior != null)
        {
            MazoFisico.Instance.ProcesarClicCarta(cartaSuperior);
        }
        else
        {
            RobarCartaDelJugadorComido();
        }
    }

    /// <summary>
    /// Roba carta del jugador comido
    /// </summary>
    private void RobarCartaDelJugadorComido()
    {
        if (GameManager.Instance.jugadorComido != null && MazoFisico.Instance != null)
        {
            int jugadorComidoID = GameManager.Instance.jugadorComido.playerID;
            
            if (MazoFisico.Instance.manosJugadores.TryGetValue(jugadorComidoID, out ManoJugador manoVictima))
            {
                if (manoVictima.CantidadCartas > 0)
                {
                    GameObject cartaARobar = manoVictima.GetPrimeraCarta();
                    if (cartaARobar != null)
                    {
                        // Actualizar escala solo si es necesario
                        bool victimaEsIA = manoVictima.esIA;
                        bool atacanteEsIA = true;
                        
                        if (victimaEsIA != atacanteEsIA)
                        {
                            Carta3D cartaScript = cartaARobar.GetComponent<Carta3D>();
                            if (cartaScript != null)
                            {
                                cartaScript.CambiarEscala(atacanteEsIA);
                            }
                        }
                        
                        MazoFisico.Instance.ProcesarClicCarta(cartaARobar);
                        return;
                    }
                }
            }
        }
        
        // Fallback: robar del mazo
        RobarCartaDelMazo();
    }

    /// <summary>
    /// Evalúa si usar cartas de acción al inicio del turno
    /// </summary>
    public void EvaluarUsoCartasAccion()
	{
		if (!isMyTurn) return;
		
		// PRIORIDAD 1: Usar Diamante si está disponible
		GameObject cartaDiamante = BuscarCartaDiamanteEnMano();
		if (cartaDiamante != null)
		{
			// Cambiar estado inmediatamente para evitar doble ejecución
			isMyTurn = false;
			StartCoroutine(UsarDiamanteContraObjetivo(cartaDiamante));
			return;
		}
		
		// PRIORIDAD 2: Usar Dinamita si está disponible
		GameObject cartaDinamita = BuscarCartaDinamitaEnMano();
		if (cartaDinamita == null) return;
		
		int objetivoID = BuscarObjetivoParaDinamita();
		if (objetivoID != -1)
		{
			// Cambiar estado inmediatamente para evitar doble ejecución
			isMyTurn = false;
			StartCoroutine(UsarDinamitaContraObjetivo(cartaDinamita, objetivoID));
		}
	}

    /// <summary>
    /// Busca carta de dinamita en la mano de la IA
    /// </summary>
    private GameObject BuscarCartaDinamitaEnMano()
    {
        if (MazoFisico.Instance != null && 
            MazoFisico.Instance.manosJugadores.TryGetValue(myTotem.playerID, out ManoJugador mano))
        {
            foreach (GameObject carta in mano.GetCartas())
            {
                Carta3D cartaScript = carta.GetComponent<Carta3D>();
                if (cartaScript != null && cartaScript.GetTipoCarta() == CardType.Dinamita)
                {
                    return carta;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Busca el mejor objetivo para la carta de dinamita
    /// </summary>
    private int BuscarObjetivoParaDinamita()
    {
        if (MazoFisico.Instance == null) return -1;
        
        int mejorObjetivo = -1;
        int maxCartas = 3; // Mínimo 4 cartas para usar Dinamita
        
        foreach (var kvp in MazoFisico.Instance.manosJugadores)
        {
            int jugadorID = kvp.Key;
            ManoJugador mano = kvp.Value;
            
            // No atacarse a sí mismo
            if (jugadorID == myTotem.playerID) continue;
            
            // Verificar que el objetivo tenga al menos 4 cartas
            if (mano.CantidadCartas >= 4 && mano.CantidadCartas > maxCartas)
            {
                mejorObjetivo = jugadorID;
                maxCartas = mano.CantidadCartas;
            }
        }
        
        return mejorObjetivo;
    }

    /// <summary>
    /// Usa la carta de dinamita contra un objetivo
    /// </summary>
    private IEnumerator UsarDinamitaContraObjetivo(GameObject cartaDinamita, int objetivoID)
    {
        // Bloquear EndTurn automático
        if (GameManager.Instance != null)
        {
            GameManager.Instance.bloquearEndTurnAutomatico = true;
        }
        
        // Delay inicial para simular pensamiento
        yield return new WaitForSeconds(1.5f);
        
        // Seleccionar carta de Dinamita
        if (MazoFisico.Instance.manosJugadores.TryGetValue(myTotem.playerID, out ManoJugador manoIA))
        {
            manoIA.SeleccionarCarta(cartaDinamita);
            
            // Feedback visual
            LeanTween.moveLocal(cartaDinamita, cartaDinamita.transform.localPosition + Vector3.up * 0.3f, 0.5f)
                .setEase(LeanTweenType.easeOutBack);
        }
        
        yield return new WaitForSeconds(1f);
        
        // Mostrar mensaje en UI
        if (GestionBotonesCartas.Instance != null)
        {
            GestionBotonesCartas.Instance.MostrarMensaje($"IA Jugador {myTotem.playerID} usa Dinamita contra Jugador {objetivoID}");
        }
        
        yield return new WaitForSeconds(2f);
        
        // Resaltar avatar del objetivo
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarAvatar(objetivoID, true);
        }
        
        yield return new WaitForSeconds(1.5f);
        
        // Ejecutar la Dinamita
        yield return StartCoroutine(EjecutarDinamitaIAConDelay(objetivoID));
        
        // Quitar resaltado del objetivo
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarAvatar(objetivoID, false);
        }
        
        // Mover carta al descarte con animación
        if (MazoDescarte.Instance != null && manoIA != null)
        {
            Vector3 posicionDescarte = MazoDescarte.Instance.transform.position;
            
            LeanTween.move(cartaDinamita, posicionDescarte, 1f)
                .setEase(LeanTweenType.easeInOutCubic);
            
            LeanTween.rotate(cartaDinamita, new Vector3(90f, 0f, 0f), 0.5f);
            
            yield return new WaitForSeconds(1f);
            
            MazoDescarte.Instance.AgregarCartaDescarte(cartaDinamita);
            manoIA.RemoverCarta(cartaDinamita);
        }
        
        // Ocultar mensaje
        if (GestionBotonesCartas.Instance != null)
        {
            GestionBotonesCartas.Instance.OcultarMensaje();
        }
        
        yield return new WaitForSeconds(1f);
        
        // Desbloquear EndTurn y terminar turno
        if (GameManager.Instance != null)
        {
            GameManager.Instance.bloquearEndTurnAutomatico = false;
        }
        
        GameManager.Instance?.EndTurn();
    }

    /// <summary>
    /// Ejecuta la lógica de la dinamita con delays para animación
    /// </summary>
    private IEnumerator EjecutarDinamitaIAConDelay(int jugadorObjetivoID)
    {
        if (MazoFisico.Instance != null && 
            MazoFisico.Instance.manosJugadores.TryGetValue(jugadorObjetivoID, out ManoJugador manoObjetivo))
        {
            int cartasTotales = manoObjetivo.CantidadCartas;
            int cartasADescartar = Mathf.CeilToInt(cartasTotales / 2f);
            
            if (cartasADescartar > 0)
            {
                // Mostrar mensaje de conteo
                if (GestionBotonesCartas.Instance != null)
                {
                    GestionBotonesCartas.Instance.MostrarMensaje($"Descartando {cartasADescartar} cartas del Jugador {jugadorObjetivoID}");
                }
                
                yield return new WaitForSeconds(1.5f);
                
                // Seleccionar cartas aleatoriamente para descartar
                List<GameObject> cartasEnMano = manoObjetivo.GetCartas();
                List<GameObject> cartasADescartarLista = new List<GameObject>();
                
                // Barajar cartas para selección aleatoria
                for (int i = 0; i < cartasEnMano.Count; i++)
                {
                    int randomIndex = Random.Range(i, cartasEnMano.Count);
                    GameObject temp = cartasEnMano[i];
                    cartasEnMano[i] = cartasEnMano[randomIndex];
                    cartasEnMano[randomIndex] = temp;
                }
                
                // Tomar las primeras N cartas para descartar
                for (int i = 0; i < cartasADescartar && i < cartasEnMano.Count; i++)
                {
                    cartasADescartarLista.Add(cartasEnMano[i]);
                }
                
                // Descarte con animaciones
                for (int i = 0; i < cartasADescartarLista.Count; i++)
                {
                    GameObject carta = cartasADescartarLista[i];
                    
                    if (carta != null && MazoDescarte.Instance != null)
                    {
                        // Efecto visual de resaltado
                        LeanTween.moveLocal(carta, carta.transform.localPosition + Vector3.up * 0.5f, 0.3f)
                            .setEase(LeanTweenType.easeOutBack);
                        
                        // Mostrar progreso
                        if (GestionBotonesCartas.Instance != null)
                        {
                            GestionBotonesCartas.Instance.MostrarMensaje($"Descartando carta {i+1}/{cartasADescartar}");
                        }
                        
                        yield return new WaitForSeconds(0.8f);
                        
                        // Animación al descarte
                        Vector3 posicionDescarte = MazoDescarte.Instance.transform.position;
                        LeanTween.move(carta, posicionDescarte, 0.8f)
                            .setEase(LeanTweenType.easeInOutCubic);
                        
                        LeanTween.rotate(carta, new Vector3(90f, 0f, 0f), 0.5f);
                        
                        yield return new WaitForSeconds(0.8f);
                        
                        // Mover efectivamente al descarte
                        MazoDescarte.Instance.AgregarCartaDescarte(carta);
                        manoObjetivo.RemoverCarta(carta);
                        
                        yield return new WaitForSeconds(0.5f);
                    }
                }
                
                // Mensaje final
                if (GestionBotonesCartas.Instance != null)
                {
                    GestionBotonesCartas.Instance.MostrarMensaje($"¡{cartasADescartarLista.Count} cartas descartadas!");
                    yield return new WaitForSeconds(1.5f);
                    GestionBotonesCartas.Instance.OcultarMensaje();
                }
            }
        }
    }

    /// <summary>
    /// Fuerza el fin del turno de la IA
    /// </summary>
    public void ForzarFinTurno()
    {
        StopAllCoroutines();
        isMyTurn = false;
        
        // Asegurar desbloqueo del GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.bloquearEndTurnAutomatico = false;
        }
        
        // Limpiar mensajes pendientes
        if (GestionBotonesCartas.Instance != null)
        {
            GestionBotonesCartas.Instance.OcultarMensaje();
        }
        
        // Des-resaltar avatares
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarTodosLosAvatares(false);
        }
    }
	
	/// <summary>
	/// Busca carta de Diamante en la mano de la IA
	/// </summary>
	private GameObject BuscarCartaDiamanteEnMano()
	{
		if (MazoFisico.Instance != null && 
			MazoFisico.Instance.manosJugadores.TryGetValue(myTotem.playerID, out ManoJugador mano))
		{
			foreach (GameObject carta in mano.GetCartas())
			{
				Carta3D cartaScript = carta.GetComponent<Carta3D>();
				if (cartaScript != null && cartaScript.GetTipoCarta() == CardType.Diamante)
				{
					return carta;
				}
			}
		}
		return null;
	}
	
	/// <summary>
	/// Usa la carta de Diamante
	/// </summary>
	private IEnumerator UsarDiamanteContraObjetivo(GameObject cartaDiamante)
	{
		// Bloquear EndTurn automático
		if (GameManager.Instance != null)
		{
			GameManager.Instance.bloquearEndTurnAutomatico = true;
		}
		
		// Delay inicial para simular pensamiento
		yield return new WaitForSeconds(1.5f);
		
		// 1. VOLTEAR LA CARTA PARA MOSTRARLA AL JUGADOR HUMANO
		yield return StartCoroutine(VoltearCartaDiamante(cartaDiamante));
		
		// 2. Mostrar mensaje en UI
		if (GestionBotonesCartas.Instance != null)
		{
			GestionBotonesCartas.Instance.MostrarMensaje($"IA Jugador {myTotem.playerID} usa Diamante");
		}
		
		yield return new WaitForSeconds(1f);
		
		// 3. Iniciar colocación del Diamante
		yield return StartCoroutine(ColocarDiamanteIA(cartaDiamante));
	}
	
	/// <summary>
	/// Voltea la carta Diamante para que el jugador humano la vea
	/// </summary>
	private IEnumerator VoltearCartaDiamante(GameObject cartaDiamante)
	{
		if (cartaDiamante == null) yield break;
		
		Carta3D cartaScript = cartaDiamante.GetComponent<Carta3D>();
		if (cartaScript == null) yield break;
		
		Debug.Log("🃏 IA mostrando carta Diamante al jugador humano");
		
		// Obtener la mano de la IA
		if (MazoFisico.Instance != null && 
			MazoFisico.Instance.manosJugadores.TryGetValue(myTotem.playerID, out ManoJugador manoIA))
		{
			// 1. Levantar la carta para destacarla
			Vector3 posicionOriginal = cartaDiamante.transform.localPosition;
			Vector3 posicionElevada = posicionOriginal + Vector3.up * 0.8f;
			
			LeanTween.moveLocal(cartaDiamante, posicionElevada, 0.5f)
				.setEase(LeanTweenType.easeOutBack);
			
			yield return new WaitForSeconds(0.5f);
			
			// 2. Voltear la carta para mostrar el frente
			cartaScript.MostrarFrente();
			
			// Efecto visual de volteo (rotación)
			Vector3 rotacionOriginal = cartaDiamante.transform.localEulerAngles;
			Vector3 rotacionVolteo = new Vector3(rotacionOriginal.x, 180f, rotacionOriginal.z);
			
			/*LeanTween.rotateLocal(cartaDiamante, rotacionVolteo, 0.8f)
				.setEase(LeanTweenType.easeInOutQuad);*/
			
			yield return new WaitForSeconds(0.8f);
			
			// 3. Mantener visible por un tiempo
			yield return new WaitForSeconds(1.5f);
			
			// 4. Volver a la posición original (pero mantener el frente visible)
			/*LeanTween.moveLocal(cartaDiamante, posicionOriginal, 0.5f)
				.setEase(LeanTweenType.easeInBack);*/
				
			yield return new WaitForSeconds(0.5f);
			
			Debug.Log("✅ Carta Diamante mostrada correctamente");
		}
	}

	/// <summary>
	/// Rutina principal para colocar el Diamante (similar a construcción)
	/// </summary>
	private IEnumerator ColocarDiamanteIA(GameObject cartaDiamante)
	{
		bool imanesActivados = false;
		
		try
		{
			// 1. ACTIVAR EL DIAMANTE
			if (GestionBotonesCartas.Instance == null || 
				GestionBotonesCartas.Instance.hexagonoDiamantePrefab == null)
			{
				Debug.LogError("❌ No se puede activar Diamante - Referencias nulas");
				CancelarUsoDiamante(cartaDiamante);
				yield break;
			}

			// Activar el diamante
			GestionBotonesCartas.Instance.hexagonoDiamantePrefab.SetActive(true);
			GameObject diamante = GestionBotonesCartas.Instance.hexagonoDiamantePrefab;
			HexagonPiece diamantePiece = diamante.GetComponent<HexagonPiece>();
			
			if (diamantePiece == null)
			{
				Debug.LogError("❌ El Diamante no tiene componente HexagonPiece");
				CancelarUsoDiamante(cartaDiamante);
				yield break;
			}

			// Configurar el diamante
			diamantePiece.isConnected = false;
			diamantePiece.SetCollidersEnabled(true);
			diamantePiece.isFlipped = true;

			Debug.Log("💎 IA activando Diamante para colocación");

			yield return new WaitForSeconds(0.5f);

			// 2. ACTIVAR IMANES VISUALMENTE (PARA QUE EL JUGADOR HUMANO VEA LAS OPCIONES)
			if (MagnetSystem.Instance != null)
			{
				MagnetSystem.Instance.ActivarImanesParaColocacion();
				imanesActivados = true;
				Debug.Log("🧲 Imanes activados visualmente para Diamante (IA)");
			}

			// Esperar un poco para que el jugador vea los imanes disponibles
			yield return new WaitForSeconds(1.5f);

			// 3. SELECCIONAR Y BLOQUEAR IMÁN DISPONIBLE
			Transform targetMagnet = SeleccionarMejorImanParaDiamante();
			
			if (targetMagnet == null)
			{
				Debug.LogWarning("⚠️ IA no encontró imán disponible para Diamante");
				CancelarUsoDiamante(cartaDiamante);
				yield break;
			}

			Debug.Log($"🎯 IA seleccionó imán: {targetMagnet.name}");

			// 4. CALCULAR CONEXIÓN
			string cleanMagnetName = targetMagnet.name.Split(' ')[0];
			
			if (!diamantePiece.magnetConnections.ContainsKey(cleanMagnetName))
			{
				Debug.LogError($"❌ No se puede conectar Diamante al imán {cleanMagnetName}");
				MagnetSystem.Instance.UnlockMagnet(targetMagnet);
				CancelarUsoDiamante(cartaDiamante);
				yield break;
			}

			string diamanteMagnetName = diamantePiece.magnetConnections[cleanMagnetName];
			Transform diamanteMagnet = diamante.transform.Find(diamanteMagnetName);

			if (diamanteMagnet == null)
			{
				Debug.LogError($"❌ No se encontró imán del Diamante: {diamanteMagnetName}");
				MagnetSystem.Instance.UnlockMagnet(targetMagnet);
				CancelarUsoDiamante(cartaDiamante);
				yield break;
			}

			// 5. MOVER Y CONECTAR EL DIAMANTE
			yield return StartCoroutine(MoverDiamanteAIConexion(diamante, targetMagnet, diamanteMagnet, diamantePiece));
			
			// 6. DESBLOQUEAR IMÁN Y FINALIZAR
			MagnetSystem.Instance.UnlockMagnet(targetMagnet);
			
			// Finalizar colocación exitosa
			yield return StartCoroutine(FinalizarUsoDiamante(cartaDiamante));
		}
		finally
		{
			// 7. DESACTIVAR IMANES VISUALMENTE (SIEMPRE, INCLUSO EN CASO DE ERROR)
			if (imanesActivados && MagnetSystem.Instance != null)
			{
				MagnetSystem.Instance.DesactivarImanesColocacion();
				Debug.Log("🧲 Imanes desactivados visualmente después de colocar Diamante (IA)");
			}
		}
	}

	/// <summary>
	/// Selecciona el mejor imán para colocar el Diamante
	/// </summary>
	private Transform SeleccionarMejorImanParaDiamante()
	{
		// Obtener imanes disponibles (mismo criterio que construcción)
		List<Transform> availableMagnets = MagnetSystem.Instance.allMagnets
			.Where(m => MagnetSystem.Instance.IsMagnetAvailableForAI(m))
			.Where(m => !EsImanDeNuestroColor(m)) // 🔴 FILTRO CRÍTICO: Excluir nuestro color
			.ToList();

		if (availableMagnets.Count == 0)
		{
			Debug.Log("❌ No hay imanes disponibles para Diamante (todos son de nuestro color o no disponibles)");
			return null;
		}

		Debug.Log($"🔍 Evaluando {availableMagnets.Count} imanes disponibles para Diamante (excluyendo nuestro color)");

		// Estrategia: seleccionar imán que maximice el valor estratégico
		Transform bestMagnet = null;
		float bestScore = -1000f;

		foreach (Transform magnet in availableMagnets)
		{
			// Verificar que el imán sigue disponible antes de intentar bloquearlo
			if (!MagnetSystem.Instance.IsMagnetAvailableForAI(magnet))
				continue;

			float score = CalcularPuntuacionImanParaDiamante(magnet);
			
			// Solo considerar puntuaciones razonables
			if (score > -10f && MagnetSystem.Instance.TryLockMagnet(magnet))
			{
				if (score > bestScore)
				{
					// Liberar el imán anterior si había uno
					if (bestMagnet != null)
					{
						MagnetSystem.Instance.UnlockMagnet(bestMagnet);
					}
					
					bestScore = score;
					bestMagnet = magnet;
					Debug.Log($"✅ Nuevo mejor imán: {magnet.name} con puntuación: {bestScore}");
				}
				else
				{
					// Liberar este imán ya que no es el mejor
					MagnetSystem.Instance.UnlockMagnet(magnet);
				}
			}
			else
			{
				Debug.Log($"❌ Iman {magnet.name} descartado - Puntuación: {score}");
			}
		}

		if (bestMagnet != null)
		{
			Debug.Log($"🎯 MEJOR IMÁN SELECCIONADO: {bestMagnet.name} con puntuación: {bestScore}");
			
			// Mostrar información detallada del imán seleccionado
			HexagonPiece piezaSeleccionada = MagnetSystem.Instance.GetPieceForMagnet(bestMagnet);
			if (piezaSeleccionada != null)
			{
				Debug.Log($"💎 Diamante se colocará en: {piezaSeleccionada.name} (Color: {piezaSeleccionada.PieceColor})");
			}
		}
		else
		{
			Debug.Log("⚠️ No se pudo seleccionar ningún imán con puntuación aceptable");
		}

		return bestMagnet;
	}

	/// <summary>
	/// Calcula la puntuación estratégica de un imán para colocar el Diamante
	/// </summary>
	private float CalcularPuntuacionImanParaDiamante(Transform magnet)
	{
		float score = 0f;
		
		// Obtener la pieza a la que está conectado el imán
		HexagonPiece connectedPiece = MagnetSystem.Instance.GetPieceForMagnet(magnet);
		if (connectedPiece == null) return -1000f;

		// Obtener todos los jugadores rivales
		List<PlayerTotem> rivales = ObtenerJugadoresRivales();
		if (rivales.Count == 0) return score;

		Debug.Log($"🎯 Calculando puntuación para imán {magnet.name} en pieza {connectedPiece.name} (Color: {connectedPiece.PieceColor})");

		// 🔴 PENALIZACIÓN MÁXIMA: Si la pieza es de NUESTRO COLOR
		if (connectedPiece.PieceColor == myTotem.playerColor)
		{
			Debug.Log($"   🚫🚫🚫 PENALIZACIÓN MÁXIMA: Pieza de nuestro propio color");
			return -1000f; // Descartar completamente
		}

		// 🟢 BONUS MÁXIMO: Si la pieza es de color de ALGÚN RIVAL
		bool esColorRival = rivales.Any(rival => connectedPiece.PieceColor == rival.playerColor);
		if (esColorRival)
		{
			score += 50f; // Bonus muy alto por estar en color rival
			Debug.Log($"   ✅✅✅ BONUS MÁXIMO: Pieza de color rival");
		}

		// 1. ESTRATEGIA PRINCIPAL: CERCANÍA A RIVALES (pero NO en nuestro color)
		float puntuacionRivales = CalcularPuntuacionProximidadRivales(connectedPiece, rivales);
		score += puntuacionRivales;

		// 2. EVITAR NUESTRO PROPIO COLOR EN EL ENTORNO
		float puntuacionNuestroColor = CalcularPuntuacionNuestroColor(connectedPiece);
		score -= puntuacionNuestroColor * 10f; // Penalización muy alta

		// 3. ESTRATEGIA: POSICIONES ESTRECHAS/CERRADAS
		float puntuacionEstrategia = CalcularPuntuacionEstrategica(connectedPiece);
		score += puntuacionEstrategia;

		// 4. CENTRALIDAD MODERADA
		float distanciaAlCentro = Vector3.Distance(connectedPiece.transform.position, Vector3.zero);
		score += (3f - Mathf.Min(distanciaAlCentro, 3f)); // Bonus moderado por centralidad

		// 5. EVITAR PIEZAS PRINCIPALES
		if (connectedPiece.isMainPiece)
		{
			score -= 20f;
		}

		Debug.Log($"🧮 Iman {magnet.name} - Puntuación final: {score}");

		return score;
	}
	
	/// <summary>
	/// Obtiene la lista de jugadores rivales (excluyendo al propio)
	/// </summary>
	private List<PlayerTotem> ObtenerJugadoresRivales()
	{
		List<PlayerTotem> rivales = new List<PlayerTotem>();
		
		if (GameManager.Instance != null)
		{
			foreach (PlayerTotem jugador in GameManager.Instance.players)
			{
				if (jugador != myTotem && jugador.playerID != myTotem.playerID)
				{
					rivales.Add(jugador);
				}
			}
		}
		
		return rivales;
	}

	/// <summary>
	/// Calcula puntuación basada en proximidad a colores de rivales
	/// </summary>
	private float CalcularPuntuacionProximidadRivales(HexagonPiece pieza, List<PlayerTotem> rivales)
	{
		float puntuacion = 0f;
		int hexagonosRivalCercanos = 0;

		// Buscar todos los hexágonos del tablero
		HexagonPiece[] todosHexagonos = FindObjectsByType<HexagonPiece>(FindObjectsSortMode.None);
		
		foreach (HexagonPiece hex in todosHexagonos)
		{
			if (hex == pieza || !hex.isFlipped) continue;

			// Calcular distancia
			float distancia = Vector3.Distance(pieza.transform.position, hex.transform.position);
			
			// Solo considerar hexágonos cercanos (dentro de 3 unidades)
			if (distancia <= 3f)
			{
				// Verificar si este hexágono es de color de algún rival
				foreach (PlayerTotem rival in rivales)
				{
					if (hex.PieceColor == rival.playerColor)
					{
						hexagonosRivalCercanos++;
						
						// Mientras más cerca del rival, MÁS puntos
						float puntuacionDistancia = (3f - distancia) * 5f; // Aumentamos el multiplicador
						puntuacion += puntuacionDistancia;
						
						Debug.Log($"   🔍 Hexágono rival cercano: {hex.name} (distancia: {distancia:F2}, color rival: {rival.playerColor})");
						break;
					}
				}
			}
		}

		// Bonus por cantidad de hexágonos rivales cercanos
		puntuacion += hexagonosRivalCercanos * 3f;

		Debug.Log($"   🎯 Proximidad rivales: {hexagonosRivalCercanos} hexágonos - Puntuación: {puntuacion}");
		return puntuacion;
	}

	/// <summary>
	/// Calcula penalización por proximidad a nuestro propio color
	/// </summary>
	private float CalcularPuntuacionNuestroColor(HexagonPiece pieza)
	{
		float penalizacion = 0f;
		int hexagonosNuestroColorCercanos = 0;

		HexagonPiece[] todosHexagonos = FindObjectsByType<HexagonPiece>(FindObjectsSortMode.None);
		
		foreach (HexagonPiece hex in todosHexagonos)
		{
			if (hex == pieza || !hex.isFlipped) continue;

			float distancia = Vector3.Distance(pieza.transform.position, hex.transform.position);
			
			// Solo considerar hexágonos cercanos (dentro de 3.5 unidades)
			if (distancia <= 3.5f)
			{
				// Penalizar si es de nuestro color
				if (hex.PieceColor == myTotem.playerColor)
				{
					hexagonosNuestroColorCercanos++;
					
					// Mientras más cerca de nuestro color, MÁS penalización
					float penalizacionDistancia = (3.5f - distancia) * 8f; // Penalización muy alta
					penalizacion += penalizacionDistancia;
					
					Debug.Log($"   ⚠️ Hexágono nuestro color cercano: {hex.name} (distancia: {distancia:F2}, penalización: {penalizacionDistancia})");
				}
			}
		}

		// Penalización adicional por cantidad
		penalizacion += hexagonosNuestroColorCercanos * 5f;

		Debug.Log($"   🚫 Nuestro color cercano: {hexagonosNuestroColorCercanos} hexágonos - Penalización total: {penalizacion}");
		return penalizacion;
	}
	
	/// <summary>
	/// Verifica si un imán está en una pieza de nuestro color (para descarte inmediato)
	/// </summary>
	private bool EsImanDeNuestroColor(Transform magnet)
	{
		HexagonPiece connectedPiece = MagnetSystem.Instance.GetPieceForMagnet(magnet);
		if (connectedPiece == null) return false;
		
		bool esNuestroColor = connectedPiece.PieceColor == myTotem.playerColor;
		
		if (esNuestroColor)
		{
			Debug.Log($"❌❌❌ IMÁN DESCARTADO: {magnet.name} está en pieza de NUESTRO COLOR");
		}
		
		return esNuestroColor;
	}

	/// <summary>
	/// Calcula puntuación basada en estrategia de posición (dificultad de acceso)
	/// </summary>
	private float CalcularPuntuacionEstrategica(HexagonPiece pieza)
	{
		float puntuacion = 0f;

		// Obtener vecinos de esta pieza
		List<HexagonPiece> vecinos = GameManager.Instance.GetNeighbors(pieza);
		
		// Estrategia 1: Preferir piezas con menos conexiones (más "callejones sin salida")
		int conexiones = vecinos.Count;
		puntuacion += (6 - Mathf.Min(conexiones, 6)) * 1.5f; // Menos conexiones = más puntos

		// Estrategia 2: Preferir piezas que estén en "bordes" del tablero
		float distanciaAlCentro = Vector3.Distance(pieza.transform.position, Vector3.zero);
		if (distanciaAlCentro > 3f)
		{
			puntuacion += 2f; // Bonus por estar en periferia
		}

		// Estrategia 3: Preferir piezas que no sean de robar carta (para no dar ventajas adicionales)
		if (!pieza.isStealCardPiece)
		{
			puntuacion += 1f;
		}

		Debug.Log($"   🧩 Pieza {pieza.name} - Estrategia: {puntuacion} (conexiones: {conexiones})");
		return puntuacion;
	}

	/// <summary>
	/// Mueve el Diamante para conectarlo (igual que construcción)
	/// </summary>
	private IEnumerator MoverDiamanteAIConexion(GameObject diamante, Transform targetMagnet, Transform diamanteMagnet, HexagonPiece diamantePiece)
	{
		if (targetMagnet == null || diamanteMagnet == null) yield break;
		if (!MagnetSystem.Instance.IsMagnetLockedByAI(targetMagnet)) yield break;

		Vector3 connectionOffset = diamanteMagnet.position - diamante.transform.position;
		Vector3 targetPosition = targetMagnet.position - connectionOffset;
		Vector3 startPos = diamante.transform.position;
		Vector3 raisedPosition = startPos + Vector3.up * 1.0f;

		float liftDuration = 0.3f;
		float moveDuration = 0.6f;
		float descendDuration = 0.3f;

		diamantePiece.SetCollidersEnabled(false);

		Debug.Log("💎 IA moviendo Diamante a posición...");

		// Fase 1: Levantar
		LeanTween.move(diamante, raisedPosition, liftDuration).setEase(LeanTweenType.easeOutQuad);
		yield return new WaitForSeconds(liftDuration);

		// Fase 2: Mover horizontalmente
		LeanTween.move(diamante, new Vector3(targetPosition.x, raisedPosition.y, targetPosition.z), moveDuration)
				 .setEase(LeanTweenType.easeInOutQuad);
		yield return new WaitForSeconds(moveDuration);

		// Fase 3: Bajar
		LeanTween.move(diamante, targetPosition, descendDuration).setEase(LeanTweenType.easeInQuad);
		yield return new WaitForSeconds(descendDuration);

		diamante.transform.position = targetPosition;
		diamantePiece.isConnected = true;
		
		// Confirmar conexión (igual que construcción)
		HexagonPiece targetPiece = MagnetSystem.Instance.GetPieceForMagnet(targetMagnet);
		if (MagnetSystem.Instance.ConfirmAIConnection(targetMagnet, diamanteMagnet) && targetPiece != null)
		{
			diamantePiece.RegisterConnection(targetPiece);
			MagnetSystem.Instance.ProcessNewConnection(diamantePiece, diamanteMagnet);
			Debug.Log($"💎 Diamante conectado a {targetPiece.name}");
		}

		diamantePiece.SetCollidersEnabled(true);
		MagnetSystem.Instance.UpdateMagnetOccupancyFromPhysics();
		targetPiece?.ForcePhysicalConnectionCheck();
		diamantePiece.ForcePhysicalConnectionCheck();
	}

	/// <summary>
	/// Finaliza el uso del Diamante
	/// </summary>
	private IEnumerator FinalizarUsoDiamante(GameObject cartaDiamante)
	{
		Debug.Log("✅ IA finalizando uso de Diamante");

		// Asegurar que la carta sigue mostrando el frente
		Carta3D cartaScript = cartaDiamante.GetComponent<Carta3D>();
		if (cartaScript != null)
		{
			cartaScript.MostrarFrente();
		}

		// Mover carta al descarte
		if (MazoDescarte.Instance != null && 
			MazoFisico.Instance.manosJugadores.TryGetValue(myTotem.playerID, out ManoJugador manoIA))
		{
			// Animación al descarte (manteniendo el frente visible durante el movimiento)
			Vector3 posicionDescarte = MazoDescarte.Instance.transform.position;
			
			// Levantar un poco antes de mover al descarte
			LeanTween.moveLocal(cartaDiamante, cartaDiamante.transform.localPosition + Vector3.up * 0.5f, 0.3f)
				.setEase(LeanTweenType.easeOutBack);
			
			yield return new WaitForSeconds(0.3f);
			
			// Mover al descarte
			LeanTween.move(cartaDiamante, posicionDescarte, 1f)
				.setEase(LeanTweenType.easeInOutCubic);
			
			// Rotar a la posición de descarte (pero mantener el frente visible)
			LeanTween.rotate(cartaDiamante, new Vector3(90f, 180f, 0f), 0.5f);
			
			yield return new WaitForSeconds(1f);
			
			// Ahora sí, mostrar dorso en el descarte
			if (cartaScript != null)
			{
				cartaScript.MostrarDorso();
				// Rotar completamente al descarte
				cartaDiamante.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			}
			
			MazoDescarte.Instance.AgregarCartaDescarte(cartaDiamante);
			manoIA.RemoverCarta(cartaDiamante);
			
			Debug.Log("✅ Carta Diamante movida al descarte");
		}

		// Ocultar mensaje
		if (GestionBotonesCartas.Instance != null)
		{
			GestionBotonesCartas.Instance.OcultarMensaje();
		}

		yield return new WaitForSeconds(1f);

		// Desbloquear EndTurn y terminar turno
		if (GameManager.Instance != null)
		{
			GameManager.Instance.bloquearEndTurnAutomatico = false;
		}
		
		GameManager.Instance?.EndTurn();
	}

	/// <summary>
	/// Cancela el uso del Diamante en caso de error
	/// </summary>
	private void CancelarUsoDiamante(GameObject cartaDiamante)
	{
		Debug.Log("❌ IA cancelando uso de Diamante");

		// Ocultar mensaje
		if (GestionBotonesCartas.Instance != null)
		{
			GestionBotonesCartas.Instance.OcultarMensaje();
		}

		// Desactivar imanes visualmente (por si acaso)
		if (MagnetSystem.Instance != null)
		{
			MagnetSystem.Instance.DesactivarImanesColocacion();
			Debug.Log("🧲 Imanes desactivados visualmente por cancelación (IA)");
		}

		// Desactivar Diamante si se activó
		if (GestionBotonesCartas.Instance != null && 
			GestionBotonesCartas.Instance.hexagonoDiamantePrefab != null)
		{
			GestionBotonesCartas.Instance.hexagonoDiamantePrefab.SetActive(false);
		}

		// Desbloquear EndTurn
		if (GameManager.Instance != null)
		{
			GameManager.Instance.bloquearEndTurnAutomatico = false;
		}
		
		// Terminar turno
		GameManager.Instance?.EndTurn();
	}
}