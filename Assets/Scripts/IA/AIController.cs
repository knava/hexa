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
    /// Inicia el turno de la IA en fase de movimiento
    /// </summary>
    public void StartAITurn()
    {
        if (GameManager.Instance != null && GameManager.Instance.juegoTerminado) return;
        if (myTotem == null) return;
        
        isMyTurn = true;
        StartCoroutine(ProcessAITurn());
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
    /// Toma decisión de movimiento basada en el resultado del dado
    /// </summary>
    private void MakeDecision()
    {
        if (!isMyTurn) return;
        if (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta) return;
        
        HexagonPiece targetHex = ChooseBestHexagon();
        if (targetHex != null)
        {
            GameManager.Instance.SelectHexagon(targetHex);
        }
        else
        {
            GameManager.Instance.EndTurn();
        }
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

        HexagonPiece chosenHex = selectableHexagons[0];
        
        // Prioridad 1: Buscar hexágonos con enemigos para comer
        List<HexagonPiece> hexagonsWithEnemies = selectableHexagons
            .Where(hex => !hex.isMainPiece && HasEnemyTotem(hex))
            .ToList();

        if (hexagonsWithEnemies.Count > 0)
        {
            return hexagonsWithEnemies[0];
        }

        // Prioridad 2: Buscar cartas de robo
        foreach (var hex in selectableHexagons)
        {
            if (hex.isStealCardPiece)
            {
                chosenHex = hex;
                break;
            }
        }

        // Prioridad 3: Buscar hexágonos del mismo color
        if (chosenHex == selectableHexagons[0])
        {
            foreach (var hex in selectableHexagons)
            {
                if (hex.PieceColor == myTotem.playerColor)
                {
                    chosenHex = hex;
                    break;
                }
            }
        }

        return chosenHex;
    }

    /// <summary>
    /// Verifica si un hexágono tiene totems enemigos
    /// </summary>
    private bool HasEnemyTotem(HexagonPiece hex)
    {
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
}