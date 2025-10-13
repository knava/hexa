using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AIController : MonoBehaviour
{
    private PlayerTotem playerTotem;
	private PlayerTotem myTotem; // Referencia al totem que controla esta IA
    private bool isMyTurn = false;
	private PlayerTotem[] cachedAllTotems;

    void Awake()
    {
        myTotem = GetComponent<PlayerTotem>();
        if (myTotem == null)
        {
            Debug.LogError("AIController necesita un PlayerTotem en el mismo GameObject!");
        }
    }
	
	void Start()
    {
        // Inicializar la cache de totems
        cachedAllTotems = FindObjectsByType<PlayerTotem>(FindObjectsSortMode.None);
    }

    public void StartAITurn()
	{
		if (GameManager.Instance != null && GameManager.Instance.juegoTerminado)
		{
			Debug.Log("⏹️ Juego terminado, IA no puede jugar");
			return;
		}
		
		if (myTotem == null) return;
		
		isMyTurn = true;
		Debug.Log($"IA (Totem {myTotem.playerID}) inicia turno");
		StartCoroutine(ProcessAITurn());
	}
	
	public void StartAIMaker()
	{
		if (GameManager.Instance != null && GameManager.Instance.juegoTerminado)
		{
			Debug.Log("⏹️ Juego terminado, IA no puede construir");
			return;
		}

		StartCoroutine(AIMakerRoutine());
	}

	private IEnumerator AIMakerRoutine()
	{
		//Debug.Log("IA comenzando construcción del tablero...");
		
		// 1. Espera inicial
		yield return new WaitForSeconds(1f);

		// 2. Voltea un hexágono aleatorio
		HexagonPiece hexToFlip = GameManager.Instance.GetRandomUnflippedHexagon();
		if (hexToFlip == null)
		{
			Debug.Log("No quedan hexágonos por voltear");
			GameManager.Instance.EndConstructionTurn(); // Asegurar que el turno termina
			yield break;
		}

		//Debug.Log($"IA volteando hexágono: {hexToFlip.gameObject.name}");
		yield return StartCoroutine(hexToFlip.FlipPiece(true)); // true indica que es turno de IA

		// 3. Espera a que termine la animación
		while (hexToFlip.isAnimating)
		{
			yield return null;
		}

		// 4. Conectar el hexágono al tablero
		yield return StartCoroutine(ConnectHexagonToBoard(hexToFlip));

		// 5. Esperar un frame para asegurar que todo se actualizó
		yield return null;

		// 6. Terminar el turno explícitamente
		GameManager.Instance.EndConstructionTurn();
	}
	
	private IEnumerator ConnectHexagonToBoard(HexagonPiece hexToConnect)
	{
		// 1. Obtener imanes realmente disponibles con bloqueo
		List<Transform> availableMagnets = MagnetSystem.Instance.allMagnets
			.Where(m => MagnetSystem.Instance.IsMagnetAvailableForAI(m))
			.ToList();

		if (availableMagnets.Count == 0)
		{
			Debug.Log("No hay imanes disponibles para conectar");
			yield break;
		}

		// 2. Seleccionar y bloquear imán
		Transform targetMagnet = null;
		int attempts = 0;
		const int maxAttempts = 5;

		while (attempts < maxAttempts && targetMagnet == null)
		{
			Transform candidate = availableMagnets[Random.Range(0, availableMagnets.Count)];
			
			if (MagnetSystem.Instance.TryLockMagnet(candidate))
			{
				targetMagnet = candidate;
				//Debug.Log($"IA bloqueó imán: {targetMagnet.name}");
			}
			else
			{
				attempts++;
				yield return null; // Esperar un frame
			}
		}

		if (targetMagnet == null)
		{
			Debug.LogError("No se pudo bloquear ningún imán después de varios intentos");
			yield break;
		}

		// 3. Obtener información de conexión
		HexagonPiece targetHex = MagnetSystem.Instance.GetPieceForMagnet(targetMagnet);
		string cleanMagnetName = targetMagnet.name.Split(' ')[0];

		if (!hexToConnect.magnetConnections.ContainsKey(cleanMagnetName))
		{
			MagnetSystem.Instance.UnlockMagnet(targetMagnet);
			Debug.LogError($"Conexión no válida para {cleanMagnetName}");
			yield break;
		}

		string hexagonMagnetName = hexToConnect.magnetConnections[cleanMagnetName];
		Transform hexagonMagnet = hexToConnect.transform.Find(hexagonMagnetName);

		if (hexagonMagnet == null)
		{
			MagnetSystem.Instance.UnlockMagnet(targetMagnet);
			Debug.LogError($"Imán correspondiente {hexagonMagnetName} no encontrado");
			yield break;
		}
		
		if (!MagnetSystem.Instance.VerifyMagnetForConnection(targetMagnet))
		{
			MagnetSystem.Instance.UnlockMagnet(targetMagnet);
			Debug.LogError("El imán objetivo no pasa la verificación final");
			yield break;
		}

		// 4. Mover y conectar
		yield return StartCoroutine(MoveHexagonToConnect(hexToConnect, targetMagnet, hexagonMagnet));

		// 5. Liberar bloqueo (si aún existe)
		MagnetSystem.Instance.UnlockMagnet(targetMagnet);
	}

	private IEnumerator MoveHexagonToConnect(HexagonPiece hex, Transform targetMagnet, Transform hexMagnet)
	{
		if (targetMagnet == null || hexMagnet == null)
		{
			Debug.LogError("MoveHexagonToConnect: Magnet references are null");
			yield break;
		}

		if (!MagnetSystem.Instance.IsMagnetLockedByAI(targetMagnet))
		{
			Debug.LogError($"MoveHexagonToConnect: Magnet {targetMagnet.name} not properly locked");
			yield break;
		}

		Vector3 connectionOffset = hexMagnet.position - hex.transform.position;
		Vector3 targetPosition = targetMagnet.position - connectionOffset;
		Vector3 startPos = hex.transform.position;
		Vector3 raisedPosition = startPos + Vector3.up * 1.0f; // Aumentado a 1.0f para mayor altura

		float liftDuration = 0.3f; // Tiempo para subir (aumentado)
		float moveDuration = 0.6f; // Tiempo para moverse horizontalmente
		float descendDuration = 0.3f; // Tiempo para bajar (aumentado)

		hex.SetCollidersEnabled(false);

		// Fase 1: Levantar el hexágono (usando hex.gameObject)
		LeanTween.move(hex.gameObject, raisedPosition, liftDuration).setEase(LeanTweenType.easeOutQuad);

		// Esperar a que termine la elevación
		yield return new WaitForSeconds(liftDuration);

		// Fase 2: Mover horizontalmente a la posición objetivo (manteniendo altura)
		LeanTween.move(hex.gameObject, new Vector3(targetPosition.x, raisedPosition.y, targetPosition.z), moveDuration)
				 .setEase(LeanTweenType.easeInOutQuad);

		// Esperar a que termine el movimiento horizontal
		yield return new WaitForSeconds(moveDuration);

		// Fase 3: Bajar a la posición final
		LeanTween.move(hex.gameObject, targetPosition, descendDuration).setEase(LeanTweenType.easeInQuad);

		// Esperar a que termine el descenso
		yield return new WaitForSeconds(descendDuration);

		hex.transform.position = targetPosition;
		hex.isConnected = true;
		
		HexagonPiece targetPiece = MagnetSystem.Instance.GetPieceForMagnet(targetMagnet);
		if (MagnetSystem.Instance.ConfirmAIConnection(targetMagnet, hexMagnet))
		{
			if (targetPiece != null)
			{
				hex.RegisterConnection(targetPiece);
				MagnetSystem.Instance.ProcessNewConnection(hex, hexMagnet);                
			}
		}
		else
		{
			Debug.LogWarning("Conexión no confirmada - revirtiendo posición");
			hex.transform.position = startPos;
		}

		hex.SetCollidersEnabled(true);
		hex.SetMagnetsVisibility(true);
		MagnetSystem.Instance.UpdateMagnetOccupancyFromPhysics();
		targetPiece?.ForcePhysicalConnectionCheck();
		hex.ForcePhysicalConnectionCheck();
		MagnetSystem.Instance.UnlockMagnet(targetMagnet);
	}


	/*private void DebugMagnetStatus(Transform magnet)
	{
		string status = MagnetSystem.Instance.GetMagnetStatusString(magnet);
		Debug.Log($"Estado de {magnet.name}: {status}");
	}*/

    private IEnumerator ProcessAITurn()
	{
		// Esperar a que termine cualquier robo de carta previo
		while (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
		{
			Debug.Log("🤖 IA esperando a que termine el robo de carta...");
			yield return new WaitForSeconds(0.5f);
		}
		
		yield return new WaitForSeconds(1f); // Espera para simular "pensamiento"
		
		// Tirar el dado automáticamente
		GameManager.Instance.ForceDiceRollForAI();
		
		// Esperar resultado del dado
		while (GameManager.Instance.waitingForDiceRoll)
		{
			yield return null;
		}
		
		// Verificar si después de tirar el dado debemos robar carta
		if (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
		{
			Debug.Log("🤖 IA cayó en casilla de robo, robando carta...");
			
			// Esperar a que termine el proceso de robo
			while (GameManager.Instance.esperandoRoboCarta)
			{
				yield return null;
			}
			
			Debug.Log("🤖 IA completó robo de carta, terminando turno");
			
			// Terminar turno después de robar
			GameManager.Instance.EndTurn();
			yield break;
		}
		
		// Continuar con movimiento normal si no hay robo de carta
		MakeDecision();
	}
	
	private void MakeDecision()
	{
		if (!isMyTurn) return;
		
		// Verificar si estamos esperando por robo de carta
		if (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
		{
			Debug.Log("🤖 IA esperando a que termine el robo de carta antes de tomar decisión");
			return;
		}
		
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

    private HexagonPiece ChooseBestHexagon()
	{
		// 1. Obtén los hexágonos seleccionables del GameManager
		List<HexagonPiece> selectableHexagons = GameManager.Instance.selectableHexagons;
		
		// 2. Si no hay movimientos válidos, retorna null (esto evita CS0161)
		if (selectableHexagons == null || selectableHexagons.Count == 0)
		{
			Debug.LogWarning("IA: No hay hexágonos seleccionables.");
			return null;
		}

		// 3. Estrategia de decisión (prioriza cartas de robo, luego su color, luego aleatorio)
		HexagonPiece chosenHex = selectableHexagons[0]; // Por defecto, el primero
		
		// 1. Buscar hexágonos con totems enemigos (excluyendo la pieza principal)
		List<HexagonPiece> hexagonsWithEnemies = selectableHexagons
			.Where(hex => !hex.isMainPiece && HasEnemyTotem(hex))
			.ToList();

		if (hexagonsWithEnemies.Count > 0)
		{
			Debug.Log($"IA {myTotem.playerID}: Priorizando comer totem enemigo");
			return hexagonsWithEnemies[0]; // Puedes añadir lógica para elegir el más cercano, etc.
		}

		// Busca cartas de robo
		foreach (var hex in selectableHexagons)
		{
			if (hex.isStealCardPiece)
			{
				chosenHex = hex;
				break;
			}
		}

		// Si no encontró carta de robo, busca hexágonos de su color
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

		// 4. Retorna el hexágono elegido 
		return chosenHex;
	}
	
	private bool HasEnemyTotem(HexagonPiece hex)
    {
        return cachedAllTotems.Any(totem => 
            totem != myTotem && 
            totem.currentHexagon == hex && 
            !totem.currentHexagon.isMainPiece);
    }
	
	public void DecidirRoboPorComer()
	{
		if (!isMyTurn) return;
		
		Debug.Log($"🤖 IA {myTotem.playerID} decidiendo qué robar...");
		
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
	
	private void RobarCartaDelMazo()
	{
		Debug.Log($"🤖 IA {myTotem.playerID} decide robar del mazo");
		
		GameObject cartaSuperior = MazoFisico.Instance?.GetCartaSuperior();
		if (cartaSuperior != null)
		{
			// Simular clic en la carta del mazo
			MazoFisico.Instance.ProcesarClicCarta(cartaSuperior);
		}
		else
		{
			Debug.LogWarning("🤖 No hay cartas en el mazo, robando del jugador comido");
			RobarCartaDelJugadorComido();
		}
	}

	private void RobarCartaDelJugadorComido()
	{
		Debug.Log($"🤖 IA {myTotem.playerID} decide robar del jugador comido");
		
		// Obtener una carta aleatoria de la mano del jugador comido
		if (GameManager.Instance.jugadorComido != null && 
			MazoFisico.Instance != null)
		{
			int jugadorComidoID = GameManager.Instance.jugadorComido.playerID;
			
			if (MazoFisico.Instance.manosJugadores.TryGetValue(jugadorComidoID, out ManoJugador manoVictima))
			{
				if (manoVictima.CantidadCartas > 0)
				{
					// Robar la primera carta disponible de la víctima
					GameObject cartaARobar = manoVictima.GetPrimeraCarta();
					if (cartaARobar != null)
					{
						// Verificar si la víctima es IA (para no cambiar escala innecesariamente)
						bool victimaEsIA = manoVictima.esIA;
						bool atacanteEsIA = true; // Porque este método solo lo llama la IA
						
						// Solo actualizar escala si es necesario
						if (victimaEsIA != atacanteEsIA)
						{
							Carta3D cartaScript = cartaARobar.GetComponent<Carta3D>();
							if (cartaScript != null)
							{
								cartaScript.CambiarEscala(atacanteEsIA);
							}
						}
						
						MazoFisico.Instance.ProcesarClicCarta(cartaARobar);
						//manoVictima.ForzarReorganizacion();
						return;
					}
				}
			}
		}
		
		// Si no puede robar del jugador, robar del mazo
		Debug.LogWarning("🤖 No puede robar del jugador comido, robando del mazo");
		RobarCartaDelMazo();
	}
}