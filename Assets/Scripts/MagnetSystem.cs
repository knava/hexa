using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MagnetSystem : MonoBehaviour
{
    public static MagnetSystem Instance;
    
    [Header("Colors")]
    public Color availableColor = Color.green;
    
    [Header("Collision Settings")]
    public float magnetDetectionRadius = 0.15f;
    public LayerMask magnetLayerMask;
    
    [Header("Raycast Settings")]
    public float raycastMaxDistance = 0.8f;
    public LayerMask raycastObstacleLayer;
    
    public List<Transform> allMagnets = new List<Transform>();
    private Dictionary<Transform, bool> magnetAvailability = new Dictionary<Transform, bool>();
    private Dictionary<Transform, HexagonPiece> magnetToPieceMap = new Dictionary<Transform, HexagonPiece>();
    
    private Dictionary<string, List<string>> adjacentMagnets = new Dictionary<string, List<string>>()
    {
        {"Magnet_1", new List<string>{"Magnet_6", "Magnet_3"}},
        {"Magnet_2", new List<string>{"Magnet_5", "Magnet_4"}},
        {"Magnet_3", new List<string>{"Magnet_1", "Magnet_5"}},
        {"Magnet_4", new List<string>{"Magnet_2", "Magnet_6"}},
        {"Magnet_5", new List<string>{"Magnet_3", "Magnet_2"}},
        {"Magnet_6", new List<string>{"Magnet_4", "Magnet_1"}}
    };

    public HexagonPiece CurrentlySelectedPiece { get; private set; }
    public bool CanSelectPiece => CurrentlySelectedPiece == null;
    private bool isConnectingInProgress = false;
    private bool isRaycastOccupation = false;
    private Dictionary<string, List<string>> availableMagnetsRegistry = new Dictionary<string, List<string>>();
    private Dictionary<Transform, bool> magnetLocks = new Dictionary<Transform, bool>();

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

    public bool IsAnyPieceBeingConnected() => isConnectingInProgress || CurrentlySelectedPiece != null;
    public void EndConnectionProcess() => isConnectingInProgress = false;
    public void StartConnectionProcess() => isConnectingInProgress = true;
    public void SetSelectedPiece(HexagonPiece piece) => CurrentlySelectedPiece = piece;
    public void ClearSelection() => CurrentlySelectedPiece = null;

    public void RegisterPiece(HexagonPiece piece, List<Transform> pieceMagnets)
    {
        foreach (Transform magnet in pieceMagnets)
        {
            if (!allMagnets.Contains(magnet))
            {
                allMagnets.Add(magnet);
                magnetAvailability[magnet] = true;
                magnetToPieceMap[magnet] = piece;
                SetMagnetColor(magnet, availableColor);
            }
        }
    }

    public void OccupyMagnet(Transform magnet)
    {
        if (magnet == null)
        {
            #if UNITY_EDITOR
            Debug.LogError("Intento de ocupar un imán nulo.");
            #endif
            return;
        }
        if (magnetAvailability.ContainsKey(magnet))
        {
            magnetAvailability[magnet] = false;
            SetMagnetVisibility(magnet, false);
        }
    }

    public void FinalizeMagnetOccupation(Transform magnet)
    {
        if (magnetAvailability.ContainsKey(magnet) && !magnetAvailability[magnet])
        {
            magnet.gameObject.layer = LayerMask.NameToLayer("MagnetsUnavailable");
        }
    }

    public void ForceDisableMagnet(Transform magnet)
    {
        if (magnet == null)
        {
            #if UNITY_EDITOR
            Debug.LogError("Intento de deshabilitar un imán nulo.");
            #endif
            return;
        }
        if (magnetAvailability.ContainsKey(magnet))
        {
            magnetAvailability[magnet] = false;
            SetMagnetVisibility(magnet, false);
            UpdateAvailableMagnetsRegistry();
        }
    }

    public bool IsMagnetAvailable(Transform magnet)
    {
        return magnetAvailability.TryGetValue(magnet, out bool isAvailable) && isAvailable;
    }

    public void OccupyMagnetAndAdjacents(Transform magnet)
    {
        StartConnectionProcess();
        if (!magnetAvailability.ContainsKey(magnet))
            return;

        HexagonPiece piece = magnetToPieceMap[magnet];
        OccupyMagnet(magnet);

        if (piece != null && !piece.isMainPiece)
        {
            string magnetName = magnet.name;
            if (adjacentMagnets.ContainsKey(magnetName))
            {
                foreach (string adjacentName in adjacentMagnets[magnetName])
                {
                    Transform adjacentMagnet = magnet.parent.Find(adjacentName);
                    if (adjacentMagnet != null && magnetAvailability.ContainsKey(adjacentMagnet))
                    {
                        OccupyMagnet(adjacentMagnet);
                        FinalizeMagnetOccupation(adjacentMagnet);
                    }
                }
            }
        }

        HighlightAvailableMagnets();
    }

    public void UpdateMagnetOccupancyFromPhysics()
    {
        foreach (Transform magnet in allMagnets)
        {
            if (magnetAvailability[magnet] == true)
            {
                bool isPhysicallyOccupied = CheckPhysicalOccupation(magnet);
                if (isPhysicallyOccupied)
                {
                    OccupyMagnet(magnet);
                }
            }
        }
    }

    public bool CheckPhysicalOccupation(Transform magnet)
    {
        if (!magnetAvailability[magnet])
            return true;

        float checkRadius = magnet.GetComponent<Collider>().bounds.extents.magnitude;
        Collider[] hitColliders = Physics.OverlapSphere(
            magnet.position, 
            checkRadius, 
            LayerMask.GetMask("Magnets"),
            QueryTriggerInteraction.Ignore
        );
        
        bool physicallyOccupied = false;
        foreach (var collider in hitColliders)
        {
            if (collider.transform != magnet && magnetToPieceMap.ContainsKey(collider.transform))
            {
                physicallyOccupied = true;
                HexagonPiece currentPiece = GetPieceForMagnet(magnet);
                HexagonPiece otherPiece = GetPieceForMagnet(collider.transform);
                
                if (currentPiece != null && otherPiece != null)
                {
                    GameManager.Instance.RegisterConnection(currentPiece, otherPiece);
                }
                
                if (currentPiece != null && !currentPiece.isMainPiece)
                {
                    OccupyAdjacentMagnets(magnet);
                }
                break;
            }
        }

        Debug.DrawRay(magnet.position, magnet.forward * raycastMaxDistance, Color.cyan);
        Ray ray = new Ray(magnet.position, magnet.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance, raycastObstacleLayer))
        {
            if (IsObstacleValid(magnet, hit.collider))
            {
                isRaycastOccupation = true;
            }
        }

        if (isRaycastOccupation)
        {
            OccupyMagnet(magnet);
        }
        isRaycastOccupation = false;
        
        return physicallyOccupied;
    }

    public HexagonPiece GetPieceForMagnet(Transform magnet)
    {
        return magnetToPieceMap.ContainsKey(magnet) ? magnetToPieceMap[magnet] : null;
    }

    public List<string> GetAdjacentMagnets(string magnetName)
    {
        if (adjacentMagnets.TryGetValue(magnetName, out List<string> magnets))
        {
            return magnets;
        }
        return new List<string>();
    }

    public void SetMagnetVisibility(Transform magnet, bool isVisible)
    {
        MeshRenderer renderer = magnet.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = isVisible;
        }
    }

    private void SetMagnetColor(Transform magnet, Color color)
    {
        Renderer renderer = magnet.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    #if UNITY_EDITOR
    public void LogAvailableMagnetsRegistry()
    {
        //Debug.Log("=== REGISTRO DE IMANES DISPONIBLES ===");
        foreach (var entry in availableMagnetsRegistry)
        {
            string magnets = string.Join(", ", entry.Value);
            //Debug.Log($"{entry.Key}: {magnets}");
        }
        //Debug.Log("=====================================");
    }

    public void DebugMagnetStatus(Transform magnet)
    {
        Debug.Log($"Estado de {magnet.name}:");
        Debug.Log($"- Disponible en diccionario: {magnetAvailability.ContainsKey(magnet) && magnetAvailability[magnet]}");
        Debug.Log($"- Ocupación física: {CheckPhysicalOccupation(magnet)}");
        Debug.Log($"- Pieza asociada: {(magnetToPieceMap.ContainsKey(magnet) ? magnetToPieceMap[magnet].gameObject.name : "Ninguna")}");
        Debug.Log($"- Pieza volteada: {(magnetToPieceMap.ContainsKey(magnet) ? magnetToPieceMap[magnet].isFlipped.ToString() : "N/A")}");
    }
    #endif

    public void UpdateAvailableMagnetsRegistry()
    {
        availableMagnetsRegistry.Clear();
        foreach (Transform magnet in allMagnets)
        {
            if (IsMagnetAvailable(magnet))
            {
                HexagonPiece piece = GetPieceForMagnet(magnet);
                string pieceName = piece.isMainPiece ? "PiezaPrincipal" : 
                                  $"Hexágono{GetHexagonNumber(piece)}";
                
                if (!availableMagnetsRegistry.ContainsKey(pieceName))
                {
                    availableMagnetsRegistry[pieceName] = new List<string>();
                }
                
                availableMagnetsRegistry[pieceName].Add(magnet.name);
            }
        }
    }
	
	public void DisableAllMagnets()
	{
		foreach (Transform magnet in allMagnets)
		{
			Collider col = magnet.GetComponent<Collider>();
			if (col != null)
			{
				col.enabled = false;
			}
			
			Renderer renderer = magnet.GetComponent<Renderer>();
			if (renderer != null)
			{
				renderer.enabled = false;
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

    public void ResetForNewTurn()
    {
        CurrentlySelectedPiece = null;
        isConnectingInProgress = false;
        HighlightAvailableMagnets();
    }

    public List<Transform> GetTrueAvailableMagnets()
    {
        List<Transform> available = new List<Transform>();
        foreach (Transform magnet in allMagnets)
        {
            if (IsMagnetAvailable(magnet) && 
                !CheckPhysicalOccupation(magnet) &&
                magnetToPieceMap.ContainsKey(magnet) &&
                (magnetToPieceMap[magnet] == null || magnetToPieceMap[magnet].isFlipped))
            {
                available.Add(magnet);
            }
        }
        return available;
    }

    public bool IsMagnetReallyAvailable(Transform magnet)
    {
        if (!allMagnets.Contains(magnet)) return false;
        return magnetAvailability.TryGetValue(magnet, out bool available) && available &&
               !CheckPhysicalOccupation(magnet) &&
               (magnetToPieceMap.TryGetValue(magnet, out HexagonPiece piece) && 
                (piece == null || piece.isFlipped));
    }

    public void UnlockMagnet(Transform magnet)
    {
        if (magnetLocks.ContainsKey(magnet))
            magnetLocks[magnet] = false;
    }

    public bool TryLockMagnet(Transform magnet)
    {
        if (!magnetAvailability.ContainsKey(magnet) || !magnetAvailability[magnet])
            return false;

        if (magnetLocks.ContainsKey(magnet) && magnetLocks[magnet])
            return false;

        magnetLocks[magnet] = true;
        return true;
    }

    public bool IsMagnetAvailableForAI(Transform magnet)
    {
        return magnetAvailability.ContainsKey(magnet) && 
               magnetAvailability[magnet] &&
               (!magnetLocks.ContainsKey(magnet) || !magnetLocks[magnet]);
    }

    public bool IsMagnetLockedByAI(Transform magnet)
    {
        return magnetLocks.ContainsKey(magnet) && magnetLocks[magnet];
    }

    public bool ConfirmAIConnection(Transform magnet1, Transform magnet2)
    {
        if (!IsMagnetLockedByAI(magnet1)) return false;
        OccupyMagnetAndAdjacents(magnet1);
        OccupyMagnetAndAdjacents(magnet2);
        UpdateMagnetOccupancyFromPhysics();
        UpdateAvailableMagnetsRegistry();
        return true;
    }

    public string GetMagnetStatusString(Transform magnet)
    {
        if (!allMagnets.Contains(magnet)) return "NOT_REGISTERED";
        return $"Available: {magnetAvailability[magnet]}, " +
               $"Locked: {magnetLocks.ContainsKey(magnet) && magnetLocks[magnet]}, " +
               $"Physical: {!CheckPhysicalOccupation(magnet)}, " +
               $"Piece: {(magnetToPieceMap.ContainsKey(magnet) ? magnetToPieceMap[magnet].name : "None")}";
    }

    public bool VerifyMagnetForConnection(Transform magnet)
    {
        return IsMagnetLockedByAI(magnet) &&
               magnetAvailability.ContainsKey(magnet) &&
               magnetAvailability[magnet] &&
               !CheckPhysicalOccupation(magnet);
    }

    public void ProcessNewConnection(HexagonPiece newPiece, Transform connectedMagnet)
    {
        if (newPiece == null || connectedMagnet == null) return;
        RegisterPiece(newPiece, newPiece.GetComponentsInChildren<Transform>()
            .Where(t => t.name.StartsWith("Magnet_")).ToList());
        OccupyMagnetAndAdjacents(connectedMagnet);
        foreach (Transform magnet in newPiece.GetComponentsInChildren<Transform>()
            .Where(t => t.name.StartsWith("Magnet_")))
        {
            bool isConnectedOrAdjacent = magnet == connectedMagnet || 
                                        GetAdjacentMagnets(connectedMagnet.name).Contains(magnet.name);
            if (!isConnectedOrAdjacent)
            {
                magnetAvailability[magnet] = true;
                SetMagnetVisibility(magnet, true);
            }
        }
        UpdateAvailableMagnetsRegistry();
    }

    private void HighlightAvailableMagnets()
    {
        foreach (Transform magnet in allMagnets)
        {
            if (IsMagnetAvailable(magnet))
            {
                SetMagnetColor(magnet, availableColor);
                SetMagnetVisibility(magnet, true);
            }
            else
            {
                SetMagnetVisibility(magnet, false);
            }
        }
    }

    private bool IsObstacleValid(Transform magnet, Collider hitCollider)
    {
        return hitCollider != null && hitCollider.transform != magnet;
    }

    private void OccupyAdjacentMagnets(Transform magnet)
    {
        if (adjacentMagnets.ContainsKey(magnet.name))
        {
            foreach (string adjacentName in adjacentMagnets[magnet.name])
            {
                Transform adjacentMagnet = magnet.parent.Find(adjacentName);
                if (adjacentMagnet != null && magnetAvailability.ContainsKey(adjacentMagnet))
                {
                    OccupyMagnet(adjacentMagnet);
                }
            }
        }
    }
}