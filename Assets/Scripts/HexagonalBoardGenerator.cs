using UnityEngine;
using System.Collections.Generic;

public class HexagonalBoardGenerator : MonoBehaviour
{
    [Header("Board Settings")]
    public GameObject hexagonPiecePrefab;
    public GameObject hexagonStealCardPrefab;
    public int rows = 4;
    public int piecesPerRow = 4;
    public float xOffset = 1.5f;
    public float zOffset = 1.3f;
    public bool startUpsideDown = true;
    public int stealCardHexagonCount = 2;

    [Header("Color Settings")]
    public ColorSettings colorSettings;
    
    [Header("Player Settings")]
    public HexagonPiece mainPiece;
    public GameObject playerPrefab;
    public float playerSpawnRadius = 0.8f;
    public float playerHeightOffset = 0.5f;

    // Variables privadas
    private List<Color> shuffledColors = new List<Color>();
    private Dictionary<int, Vector3> playerStartPositions = new Dictionary<int, Vector3>();
    private static HexagonalBoardGenerator _instance;
    public static HexagonalBoardGenerator Instance { get { return _instance; } }

    void Start()
    {
        // Implementación del Singleton
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
        
        InitializeColorDistribution();
        GenerateHexagonalBoard();
        SpawnPlayers();
    }

    /// <summary>
    /// Distribuye los colores disponibles para las piezas del tablero
    /// </summary>
    private void InitializeColorDistribution()
    {
        int totalColoredPieces = colorSettings.availableColors.Count * colorSettings.piecesPerColor;
        int blackPiecesCount = (rows * piecesPerRow) - totalColoredPieces - stealCardHexagonCount;

        List<Color> allPieceColors = new List<Color>();
        
        // Añade piezas por cada color disponible
        foreach (Color color in colorSettings.availableColors)
        {
            for (int i = 0; i < colorSettings.piecesPerColor; i++)
            {
                allPieceColors.Add(color);
            }
        }
        
        // Añade piezas negras (ocultas)
        for (int i = 0; i < blackPiecesCount; i++)
        {
            allPieceColors.Add(colorSettings.hiddenColor);
        }
        
        shuffledColors = ShuffleList(allPieceColors);
    }

    /// <summary>
    /// Baraja una lista de colores aleatoriamente
    /// </summary>
    private List<Color> ShuffleList(List<Color> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            Color temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
        return list;
    }

    /// <summary>
    /// Genera el tablero hexagonal con todas las piezas
    /// </summary>
    private void GenerateHexagonalBoard()
    {
        List<Vector3> positions = CalculateBoardPositions();
        ShufflePositions(positions);

        for (int i = 0; i < positions.Count; i++)
        {
            bool isStealCard = i < stealCardHexagonCount;
            CreateHexagonPiece(positions[i], i, isStealCard);
        }
    }

    /// <summary>
    /// Calcula todas las posiciones posibles para las piezas del tablero
    /// </summary>
    private List<Vector3> CalculateBoardPositions()
    {
        List<Vector3> positions = new List<Vector3>();

        for (int row = 0; row < rows; row++)
        {
            int piecesInThisRow = (row % 2 == 0) ? piecesPerRow : piecesPerRow - 1;
            float rowXOffset = (row % 2 == 0) ? 0f : xOffset * 0.5f;

            for (int col = 0; col < piecesInThisRow; col++)
            {
                positions.Add(new Vector3(col * xOffset + rowXOffset, 0, row * zOffset));
            }
        }

        return positions;
    }

