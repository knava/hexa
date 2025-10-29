using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(MeshRenderer))] // Garantiza que el GameObject tenga un MeshRenderer
public class PlayerTotem : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("ID único del jugador (debe ser entre 1 y 6)")]
    public int playerID = 1;
    
    [Tooltip("Color identificativo del jugador")]
    public Color playerColor = Color.white;
    
    [Header("Visual Components")]
    [Tooltip("Renderer principal del totem")]
    public MeshRenderer bodyRenderer;
    
    [Tooltip("Renderers secundarios que deben coincidir con el color principal")]
    public Renderer[] additionalRenderers;
    
    [Header("Gameplay")]
    [Tooltip("Hexágono actual donde está posicionado el totem")]
    public HexagonPiece currentHexagon;
    
    [Header("Movement Settings")]
    [Tooltip("Duración en segundos del movimiento entre hexágonos")]
    public float moveDuration = 0.5f;
    
    [Tooltip("Altura máxima del salto durante el movimiento")]
    public float jumpHeight = 0.3f;
    
    private Material instancedMaterial; // Material instanciado para este totem
    private bool isMoving = false;     // Flag para controlar si el totem está en movimiento
	
	private bool comioAlguien = false;
	
	public bool ComioAlguien 
    { 
        get { return comioAlguien; } 
        set { comioAlguien = value; } 
    }

    void Awake()
    {
        InitializeRenderer();
        ApplyColor(playerColor);
    }

    /// <summary>
    /// Inicializa el renderer principal y crea una instancia única de su material
    /// </summary>
    private void InitializeRenderer()
    {
        // Auto-asignar el MeshRenderer si no está asignado
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponent<MeshRenderer>();
        }
        
        // Crear material instanciado para evitar compartir materiales entre objetos
        if (bodyRenderer != null)
        {
            instancedMaterial = new Material(bodyRenderer.sharedMaterial);
            bodyRenderer.material = instancedMaterial;
        }
    }

    /// <summary>
    /// Inicia el movimiento del totem a lo largo de un camino de hexágonos
    /// </summary>
    /// <param name="path">Lista de hexágonos que forman el camino</param>
    public void MoveAlongPath(List<HexagonPiece> path)
    {
        if(!isMoving && path != null && path.Count > 0)
        {
            StartCoroutine(FollowPathAndEat(path));
        }
    }
	
	private IEnumerator AnimateEatenTotem(PlayerTotem totem)
	{
		yield return new WaitForSeconds(0.3f);
		
		Vector3 startPos = totem.transform.position;
		Vector3 endPos = FindAnyObjectByType<HexagonalBoardGenerator>().GetStartPosition(totem.playerID);
		float duration = 1f;
		
		// Punto alto del arco (50% más alto que el más salto de startPos/endPos)
		float arcHeight = Mathf.Max(startPos.y, endPos.y) * 1.5f;
		Vector3 arcPeak = Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * arcHeight;
		
		// Animación en arco usando LeanTween.path
		LTBezierPath arcPath = new LTBezierPath(new Vector3[] {
			startPos,
			startPos + (arcPeak - startPos) * 0.5f,
			arcPeak,
			endPos
		});
		
		LeanTween.move(totem.gameObject, arcPath, duration)
			.setEase(LeanTweenType.easeOutQuad);
		
		// Rotación durante el vuelo
		LeanTween.rotateAround(totem.gameObject, Vector3.up, 360f, duration);
		
		// Actualizar lógica inmediatamente pero la posición visual se anima
		totem.currentHexagon = FindAnyObjectByType<HexagonalBoardGenerator>().mainPiece;
		
		yield return new WaitForSeconds(duration);
		
		// Asegurar posición final exacta
		totem.transform.position = endPos;
		
		// Reorientar
		Vector3 lookDirection = FindAnyObjectByType<HexagonalBoardGenerator>().mainPiece.transform.position - endPos;
		totem.transform.rotation = Quaternion.LookRotation(-lookDirection);
	}

    /// <summary>
    /// Corrutina que maneja el movimiento suave entre hexágonos
    /// </summary>
	private IEnumerator FollowPathAndEat(List<HexagonPiece> path)
	{
		isMoving = true;
		comioAlguien = false;
		HexagonPiece destination = path[path.Count - 1];
		PlayerTotem jugadorComidoTemp = null;
		bool jugadorComidoEnCasillaRobo = false;
		
		// 1. Mover normalmente por el camino
		foreach(HexagonPiece hex in path)
		{
			Vector3 startPos = transform.position;
			Vector3 endPos = hex.isMainPiece ? 
				FindAnyObjectByType<HexagonalBoardGenerator>().GetStartPosition(playerID) : 
				hex.transform.position + Vector3.up * 0.5f;
				
			float progress = 0f;
			
			while(progress < 1f)
			{
				progress += Time.deltaTime / moveDuration;
				transform.position = Vector3.Lerp(startPos, endPos, progress) 
								  + Vector3.up * Mathf.Sin(progress * Mathf.PI) * jumpHeight;
				yield return null;
			}
			
			currentHexagon = hex;
		}
		
		if (destination.isDiamondPiece)
		{
			// Sumar puntos adicionales al jugador
			if (GameManager.Instance != null)
			{
				GameManager.Instance.SumarPuntosPorDiamante(this.playerID);
			}
			
			GameManager.Instance.FinDelJuego();
			yield break; // detenemos la coroutine para evitar seguir ejecutando acciones
		}
		
		// 2. PRIMERO: Verificar si hay totems para comer
		if (!destination.isMainPiece)
		{
			PlayerTotem[] allTotems = FindObjectsByType<PlayerTotem>(FindObjectsSortMode.None);
			
			foreach (PlayerTotem totem in allTotems)
			{
				if (totem != this && totem.currentHexagon == destination)
				{
					jugadorComidoTemp = totem;
					jugadorComidoEnCasillaRobo = destination.isStealCardPiece;
					
					yield return StartCoroutine(AnimateEatenTotem(totem));
					/*Debug.Log($"Totem {playerID} ha comido al totem {totem.playerID}" + 
							 (jugadorComidoEnCasillaRobo ? " en CASILLA DE ROBAR CARTA" : ""));*/
					comioAlguien = true;
					break;
				}
			}
			
			// 3. SEGUNDO: Manejar los robos
			if (comioAlguien && jugadorComidoTemp != null)
			{
				GameManager.Instance?.RegistrarJugadorComido(jugadorComidoTemp);
				
				// ? CASO ESPECIAL: Si el jugador comido está en casilla de robar carta
				if (jugadorComidoEnCasillaRobo)
				{
					//Debug.Log($"???? JUGADOR {playerID} COME EN CASILLA DE ROBAR - ACTIVANDO AMBOS ROBOS");
					
					// DESACTIVAR EndTurn automático en GameManager
					GameManager.Instance.bloquearEndTurnAutomatico = true;
					
					// PRIMERO: Activar robo por comer
					GameManager.Instance?.ActivarRoboPorComerDirecto(this, jugadorComidoTemp);
					
					// Esperar a que termine el robo por comer
					while (GameManager.Instance != null && GameManager.Instance.esperandoRoboPorComer)
					{
						yield return null;
					}
					
					// SEGUNDO: Activar robo por casilla (después de terminar el robo por comer)
					//Debug.Log($"???? ACTIVANDO ROBO POR CASILLA DESPUÉS DE COMER");
					GameManager.Instance?.IniciarRoboCarta(this);
					MazoFisico.Instance.HabilitarRoboUnaCarta();
					
					// Esperar a que termine el robo por casilla
					while (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
					{
						yield return null;
					}
					
					// ? FINALMENTE: Terminar turno manualmente después de AMBOS robos
					//Debug.Log($"???? AMBOS ROBOS COMPLETADOS - TERMINANDO TURNO MANUALMENTE");
					GameManager.Instance.bloquearEndTurnAutomatico = false;
					GameManager.Instance.EndTurn();
				}
				else
				{
					// ? CASO NORMAL: Solo robo por comer (dejar que GameManager maneje el EndTurn)
					GameManager.Instance?.ActivarRoboPorComerDirecto(this, jugadorComidoTemp);
					
					// Esperar a que termine el robo por comer
					while (GameManager.Instance != null && GameManager.Instance.esperandoRoboPorComer)
					{
						yield return null;
					}
				}
			}
		}
		
		// 4. TERCERO: Verificar si es casilla de robo (SOLO si no se comió a nadie)
		if (destination.isStealCardPiece && !comioAlguien)
		{
			//Debug.Log($"?? Jugador {playerID} CAYÓ en casilla de robar carta");
			ActivarRoboCarta();
			
			// Esperar a que termine el robo antes de continuar
			while (GameManager.Instance != null && GameManager.Instance.esperandoRoboCarta)
			{
				yield return null;
			}
		}
		
		isMoving = false;
		
		// 5. Finalmente, terminar turno solo si NO estamos en medio de ningún robo
		// y NO es el caso especial (que ya manejamos manualmente)
		if (GameManager.Instance != null && 
			!GameManager.Instance.esperandoRoboCarta && 
			!GameManager.Instance.esperandoRoboPorComer &&
			!destination.isStealCardPiece && 
			!comioAlguien &&
			!GameManager.Instance.bloquearEndTurnAutomatico)
		{
			GameManager.Instance?.EndTurn();
		}
	}

    /// <summary>
    /// Aplica un nuevo color al totem y todos sus componentes visuales
    /// </summary>
    /// <param name="newColor">Nuevo color a aplicar</param>
    public void ApplyColor(Color newColor)
    {
        playerColor = newColor;
        
        // Aplicar color al material principal
        if (instancedMaterial != null)
        {
            instancedMaterial.color = playerColor;
        }
        
        // Aplicar color a todos los renderers adicionales
        foreach (Renderer r in additionalRenderers)
        {
            if (r != null)
            {
                r.material.color = playerColor;
            }
        }
    }

    /// <summary>
    /// Mueve instantáneamente el totem a un hexágono específico
    /// </summary>
    /// <param name="targetHexagon">Hexágono destino</param>
    public void MoveToHexagon(HexagonPiece targetHexagon)
	{
		if (targetHexagon != null)
		{
			currentHexagon = targetHexagon;
			
			if(targetHexagon.isMainPiece)
			{
				transform.position = HexagonalBoardGenerator.Instance.GetStartPosition(playerID);
				// Mantener la rotación inicial hacia afuera
				Vector3 lookDirection = targetHexagon.transform.position - transform.position;
				transform.rotation = Quaternion.LookRotation(-lookDirection);
			}
			else
			{
				transform.position = targetHexagon.transform.position + Vector3.up * 0.5f;
				transform.rotation = targetHexagon.transform.rotation;
			}
		}
	}

    /// <summary>
    /// Restablece el totem a su estado inicial en el hexágono actual
    /// </summary>
    public void ResetTotem()
    {
        if (currentHexagon != null)
        {
            MoveToHexagon(currentHexagon);
        }
        ApplyColor(playerColor);
    }
	
	public void ReturnToMainPiece()
	{
		// Cambio aquí:
		HexagonalBoardGenerator boardGenerator = FindAnyObjectByType<HexagonalBoardGenerator>();
		if (boardGenerator != null)
		{
			currentHexagon = boardGenerator.mainPiece;
			// Usar la posición inicial específica para este jugador
			transform.position = boardGenerator.GetStartPosition(playerID);
			
			// Reorientar hacia afuera del centro
			Vector3 lookDirection = boardGenerator.mainPiece.transform.position - transform.position;
			transform.rotation = Quaternion.LookRotation(-lookDirection);
		}
	}
	private void ActivarRoboCarta()
	{
		if (MazoFisico.Instance != null)
		{
			// ? Este método solo se llama para casillas de robo, NO para comer jugadores
			GameManager.Instance?.IniciarRoboCarta(this);
			MazoFisico.Instance.HabilitarRoboUnaCarta();  // Solo habilitar mazo, no mano de otros
			
			// Si es IA, robar automáticamente después de un pequeño delay
			AIController aiController = GetComponent<AIController>();
			if (aiController != null)
			{
				Invoke("RobarCartaIA", 0.2f);
			}
		}
		/*else
		{
			Debug.LogError("MazoFisico.Instance es null - No se puede activar robo por CASILLA");
		}*/
	}

	// Nuevo método para robo de IA
	private void RobarCartaIA()
	{
		MazoFisico.Instance.RobarCartaParaJugador(playerID);
	}
}