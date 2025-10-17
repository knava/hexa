using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public enum GamePhase
{
    BoardConstruction,
    TotemMovement
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    [Header("References")]
    public ControlDado diceController;
    public HexagonalBoardGenerator boardGenerator; // Mantener por posible uso externo
    
    [Header("Game State")]
    public List<PlayerTotem> players = new List<PlayerTotem>();
    public int currentPlayerIndex = 0;
    public int diceResult = 0;
    public bool waitingForDiceRoll = false;
    
    [Header("Pathfinding")]
    public List<HexagonPiece> selectableHexagons = new List<HexagonPiece>();
    private Dictionary<HexagonPiece, List<HexagonPiece>> boardGraph = new Dictionary<HexagonPiece, List<HexagonPiece>>();
    private Dictionary<HexagonPiece, Dictionary<int, List<HexagonPiece>>> exactPaths = new Dictionary<HexagonPiece, Dictionary<int, List<HexagonPiece>>>();
    
    [Header("AI Settings")]
    public bool enableAI = true;
	
	[Header("Referencias")]
	public GameObject mazoGameObject;
    
    public Dictionary<string, HexagonPiece> unflippedHexagons = new Dictionary<string, HexagonPiece>();
    public GamePhase currentPhase;
    private bool isConstructionTurnActive = false;
	
	[Header("Control de Robo")]
	public bool esperandoRoboCarta = false;
	public PlayerTotem jugadorRobandoCarta = null;
	public bool esperandoRoboPorComer = false;
	public PlayerTotem jugadorQueComio = null;
	public PlayerTotem jugadorComido = null;
	
	[Header("Caso Especial - Comer en Casilla Robo")]
	public bool enCasoEspecialComerYRobar = false;
	
	[Header("Control de Flujo")]
	public bool bloquearEndTurnAutomatico = false;
	
	[Header("Fin del Juego")]
	public GameObject uiFinDelJuego;
	public string nombreEscenaMenu = "MenuPrincipal";
	
	
	[Header("Control de Estado")]
	public bool juegoTerminado = false;

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
        InitializePlayers();
        SetupDiceController();
        currentPhase = GamePhase.BoardConstruction;
        InitializeUnflippedHexagons();
		UIManager.Instance?.SetPhaseUI(GamePhase.BoardConstruction);
        UIManager.Instance?.ShowTemporaryMessage(UIManager.Instance.phase1Text, 2f);
		InicializarManosJugadores();
		InicializarAvataresJugadores();
        StartPlayerMaker();
		DeshabilitarMazoDeCartas();
    }
	
	private void InicializarAvataresJugadores()
	{
		if (SistemaAvataresJugadores.Instance != null)
		{
			SistemaAvataresJugadores.Instance.InicializarAvatares(players.Count, players);
			
			// Ocultar avatares durante la fase de construcción
			if (currentPhase == GamePhase.BoardConstruction)
			{
				SistemaAvataresJugadores.Instance.OcultarAvatares();
			}
		}
	}

	private void InicializarManosJugadores()
	{
		if (MazoFisico.Instance != null)
		{
			int cantidadJugadores = players.Count;
			int cantidadIA = players.Count(p => p.GetComponent<AIController>() != null);
			MazoFisico.Instance.InicializarManosJugadores(cantidadJugadores, cantidadIA);
		}
	}

    private void InitializePlayers()
    {
        players = FindObjectsByType<PlayerTotem>(FindObjectsSortMode.None).ToList();
        players = players.OrderBy(p => p.playerID).ToList();
        if (players.Count == 0)
        {
            Debug.LogError("¡No se encontraron jugadores en la escena!");
        }
    }

    private void SetupDiceController()
    {
        if (diceController != null)
        {
            diceController.OnDiceStopped += HandleDiceResult;
        }
        else
        {
            Debug.LogError("diceController no está asignado en GameManager.");
        }
    }

    private void StartPlayerTurn()
	{
		if (juegoTerminado) return;
		
		if (currentPlayerIndex >= players.Count) return;

		PlayerTotem currentPlayer = players[currentPlayerIndex];
		AIController aiController = currentPlayer.GetComponent<AIController>();

		if (aiController != null && enableAI)
		{
			UIManager.Instance?.SetDiceButtonVisibility(false);
			if (!waitingForDiceRoll)
			{
				// La IA ahora evaluará cartas de acción primero
				aiController.StartAITurn();
			}
		}
		else
		{
			if (currentPlayer.GetComponent<AIController>() == null)
			{
				UIManager.Instance?.SetDiceButtonVisibility(true);
			}
		}
	}

    public void OnDiceButtonPressed()
	{
		if (juegoTerminado) return; // ✅ Agregar esta línea
		
		if (diceController == null)
		{
			Debug.LogError("No se puede tirar el dado: diceController no está asignado.");
			return;
		}
		if (!waitingForDiceRoll && players[currentPlayerIndex].GetComponent<AIController>() == null)
		{
			waitingForDiceRoll = true;
			diceController.PrepararDado();
		}
	}

    private void HandleDiceResult(int result)
    {
        diceResult = result;
        waitingForDiceRoll = false;
        ShowReachableHexagons(players[currentPlayerIndex].currentHexagon, diceResult);
    }

    public void ShowReachableHexagons(HexagonPiece startHex, int steps)
    {
        ClearSelection();
        FindAllExactPaths(startHex, steps);
        
        selectableHexagons = exactPaths.Keys
            .Where(hex => exactPaths[hex].ContainsKey(steps))
            .ToList();
        
        foreach (var hex in selectableHexagons)
        {
            hex.SetSelectable(true);
        }
    }

    public void SelectHexagon(HexagonPiece hex)
    {
        if (selectableHexagons.Contains(hex))
        {
            PlayerTotem currentPlayer = players[currentPlayerIndex];
            List<HexagonPiece> path = GetExactPath(hex, diceResult);
            
            if (path.Count > 0)
            {
                currentPlayer.MoveAlongPath(path);
                ClearSelection();
            }
        }
    }

    public List<HexagonPiece> GetExactPath(HexagonPiece destination, int requiredSteps)
    {
        if (exactPaths.TryGetValue(destination, out var paths) && 
            paths.TryGetValue(requiredSteps, out var path))
        {
            return path;
        }
        return new List<HexagonPiece>();
    }

    private void ClearSelection()
    {
        foreach (var hex in selectableHexagons)
        {
            hex.SetSelectable(false);
        }
        selectableHexagons.Clear();
    }

    public void EndTurn()
	{
		if (juegoTerminado)
		{
			Debug.Log("⏹️ Juego terminado, no se puede cambiar turno");
			return;
		}
		
		// ✅ VERIFICACIÓN CRÍTICA: Si está bloqueado, no hacer nada
		if (bloquearEndTurnAutomatico)
		{
			Debug.Log("⏸️ EndTurn bloqueado temporalmente - carta de acción en progreso");
			return;
		}
		
		// Si estamos en medio de un robo por comer, no hacer nada
		if (esperandoRoboPorComer)
		{
			Debug.Log("⏳ Turno en pausa - Esperando a que termine el robo por comer");
			return;
		}
		
		if (esperandoRoboCarta)
		{
			Debug.Log("⏳ Turno en pausa - Esperando a que el jugador robe una carta");
			return;
		}
		
		Debug.Log("🔄 Finalizando turno normal...");
		ClearSelection();
		currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
		
		// NUEVO: Notificar cambio de turno al sistema de botones
		if (GestionBotonesCartas.Instance != null)
		{
			GestionBotonesCartas.Instance.ForzarActualizacionBoton();
		}
		
		StartPlayerTurn();
	}

    public void RegisterConnection(HexagonPiece piece1, HexagonPiece piece2)
    {
        if (piece1 == piece2) return;

        if (!boardGraph.ContainsKey(piece1))
            boardGraph[piece1] = new List<HexagonPiece>();
        
        if (!boardGraph.ContainsKey(piece2))
            boardGraph[piece2] = new List<HexagonPiece>();

        if (!boardGraph[piece1].Contains(piece2))
            boardGraph[piece1].Add(piece2);

        if (!boardGraph[piece2].Contains(piece1))
            boardGraph[piece2].Add(piece1);
    }

    public void RegisterMainPiece(HexagonPiece mainPiece)
    {
        if (!boardGraph.ContainsKey(mainPiece))
        {
            boardGraph[mainPiece] = new List<HexagonPiece>();
        }
        
        Collider[] hitColliders = Physics.OverlapSphere(mainPiece.transform.position, 1.7f);
        foreach (var hit in hitColliders)
        {
            HexagonPiece piece = hit.GetComponent<HexagonPiece>();
            if (piece != null && piece != mainPiece)
            {
                RegisterConnection(mainPiece, piece);
            }
        }
    }

    public List<HexagonPiece> GetNeighbors(HexagonPiece hex)
    {
        return boardGraph.ContainsKey(hex) ? boardGraph[hex] : new List<HexagonPiece>();
    }

    public void FindAllExactPaths(HexagonPiece startHex, int maxSteps)
    {
        exactPaths.Clear();
        PlayerTotem currentPlayer = players[currentPlayerIndex];
        Color playerColor = currentPlayer.playerColor;
        Queue<PathNode> queue = new Queue<PathNode>();
        queue.Enqueue(new PathNode(startHex, 0, new List<HexagonPiece>{startHex}));
        List<HexagonPiece> tempPath = new List<HexagonPiece>();

        while (queue.Count > 0)
        {
            PathNode current = queue.Dequeue();
            if (current.steps >= maxSteps) continue;

            foreach (HexagonPiece neighbor in GetNeighbors(current.hex))
            {
                if (!current.path.Contains(neighbor) && neighbor.PieceColor != playerColor)
                {
                    tempPath.Clear();
                    tempPath.AddRange(current.path);
                    tempPath.Add(neighbor);

                    if (!exactPaths.ContainsKey(neighbor))
                        exactPaths[neighbor] = new Dictionary<int, List<HexagonPiece>>();

                    if (!exactPaths[neighbor].ContainsKey(current.steps + 1))
                        exactPaths[neighbor][current.steps + 1] = new List<HexagonPiece>(tempPath);

                    queue.Enqueue(new PathNode(neighbor, current.steps + 1, new List<HexagonPiece>(tempPath)));
                }
            }
        }
    }

    private class PathNode
    {
        public HexagonPiece hex;
        public int steps;
        public List<HexagonPiece> path;

        public PathNode(HexagonPiece hex, int steps, List<HexagonPiece> path)
        {
            this.hex = hex;
            this.steps = steps;
            this.path = path;
        }
    }

    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (exactPaths != null)
        {
            foreach (var dest in exactPaths)
            {
                foreach (var path in dest.Value)
                {
                    Gizmos.color = Color.Lerp(Color.green, Color.red, path.Key / 6f);
                    for (int i = 1; i < path.Value.Count; i++)
                    {
                        Gizmos.DrawLine(
                            path.Value[i-1].transform.position + Vector3.up * 0.1f,
                            path.Value[i].transform.position + Vector3.up * 0.1f
                        );
                    }
                }
            }
        }
    }
    #endif

    public void ForceDiceRollForAI()
	{
		if (juegoTerminado) return; // ✅ Agregar esta línea
		
		if (diceController == null)
		{
			Debug.LogError("No se puede tirar el dado para IA: diceController no está asignado.");
			return;
		}
		if (!waitingForDiceRoll)
		{
			waitingForDiceRoll = true;
			diceController.PrepararDado();
		}
	}

    private void InitializeUnflippedHexagons()
    {
        unflippedHexagons.Clear();
        HexagonPiece[] allPieces = FindObjectsByType<HexagonPiece>(FindObjectsSortMode.None);
        
        foreach (HexagonPiece piece in allPieces)
        {
            if (!piece.isMainPiece && !piece.isFlipped)
            {
                string pieceName = $"Hexágono{GetHexagonNumber(piece)}";
                unflippedHexagons[pieceName] = piece;
            }
        }
    }

    private int GetHexagonNumber(HexagonPiece piece)
    {
        string[] parts = piece.gameObject.name.Split('_');
        string numberStr = parts.Length > 1 ? parts[parts.Length - 1] : "0";
        int.TryParse(numberStr, out int number);
        return number;
    }

    public void RegisterHexagonFlip(HexagonPiece piece)
    {
        string pieceName = $"Hexágono{GetHexagonNumber(piece)}";
        if (unflippedHexagons.ContainsKey(pieceName))
        {
            unflippedHexagons.Remove(pieceName);
        }
    }

    #if UNITY_EDITOR
    public void LogUnflippedHexagons()
    {
        Debug.Log("=== HEXÁGONOS NO VOLTEADOS ===");
        
        if (unflippedHexagons.Count == 0)
        {
            Debug.Log("Todos los hexágonos han sido volteados");
        }
        else
        {
            foreach (var entry in unflippedHexagons)
            {
                Debug.Log($"{entry.Key}");
            }
        }
        
        Debug.Log("==============================");
    }

    public void LogRandomUnflippedHexagons()
    {
        HexagonPiece randomHex = GetRandomUnflippedHexagon();
        if (randomHex != null)
        {
            Debug.Log($"Hexágono seleccionado: {randomHex.gameObject.name}");
        }
    }
    #endif

    public HexagonPiece GetRandomUnflippedHexagon()
    {
        if (unflippedHexagons.Count == 0)
            return null;
            
        var randomEntry = unflippedHexagons.ElementAt(Random.Range(0, unflippedHexagons.Count));
        return randomEntry.Value;
    }

    public void StartPlayerMaker()
	{
		if (juegoTerminado) return; // ✅ Agregar esta línea
		
		if (currentPhase != GamePhase.BoardConstruction || isConstructionTurnActive) return;
		
		isConstructionTurnActive = true;
		PlayerTotem currentPlayer = players[currentPlayerIndex];
		AIController aiController = currentPlayer.GetComponent<AIController>();

		if (aiController != null && enableAI)
		{
			Debug.Log($"Turno de IA (Jugador {currentPlayer.playerID}) para construcción");
			aiController.StartAIMaker();
		}
		else
		{
			Debug.Log($"Turno del jugador humano (Jugador {currentPlayer.playerID})");
		}
	}

    public void EndConstructionTurn()
	{
		if (!isConstructionTurnActive) return;
		
		//Debug.Log($"Finalizando turno de construcción del jugador {currentPlayerIndex + 1}");
		
		MagnetSystem.Instance.ResetForNewTurn();
		ClearSelection();
		
		currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
		isConstructionTurnActive = false;
		
		foreach (var hex in FindObjectsByType<HexagonPiece>(FindObjectsSortMode.None))
		{
			hex.SetCollidersEnabled(true);
		}
		
		// Nuevo: Chequeo de fin de fase después de la conexión
		if (unflippedHexagons.Count == 0)
		{
			currentPhase = GamePhase.TotemMovement;
			Debug.Log("¡TODOS LOS HEXÁGONOS VOLTEADOS Y COLOCADOS! Cambiando a fase de movimiento.");
			
			MagnetSystem.Instance.DisableAllMagnets();
			
			foreach (var player in players)
			{
				player.ReturnToMainPiece();
			}
			HabilitarMazoDeCartas();
			
			if (SistemaAvataresJugadores.Instance != null)
			{
				SistemaAvataresJugadores.Instance.MostrarAvatares();
			}
			
			StartCoroutine(ShowPhase2TextWithDelay());  // Nueva corrutina para retrasar el texto
			isConstructionTurnActive = false;
			currentPlayerIndex = (currentPlayerIndex) % players.Count;
			StartCoroutine(WaitToStart());
            
		}
		else if (unflippedHexagons.Count > 0)
		{
			StartCoroutine(StartNextConstructionTurnAfterDelay(0.5f));
		}
	}
	
	private IEnumerator ShowPhase2TextWithDelay()
	{
		yield return new WaitForSeconds(1f);  // Pausa de 2 segundos antes de mostrar el texto
		UIManager.Instance?.SetPhaseUI(GamePhase.TotemMovement);
		UIManager.Instance?.ShowTemporaryMessage(UIManager.Instance.phase2Text, 3f);  // Muestra el texto por 3 segundos
	}
	
	private IEnumerator WaitToStart()
	{
		yield return new WaitForSeconds(3f);  // Pausa de 2 segundos antes de mostrar el texto
		StartPlayerTurn();
	}

    private IEnumerator StartNextConstructionTurnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartPlayerMaker();
    }
	private void HabilitarMazoDeCartas()
	{
		if (mazoGameObject != null)
		{
			mazoGameObject.SetActive(true);
			Debug.Log("⚡ MAZO HABILITADO");
		}
	}

	private void DeshabilitarMazoDeCartas()
	{
		if (mazoGameObject != null)
		{
			mazoGameObject.SetActive(false);
			Debug.Log("⚡ MAZO DESHABILITADO");
		}
	}
	
	public void IniciarRoboCarta(PlayerTotem jugador)
	{
		esperandoRoboCarta = true;
		jugadorRobandoCarta = jugador;
		Debug.Log($"🎲 Jugador {jugador.playerID} debe robar una carta - Turno en pausa");
		
		// Deshabilitar el dado u otras interacciones durante el robo
		UIManager.Instance?.SetDiceButtonVisibility(false);
	}
	
	public void FinalizarRoboCarta()
	{
		esperandoRoboCarta = false;
		jugadorRobandoCarta = null;
		//Debug.Log("✅ Robo por casilla completado");
		
		// ✅ NO llamar EndTurn() aquí - será manejado por PlayerTotem en el caso especial
		if (!bloquearEndTurnAutomatico)
		{
			EndTurn();
		}
	}
	
	public bool IsCurrentPlayerAI()
	{
		if (currentPlayerIndex >= players.Count) return false;
		return players[currentPlayerIndex].GetComponent<AIController>() != null;
	}
	public void ActivarRoboPorComer(PlayerTotem atacante, PlayerTotem victima)
	{
		if (atacante == null || victima == null)
		{
			//Debug.LogError("❌ Referencias nulas en ActivarRoboPorComer");
			EndTurn();
			return;
		}
		
		esperandoRoboPorComer = true;
		jugadorQueComio = atacante;
		jugadorComido = victima;
		
		//Debug.Log($"🎯 Jugador {atacante.playerID} puede robar carta del mazo o del jugador {victima.playerID}");
		
		// Verificar que MazoFisico existe
		if (MazoFisico.Instance != null)
		{
			MazoFisico.Instance.HabilitarRoboPorComer(atacante.playerID, victima.playerID);
		}
		else
		{
			//Debug.LogError("❌ MazoFisico.Instance es null");
			FinalizarRoboPorComer();
		}
	}

	// Nuevo método para finalizar robo por comer
	public void FinalizarRoboPorComer()
	{
		Debug.Log("🔄 Finalizando robo por comer...");
		
		esperandoRoboPorComer = false;
		jugadorQueComio = null;
		jugadorComido = null;
		
		// ✅ NO llamar EndTurn() aquí - será manejado por PlayerTotem en el caso especial
		Debug.Log("✅ Robo por comer completado");
		
		// Solo llamar EndTurn si NO estamos en el caso especial
		if (!bloquearEndTurnAutomatico)
		{
			EndTurn();
		}
	}
	
	public void RegistrarJugadorComido(PlayerTotem victima)
	{
		jugadorComido = victima;
		//Debug.Log($"🎯 Registrado jugador comido: {victima.playerID}");
	}
	
	[ContextMenu("Debug Estado Robo Por Comer")]
	
	public void ActivarRoboPorComerDirecto(PlayerTotem atacante, PlayerTotem victima)
	{
		if (atacante == null || victima == null)
		{
			//Debug.LogError("❌ Referencias nulas en ActivarRoboPorComerDirecto");
			EndTurn();
			return;
		}
		
		// ✅ IMPORTANTE: Saltarnos completamente el sistema de casilla de robo
		esperandoRoboPorComer = true;
		jugadorQueComio = atacante;
		jugadorComido = victima;
		
		//Debug.Log($"🎯 ACTIVANDO ROBO DIRECTO: Jugador {atacante.playerID} puede robar del mazo o de {victima.playerID}");
		
		if (atacante.GetComponent<AIController>() != null)
		{
			//Debug.Log($"🤖 IA {atacante.playerID} decidirá automáticamente");
		}
		// Llamar directamente al mazo para habilitar robo por comer
		if (MazoFisico.Instance != null)
		{
			MazoFisico.Instance.HabilitarRoboPorComer(atacante.playerID, victima.playerID);
		}
		else
		{
			//Debug.LogError("❌ MazoFisico.Instance es null");
			FinalizarRoboPorComer();
		}
	}
	
	public void FinDelJuego()
	{
		if (juegoTerminado) return;
		
		juegoTerminado = true;
		Debug.Log("🎮 FIN DEL JUEGO - Mostrando cartas antes del fin...");
		
		// Deshabilitar interacciones del juego inmediatamente
		enabled = false;
		DeshabilitarSistemasDelJuego();
		
		// ✅ Iniciar secuencia de mostrar cartas
		StartCoroutine(SecuenciaFinDelJuego());
	}
	
	private IEnumerator SecuenciaFinDelJuego()
		{
		Debug.Log("🃏 Iniciando secuencia de fin del juego...");

		// 1. Mostrar todas las cartas de los jugadores
		yield return StartCoroutine(MostrarTodasLasCartas());

		// 2. Esperar 3 segundos para que los jugadores vean las cartas
		Debug.Log("⏰ Esperando 3 segundos para mostrar cartas...");
		yield return new WaitForSeconds(3f);

		// 3. Mostrar el ranking de puntuaciones (ya incluye el botón de salir)
		MostrarRankingPuntuaciones();
	}
	
	private void MostrarRankingPuntuaciones()
	{
		Debug.Log("🏆 Mostrando ranking de puntuaciones...");
		
		FinDelJuegoUI finDelJuegoUI = uiFinDelJuego?.GetComponent<FinDelJuegoUI>();
		if (finDelJuegoUI != null)
		{
			finDelJuegoUI.MostrarRanking(players);
		}
		else
		{
			Debug.LogWarning("⚠️ Componente FinDelJuegoUI no encontrado");
			// Fallback: mostrar en consola
			MostrarRankingEnConsola();
		}
	}
	
	private void MostrarRankingEnConsola()
	{
		Debug.Log("🏆 RANKING FINAL:");
		
		var jugadoresConPuntuacion = new List<JugadorPuntuacion>();
		
		foreach (PlayerTotem jugador in players)
		{
			int puntuacion = CalcularPuntuacionJugador(jugador.playerID);
			jugadoresConPuntuacion.Add(new JugadorPuntuacion(jugador.playerID, puntuacion, jugador.playerColor));
		}
		
		// Ordenar por puntuación descendente
		jugadoresConPuntuacion = jugadoresConPuntuacion.OrderByDescending(j => j.puntuacion).ToList();
		
		for (int i = 0; i < jugadoresConPuntuacion.Count; i++)
		{
			Debug.Log($"{i + 1}º - Jugador {jugadoresConPuntuacion[i].playerID}: {jugadoresConPuntuacion[i].puntuacion} puntos");
		}
	}

	// Método para calcular puntuación individual
	private int CalcularPuntuacionJugador(int playerID)
	{
		int puntuacion = 0;
		
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
						puntuacion += 1; // Oro vale 1 punto
					}
					// Piedra vale 0 puntos
				}
			}
		}
		
		return puntuacion;
	}
	
	private IEnumerator MostrarTodasLasCartas()
	{
		Debug.Log("🎴 Volteando todas las cartas de los jugadores...");
		
		List<Coroutine> corrutinas = new List<Coroutine>();
		
		// Voltear cartas de todos los jugadores
		foreach (var player in players)
		{
			// Obtener la mano del jugador
			if (MazoFisico.Instance != null && 
				MazoFisico.Instance.manosJugadores.TryGetValue(player.playerID, out ManoJugador mano))
			{
				// Iniciar corrutina para voltear cartas de este jugador
				Coroutine corrutina = StartCoroutine(VoltearCartasDeJugador(mano));
				corrutinas.Add(corrutina);
			}
		}
		
		// Esperar a que todas las corrutinas terminen
		foreach (var corrutina in corrutinas)
		{
			yield return corrutina;
		}
		
		Debug.Log("✅ Todas las cartas han sido volteadas");
	}
	
	private IEnumerator VoltearCartasDeJugador(ManoJugador mano)
	{
		Debug.Log($"🃏 Volteando cartas del jugador {mano.playerID}...");
		
		// Obtener todas las cartas de la mano
		var cartas = mano.GetCartas(); // Necesitaremos agregar este método
		
		List<Coroutine> corrutinas = new List<Coroutine>();
		
		// Voltear cada carta con un pequeño delay entre ellas
		for (int i = 0; i < cartas.Count; i++)
		{
			GameObject carta = cartas[i];
			if (carta != null)
			{
				// Pequeño delay entre cartas para efecto cascada
				yield return new WaitForSeconds(0.3f);
				
				Coroutine corrutina = StartCoroutine(VoltearCartaIndividual(carta));
				corrutinas.Add(corrutina);
			}
		}
		
		// Esperar a que todas las cartas de este jugador terminen de voltearse
		foreach (var corrutina in corrutinas)
		{
			yield return corrutina;
		}
		
		Debug.Log($"✅ Jugador {mano.playerID} - {cartas.Count} cartas volteadas");
	}
	
	private IEnumerator VoltearCartaIndividual(GameObject carta)
	{
		Carta3D cartaScript = carta.GetComponent<Carta3D>();
		if (cartaScript != null)
		{
			// Mostrar el frente de la carta
			cartaScript.MostrarFrente();
			
			// Efecto visual de volteo (opcional)
			yield return StartCoroutine(EfectoVolteoCarta(carta));
		}
	}
	
	private IEnumerator EfectoVolteoCarta(GameObject carta)
	{
		// Efecto de escala para simular volteo
		Vector3 escalaOriginal = carta.transform.localScale;
		Vector3 escalaVolteo = new Vector3(escalaOriginal.x * 1.2f, escalaOriginal.y, escalaOriginal.z);
		
		// Escalar hacia arriba
		LeanTween.scale(carta, escalaVolteo, 0.2f)
			.setEase(LeanTweenType.easeOutBack);
		
		yield return new WaitForSeconds(0.2f);
		
		// Volver a escala normal
		LeanTween.scale(carta, escalaOriginal, 0.2f)
			.setEase(LeanTweenType.easeInBack);
		
		yield return new WaitForSeconds(0.2f);
	}
	
	private void DeshabilitarSistemasDelJuego()
	{
		// Deshabilitar el dado
		if (diceController != null)
		{
			diceController.enabled = false;
			diceController.StopAllCoroutines();
		}
		
		// Deshabilitar interacción con jugadores
		foreach (var player in players)
		{
			player.enabled = false;
			player.StopAllCoroutines();
			
			// Deshabilitar AIController si existe
			AIController ai = player.GetComponent<AIController>();
			if (ai != null)
			{
				ai.enabled = false;
				ai.StopAllCoroutines();
			}
		}
		
		// Deshabilitar sistema de imanes
		if (MagnetSystem.Instance != null)
		{
			MagnetSystem.Instance.enabled = false;
		}
		
		// Deshabilitar hexágonos
		HexagonPiece[] allHexagons = FindObjectsByType<HexagonPiece>(FindObjectsSortMode.None);
		foreach (var hex in allHexagons)
		{
			hex.enabled = false;
			hex.StopAllCoroutines();
		}
		
		// Deshabilitar UIManager si existe
		if (UIManager.Instance != null)
		{
			UIManager.Instance.enabled = false;
		}
	}

	// Método para el botón de salir
	public void SalirAlMenu()
	{
		Debug.Log("🚪 Saliendo al menú principal...");
		
		// Cargar la escena del menú
		UnityEngine.SceneManagement.SceneManager.LoadScene(nombreEscenaMenu);
	}
}