    /// <summary>
    /// Crea una pieza hexagonal en la posición especificada
    /// </summary>
    private void CreateHexagonPiece(Vector3 position, int index, bool isStealCard)
    {
        Quaternion rotation = startUpsideDown ? Quaternion.Euler(180, 0, 0) : Quaternion.identity;
        GameObject piecePrefab = isStealCard ? hexagonStealCardPrefab : hexagonPiecePrefab;

        GameObject newPiece = Instantiate(piecePrefab, position, rotation, transform);
        newPiece.name = $"HexagonPiece_{index}";

        HexagonPiece pieceComponent = newPiece.GetComponent<HexagonPiece>();
        if (pieceComponent != null)
        {
            if (isStealCard)
            {
                pieceComponent.isStealCardPiece = true;
            }
            else if (index - stealCardHexagonCount < shuffledColors.Count)
            {
                pieceComponent.SetHiddenColor(shuffledColors[index - stealCardHexagonCount]);
            }
            pieceComponent.SetMagnetsVisibility(false);
        }
    }

    /// <summary>
    /// Baraja las posiciones del tablero aleatoriamente
    /// </summary>
    private void ShufflePositions(List<Vector3> positions)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            int randomIndex = Random.Range(i, positions.Count);
            Vector3 temp = positions[i];
            positions[i] = positions[randomIndex];
            positions[randomIndex] = temp;
        }
    }

    /// <summary>
    /// Instancia los jugadores alrededor de la pieza principal
    /// </summary>
    private void SpawnPlayers()
    {
        int playerCount = Menu.cantidadJugadoresAEinstanciar;
        List<Color> playerColors = colorSettings.availableColors;
        Vector3[] spawnPositions = CalculatePlayerPositions(playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            if (i >= playerColors.Count) break;

            GameObject player = InstantiatePlayer(spawnPositions[i], i + 1, playerColors[i]);
            playerStartPositions[i + 1] = spawnPositions[i];
        }
		
        GameManager.Instance?.RegisterMainPiece(mainPiece);
    }

    /// <summary>
    /// Calcula las posiciones de spawn para los jugadores en forma hexagonal
    /// </summary>
    private Vector3[] CalculatePlayerPositions(int playerCount)
    {
        Vector3[] positions = new Vector3[playerCount];
        float angleStep = 360f / playerCount;

        for (int i = 0; i < playerCount; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            positions[i] = mainPiece.transform.position + new Vector3(
                Mathf.Cos(angle) * playerSpawnRadius,
                playerHeightOffset,
                Mathf.Sin(angle) * playerSpawnRadius
            );
        }

        return positions;
    }

    /// <summary>
    /// Instancia un jugador con la configuración adecuada
    /// </summary>
    private GameObject InstantiatePlayer(Vector3 position, int playerID, Color color)
    {
        GameObject player = Instantiate(playerPrefab, position, Quaternion.identity);
        PlayerTotem totem = player.GetComponent<PlayerTotem>();

        if (totem != null)
        {
            totem.playerID = playerID;
            totem.currentHexagon = mainPiece;
            totem.ApplyColor(color);
            
            Vector3 lookDirection = mainPiece.transform.position - player.transform.position;
            player.transform.rotation = Quaternion.LookRotation(-lookDirection);
            
			if (playerID > (Menu.cantidadJugadoresAEinstanciar - Menu.cantidadIA))
			{
				player.AddComponent<AIController>();
				Debug.Log($"Jugador {playerID} es IA");
			}
			
            StartCoroutine(AnimatePlayerSpawn(player));
        }

        return player;
    }

    /// <summary>
    /// Animación de aparición suave para los jugadores
    /// </summary>
    private System.Collections.IEnumerator AnimatePlayerSpawn(GameObject player)
    {
        Vector3 originalScale = player.transform.localScale;
        player.transform.localScale = Vector3.zero;

        LeanTween.scale(player, originalScale * 1.2f, 0.3f)
                 .setEase(LeanTweenType.easeOutBack);

        yield return new WaitForSeconds(0.3f);

        LeanTween.scale(player, originalScale, 0.15f)
                 .setEase(LeanTweenType.easeInOutQuad);
    }

    /// <summary>
    /// Obtiene la posición de inicio para un jugador específico
    /// </summary>
    public Vector3 GetStartPosition(int playerID)
    {
        if(playerStartPositions.ContainsKey(playerID))
        {
            return playerStartPositions[playerID];
        }
        return Vector3.zero;
    }
}