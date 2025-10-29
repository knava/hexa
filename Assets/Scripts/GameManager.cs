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
    
    [Header("Referencias")]
    public ControlDado diceController;
    public HexagonalBoardGenerator boardGenerator;
    
    [Header("Estado del Juego")]
    public List<PlayerTotem> players = new List<PlayerTotem>();
    public int currentPlayerIndex = 0;
    public int diceResult = 0;
    public bool waitingForDiceRoll = false;
    
    [Header("Pathfinding")]
    public List<HexagonPiece> selectableHexagons = new List<HexagonPiece>();
    private Dictionary<HexagonPiece, List<HexagonPiece>> boardGraph = new Dictionary<HexagonPiece, List<HexagonPiece>>();
    private Dictionary<HexagonPiece, Dictionary<int, List<HexagonPiece>>> exactPaths = new Dictionary<HexagonPiece, Dictionary<int, List<HexagonPiece>>>();
    
    [Header("Configuración IA")]
    public bool enableAI = true;
    
    [Header("Referencias de Sistema")]
    public GameObject mazoGameObject;
    
    [Header("Control de Robo")]
    public bool esperandoRoboCarta = false;
    public PlayerTotem jugadorRobandoCarta = null;
    public bool esperandoRoboPorComer = false;
    public PlayerTotem jugadorQueComio = null;
    public PlayerTotem jugadorComido = null;
    
    [Header("Control de Flujo")]
    public bool bloquearEndTurnAutomatico = false;
    
    [Header("Fin del Juego")]
    public GameObject uiFinDelJuego;
    public string nombreEscenaMenu = "MenuPrincipal";
    
    [Header("Control de Estado")]
    public bool juegoTerminado = false;
    
    [Header("Control de Acciones por Turno")]
    public bool dadoTiradoEnEsteTurno = false;
	
	[Header("Sistema Diamante")]
	public int puntosDiamante = 20;

    // Variables privadas para gestión interna
    public Dictionary<string, HexagonPiece> unflippedHexagons = new Dictionary<string, HexagonPiece>();
    public GamePhase currentPhase;
    private bool isConstructionTurnActive = false;

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
        
        // Configurar UI según fase actual
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetPhaseUI(GamePhase.BoardConstruction);
            UIManager.Instance.ShowTemporaryMessage(UIManager.Instance.phase1Text, 2f);
        }
        
        InicializarManosJugadores();
        InicializarAvataresJugadores();
        StartPlayerMaker();
        DeshabilitarMazoDeCartas();
    }

    /// <summary>
    /// Inicializa los avatares de los jugadores
    /// </summary>
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

    /// <summary>
    /// Inicializa las manos de los jugadores
    /// </summary>
    private void InicializarManosJugadores()
    {
        if (MazoFisico.Instance != null)
        {
            int cantidadJugadores = players.Count;
            int cantidadIA = players.Count(p => p.GetComponent<AIController>() != null);
            MazoFisico.Instance.InicializarManosJugadores(cantidadJugadores, cantidadIA);
        }
    }

    /// <summary>
    /// Inicializa la lista de jugadores
    /// </summary>
    private void InitializePlayers()
    {
        players = FindObjectsByType<PlayerTotem>(FindObjectsSortMode.None).ToList();
        players = players.OrderBy(p => p.playerID).ToList();
    }

    /// <summary>
    /// Configura el controlador de dados
    /// </summary>
    private void SetupDiceController()
    {
        if (diceController != null)
        {
            diceController.OnDiceStopped += HandleDiceResult;
        }
    }

    /// <summary>
    /// Inicia el turno del jugador actual
    /// </summary>
    private void StartPlayerTurn()
    {
        if (juegoTerminado) return;
        
        if (currentPlayerIndex >= players.Count) return;

        PlayerTotem currentPlayer = players[currentPlayerIndex];
        AIController aiController = currentPlayer.GetComponent<AIController>();

        // Manejar turno de IA
        if (aiController != null && enableAI)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetDiceButtonVisibility(false);
            }
            
            if (!waitingForDiceRoll)
            {
                aiController.StartAITurn();
            }
        }
        else
        {
            // Habilitar botón de dado para jugador humano
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetDiceButtonVisibility(true);
            }
        }
    }

    /// <summary>
    /// Maneja la pulsación del botón de dado
    /// </summary>
    public void OnDiceButtonPressed()
    {
        if (juegoTerminado) return;
        
        if (diceController == null) return;
        
        if (!waitingForDiceRoll && players[currentPlayerIndex].GetComponent<AIController>() == null)
        {
            waitingForDiceRoll = true;
            dadoTiradoEnEsteTurno = true;
            
            // Desactivar botones de cartas al tirar dado
            if (GestionBotonesCartas.Instance != null)
            {
                GestionBotonesCartas.Instance.OnDiceActivated();
            }
            
            diceController.PrepararDado();
        }
    }

    /// <summary>
    /// Procesa el resultado del dado
    /// </summary>
    private void HandleDiceResult(int result)
    {
        diceResult = result;
        waitingForDiceRoll = false;
        
        // Reactivar botones de cartas al terminar dado
        if (players[currentPlayerIndex].GetComponent<AIController>() == null && 
            GestionBotonesCartas.Instance != null)
        {
            GestionBotonesCartas.Instance.OnDiceDeactivated();
        }
        
        ShowReachableHexagons(players[currentPlayerIndex].currentHexagon, diceResult);
    }

    /// <summary>
    /// Muestra los hexágonos alcanzables desde una posición
    /// </summary>
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

    /// <summary>
    /// Selecciona un hexágono para movimiento
    /// </summary>
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

    /// <summary>
    /// Obtiene el camino exacto hacia un destino
    /// </summary>
    public List<HexagonPiece> GetExactPath(HexagonPiece destination, int requiredSteps)
    {
        if (exactPaths.TryGetValue(destination, out var paths) && 
            paths.TryGetValue(requiredSteps, out var path))
        {
            return path;
        }
        return new List<HexagonPiece>();
    }

    /// <summary>
    /// Limpia la selección de hexágonos
    /// </summary>
    private void ClearSelection()
    {
        foreach (var hex in selectableHexagons)
        {
            hex.SetSelectable(false);
        }
        selectableHexagons.Clear();
    }

    /// <summary>
    /// Finaliza el turno actual
    /// </summary>
    public void EndTurn()
    {
        if (juegoTerminado) return;
        
        // Verificar si el cambio de turno está bloqueado
        if (bloquearEndTurnAutomatico || esperandoRoboPorComer || esperandoRoboCarta)
        {
            return;
        }
        
        // Resetear estado de acciones para el siguiente turno
        dadoTiradoEnEsteTurno = false;
        
        ClearSelection();
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        
        // Notificar cambio de turno al sistema de botones
        if (GestionBotonesCartas.Instance != null)
        {
            GestionBotonesCartas.Instance.ForzarActualizacionBoton();
        }
        
        StartPlayerTurn();
    }

    /// <summary>
    /// Registra una conexión entre dos piezas hexagonales
    /// </summary>
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

    /// <summary>
    /// Registra una pieza principal en el grafo del tablero
    /// </summary>
    public void RegisterMainPiece(HexagonPiece mainPiece)
    {
        if (!boardGraph.ContainsKey(mainPiece))
        {
            boardGraph[mainPiece] = new List<HexagonPiece>();
        }
        
        // Buscar piezas adyacentes
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

    /// <summary>
    /// Obtiene los vecinos de un hexágono
    /// </summary>
    public List<HexagonPiece> GetNeighbors(HexagonPiece hex)
    {
        return boardGraph.ContainsKey(hex) ? boardGraph[hex] : new List<HexagonPiece>();
    }

    /// <summary>
    /// Encuentra todos los caminos exactos desde un hexágono inicial
    /// </summary>
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

    /// <summary>
    /// Nodo para el algoritmo de pathfinding
    /// </summary>
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

    /// <summary>
    /// Fuerza el tiro de dado para IA
    /// </summary>
    public void ForceDiceRollForAI()
    {
        if (juegoTerminado) return;
        
        if (diceController == null) return;
        
        if (!waitingForDiceRoll)
        {
            waitingForDiceRoll = true;
            diceController.PrepararDado();
        }
    }

    /// <summary>
    /// Inicializa el diccionario de hexágonos no volteados
    /// </summary>
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

    /// <summary>
    /// Obtiene el número de un hexágono a partir de su nombre
    /// </summary>
    private int GetHexagonNumber(HexagonPiece piece)
    {
        string[] parts = piece.gameObject.name.Split('_');
        string numberStr = parts.Length > 1 ? parts[parts.Length - 1] : "0";
        int.TryParse(numberStr, out int number);
        return number;
    }

    /// <summary>
    /// Registra que un hexágono ha sido volteado
    /// </summary>
    public void RegisterHexagonFlip(HexagonPiece piece)
    {
        string pieceName = $"Hexágono{GetHexagonNumber(piece)}";
        if (unflippedHexagons.ContainsKey(pieceName))
        {
            unflippedHexagons.Remove(pieceName);
        }
    }

    /// <summary>
    /// Obtiene un hexágono no volteado aleatorio
    /// </summary>
    public HexagonPiece GetRandomUnflippedHexagon()
    {
        if (unflippedHexagons.Count == 0)
            return null;
            
        var randomEntry = unflippedHexagons.ElementAt(Random.Range(0, unflippedHexagons.Count));
        return randomEntry.Value;
    }

    /// <summary>
    /// Inicia el turno de construcción del jugador actual
    /// </summary>
    public void StartPlayerMaker()
    {
        if (juegoTerminado) return;
        
        if (currentPhase != GamePhase.BoardConstruction || isConstructionTurnActive) return;
        
        isConstructionTurnActive = true;
        PlayerTotem currentPlayer = players[currentPlayerIndex];
        AIController aiController = currentPlayer.GetComponent<AIController>();

        if (aiController != null && enableAI)
        {
            aiController.StartAIMaker();
        }
    }

    /// <summary>
    /// Finaliza el turno de construcción
    /// </summary>
    public void EndConstructionTurn()
    {
        if (!isConstructionTurnActive) return;
        
        MagnetSystem.Instance.ResetForNewTurn();
        ClearSelection();
        
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        isConstructionTurnActive = false;
        
        // Habilitar colliders de todos los hexágonos
        foreach (var hex in FindObjectsByType<HexagonPiece>(FindObjectsSortMode.None))
        {
            hex.SetCollidersEnabled(true);
        }
        
        // Verificar fin de fase de construcción
        if (unflippedHexagons.Count == 0)
        {
            CambiarAFaseMovimiento();
        }
        else
        {
            StartCoroutine(StartNextConstructionTurnAfterDelay(0.5f));
        }
    }

    /// <summary>
    /// Cambia a la fase de movimiento
    /// </summary>
    private void CambiarAFaseMovimiento()
    {
        currentPhase = GamePhase.TotemMovement;
        
        MagnetSystem.Instance.DisableAllMagnets();
        
        // Reposicionar jugadores en pieza principal
        foreach (var player in players)
        {
            player.ReturnToMainPiece();
        }
        
        HabilitarMazoDeCartas();
        
        // Mostrar avatares
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.MostrarAvatares();
        }
        
        StartCoroutine(ShowPhase2TextWithDelay());
        isConstructionTurnActive = false;
        currentPlayerIndex = (currentPlayerIndex) % players.Count;
        StartCoroutine(WaitToStart());
    }

    /// <summary>
    /// Muestra el texto de fase 2 con delay
    /// </summary>
    private IEnumerator ShowPhase2TextWithDelay()
    {
        yield return new WaitForSeconds(1f);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetPhaseUI(GamePhase.TotemMovement);
            UIManager.Instance.ShowTemporaryMessage(UIManager.Instance.phase2Text, 3f);
        }
    }

    /// <summary>
    /// Espera antes de iniciar el turno
    /// </summary>
    private IEnumerator WaitToStart()
    {
        yield return new WaitForSeconds(3f);
        StartPlayerTurn();
    }

    /// <summary>
    /// Inicia el siguiente turno de construcción con delay
    /// </summary>
    private IEnumerator StartNextConstructionTurnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartPlayerMaker();
    }

    /// <summary>
    /// Habilita el mazo de cartas
    /// </summary>
    private void HabilitarMazoDeCartas()
    {
        if (mazoGameObject != null)
        {
            mazoGameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Deshabilita el mazo de cartas
    /// </summary>
    private void DeshabilitarMazoDeCartas()
    {
        if (mazoGameObject != null)
        {
            mazoGameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Inicia el proceso de robo de carta
    /// </summary>
    public void IniciarRoboCarta(PlayerTotem jugador)
    {
        esperandoRoboCarta = true;
        jugadorRobandoCarta = jugador;
        
        // Deshabilitar interacciones durante el robo
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetDiceButtonVisibility(false);
        }
    }

    /// <summary>
    /// Finaliza el proceso de robo de carta
    /// </summary>
    public void FinalizarRoboCarta()
    {
        esperandoRoboCarta = false;
        jugadorRobandoCarta = null;
        
        if (!bloquearEndTurnAutomatico)
        {
            EndTurn();
        }
    }

    /// <summary>
    /// Verifica si el jugador actual es IA
    /// </summary>
    public bool IsCurrentPlayerAI()
    {
        if (currentPlayerIndex >= players.Count) return false;
        return players[currentPlayerIndex].GetComponent<AIController>() != null;
    }

    /// <summary>
    /// Activa el robo por comer entre jugadores
    /// </summary>
    public void ActivarRoboPorComer(PlayerTotem atacante, PlayerTotem victima)
    {
        if (atacante == null || victima == null)
        {
            EndTurn();
            return;
        }
        
        esperandoRoboPorComer = true;
        jugadorQueComio = atacante;
        jugadorComido = victima;
        
        if (MazoFisico.Instance != null)
        {
            MazoFisico.Instance.HabilitarRoboPorComer(atacante.playerID, victima.playerID);
        }
        else
        {
            FinalizarRoboPorComer();
        }
    }

    /// <summary>
    /// Finaliza el robo por comer
    /// </summary>
    public void FinalizarRoboPorComer()
    {
        esperandoRoboPorComer = false;
        jugadorQueComio = null;
        jugadorComido = null;
        
        if (!bloquearEndTurnAutomatico)
        {
            EndTurn();
        }
    }

    /// <summary>
    /// Registra un jugador comido
    /// </summary>
    public void RegistrarJugadorComido(PlayerTotem victima)
    {
        jugadorComido = victima;
    }

    /// <summary>
    /// Activa robo por comer directo (sin casilla especial)
    /// </summary>
    public void ActivarRoboPorComerDirecto(PlayerTotem atacante, PlayerTotem victima)
    {
        if (atacante == null || victima == null)
        {
            EndTurn();
            return;
        }
        
        esperandoRoboPorComer = true;
        jugadorQueComio = atacante;
        jugadorComido = victima;
        
        if (MazoFisico.Instance != null)
        {
            MazoFisico.Instance.HabilitarRoboPorComer(atacante.playerID, victima.playerID);
        }
        else
        {
            FinalizarRoboPorComer();
        }
    }

    /// <summary>
    /// Finaliza el juego
    /// </summary>
    public void FinDelJuego()
    {
        if (juegoTerminado) return;
        
        juegoTerminado = true;
        enabled = false;
        DeshabilitarSistemasDelJuego();
        
        StartCoroutine(SecuenciaFinDelJuego());
    }

    /// <summary>
    /// Secuencia de fin del juego
    /// </summary>
    private IEnumerator SecuenciaFinDelJuego()
    {
        // Mostrar todas las cartas de los jugadores
        yield return StartCoroutine(MostrarTodasLasCartas());

        // Esperar para que los jugadores vean las cartas
        yield return new WaitForSeconds(3f);

        // Mostrar ranking de puntuaciones
        MostrarRankingPuntuaciones();
    }

    /// <summary>
    /// Muestra el ranking de puntuaciones
    /// </summary>
    private void MostrarRankingPuntuaciones()
    {
        FinDelJuegoUI finDelJuegoUI = uiFinDelJuego?.GetComponent<FinDelJuegoUI>();
        if (finDelJuegoUI != null)
        {
            finDelJuegoUI.MostrarRanking(players);
        }
    }

    /// <summary>
    /// Calcula la puntuación de un jugador
    /// </summary>
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
                        puntuacion += 1;
                    }
                }
            }
        }
        
        return puntuacion;
    }

    /// <summary>
    /// Muestra todas las cartas de los jugadores
    /// </summary>
    private IEnumerator MostrarTodasLasCartas()
    {
        List<Coroutine> corrutinas = new List<Coroutine>();
        
        // Voltear cartas de todos los jugadores
        foreach (var player in players)
        {
            if (MazoFisico.Instance != null && 
                MazoFisico.Instance.manosJugadores.TryGetValue(player.playerID, out ManoJugador mano))
            {
                Coroutine corrutina = StartCoroutine(VoltearCartasDeJugador(mano));
                corrutinas.Add(corrutina);
            }
        }
        
        // Esperar a que todas las corrutinas terminen
        foreach (var corrutina in corrutinas)
        {
            yield return corrutina;
        }
    }

    /// <summary>
    /// Voltea las cartas de un jugador específico
    /// </summary>
    private IEnumerator VoltearCartasDeJugador(ManoJugador mano)
    {
        var cartas = mano.GetCartas();
        List<Coroutine> corrutinas = new List<Coroutine>();
        
        // Voltear cada carta con delay entre ellas
        for (int i = 0; i < cartas.Count; i++)
        {
            GameObject carta = cartas[i];
            if (carta != null)
            {
                yield return new WaitForSeconds(0.3f);
                
                Coroutine corrutina = StartCoroutine(VoltearCartaIndividual(carta));
                corrutinas.Add(corrutina);
            }
        }
        
        // Esperar a que todas las cartas terminen de voltearse
        foreach (var corrutina in corrutinas)
        {
            yield return corrutina;
        }
    }

    /// <summary>
    /// Voltea una carta individual con efecto
    /// </summary>
    private IEnumerator VoltearCartaIndividual(GameObject carta)
    {
        Carta3D cartaScript = carta.GetComponent<Carta3D>();
        if (cartaScript != null)
        {
            cartaScript.MostrarFrente();
            yield return StartCoroutine(EfectoVolteoCarta(carta));
        }
    }

    /// <summary>
    /// Efecto visual de volteo de carta
    /// </summary>
    private IEnumerator EfectoVolteoCarta(GameObject carta)
    {
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

    /// <summary>
    /// Deshabilita todos los sistemas del juego
    /// </summary>
    private void DeshabilitarSistemasDelJuego()
    {
        // Deshabilitar dado
        if (diceController != null)
        {
            diceController.enabled = false;
            diceController.StopAllCoroutines();
        }
        
        // Deshabilitar jugadores
        foreach (var player in players)
        {
            player.enabled = false;
            player.StopAllCoroutines();
            
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
        
        // Deshabilitar UIManager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.enabled = false;
        }
    }

    /// <summary>
    /// Sale al menú principal
    /// </summary>
    public void SalirAlMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(nombreEscenaMenu);
    }

    /// <summary>
    /// Cancela el tiro de dado
    /// </summary>
    public void CancelarDado()
    {
        if (waitingForDiceRoll)
        {
            waitingForDiceRoll = false;
            
            // Reactivar botones de cartas al cancelar dado
            if (players[currentPlayerIndex].GetComponent<AIController>() == null && 
                GestionBotonesCartas.Instance != null)
            {
                GestionBotonesCartas.Instance.OnDiceDeactivated();
            }
        }
    }
}