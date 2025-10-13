using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class HexagonPiece : MonoBehaviour
{
    [Header("Special Pieces")]
    public bool isMainPiece = false;
    public bool isStealCardPiece = false;
    
    [Header("Material Settings")]
    public int targetMaterialIndex = 2;
    public Material highlightMaterial;
    
    [Header("Visual Settings")]
    [SerializeField] private Renderer[] coloredRenderers;
    
    private List<Transform> hexagonMagnets = new List<Transform>();
    public Dictionary<string, string> magnetConnections = new Dictionary<string, string>()
    {
        {"Magnet_1", "Magnet_2"}, {"Magnet_2", "Magnet_1"},
        {"Magnet_3", "Magnet_4"}, {"Magnet_4", "Magnet_3"},
        {"Magnet_5", "Magnet_6"}, {"Magnet_6", "Magnet_5"}
    };
    
    private List<HexagonPiece> connectedPieces = new List<HexagonPiece>();
    private Renderer hexRenderer;
    private Material originalMaterial;
    private Material targetMaterial;
    private Color originalColor;
    private Color pieceHiddenColor;
    
    public bool isConnected = false;
    public bool isFlipped = false;
    public bool isAnimating = false;
    public bool isSelectable = false;

    void Awake()
    {
        InitializeMaterials();
        FindAndAssignHexagonMagnets();
        if (isMainPiece)
        {
            StartCoroutine(InitializeMainPiece());
        }
    }

    private void InitializeMaterials()
    {
        hexRenderer = GetComponent<Renderer>();
        if (hexRenderer == null)
        {
            #if UNITY_EDITOR
            Debug.LogError("Renderer no encontrado en HexagonPiecee.");
            #endif
            return;
        }
        originalMaterial = hexRenderer.material;
        if (hexRenderer.materials.Length > targetMaterialIndex)
        {
            Material[] mats = hexRenderer.materials;
            mats[targetMaterialIndex] = new Material(mats[targetMaterialIndex]);
            hexRenderer.materials = mats;
            targetMaterial = mats[targetMaterialIndex];
            originalColor = targetMaterial.color;
            targetMaterial.color = Color.black;
        }
    }

    private IEnumerator InitializeMainPiece()
    {
        while (MagnetSystem.Instance == null)
        {
            yield return null;
        }
        MagnetSystem.Instance.RegisterPiece(this, hexagonMagnets);
        foreach (Transform magnet in hexagonMagnets)
        {
            SetMagnetColor(magnet, MagnetSystem.Instance.availableColor);
        }
    }

    private void FindAndAssignHexagonMagnets()
    {
        hexagonMagnets.Clear();
        hexagonMagnets = GetComponentsInChildren<Transform>()
            .Where(t => t.name.StartsWith("Magnet_"))
            .OrderBy(t => t.name)
            .ToList();
        
        foreach (Transform magnet in hexagonMagnets)
        {
            magnet.gameObject.layer = LayerMask.NameToLayer("Magnets");
            if (magnet.GetComponent<Collider>() == null)
            {
                magnet.gameObject.AddComponent<SphereCollider>();
            }
            if (magnet.GetComponent<Renderer>() == null)
            {
                #if UNITY_EDITOR
                Debug.LogError($"Magnet {magnet.name} is missing Renderer component");
                #endif
            }
        }
        
        #if UNITY_EDITOR
        if (hexagonMagnets.Count == 0)
        {
            Debug.LogError("No magnets found in FBX model");
        }
        #endif
    }

    void OnMouseDown()
    {
        bool isHumanTurn = GameManager.Instance.currentPhase == GamePhase.BoardConstruction && 
                          GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].GetComponent<AIController>() == null;

        if (!isMainPiece && !isConnected && !isFlipped && !isAnimating && 
            isHumanTurn && MagnetSystem.Instance.CanSelectPiece && 
            !MagnetSystem.Instance.IsAnyPieceBeingConnected())
        {
            StartCoroutine(FlipPiece());
        }
        if (isSelectable)
        {
            GameManager.Instance.SelectHexagon(this);
        }
    }

    public IEnumerator FlipPiece(bool isAITurn = false)
    {
        isAnimating = true;
        float duration = 0.8f;
        Vector3 originalPosition = transform.position;
        Vector3 raisedPosition = originalPosition + Vector3.up * 0.5f;
        
        LeanTween.move(gameObject, raisedPosition, duration * 0.3f).setEase(LeanTweenType.easeOutQuad);
        LeanTween.rotate(gameObject, Vector3.zero, duration).setEase(LeanTweenType.easeInOutBack);
        LeanTween.move(gameObject, originalPosition, duration * 0.7f).setDelay(duration * 0.3f);
        
        if (targetMaterial != null)
        {
            targetMaterial.color = pieceHiddenColor;
        }
        
        yield return new WaitForSeconds(duration);
        
        isFlipped = true;
        GameManager.Instance?.RegisterHexagonFlip(this);
        isAnimating = false;
        
        if (!isAITurn)
        {
            StartCoroutine(SelectMagnet());
        }
    }

    public void SetSelectable(bool selectable)
    {
        isSelectable = selectable;
        hexRenderer.material = selectable ? highlightMaterial : originalMaterial;
    }

    public void SetHiddenColor(Color color)
    {
        if (isStealCardPiece) return;
        pieceHiddenColor = color;
    }

    private IEnumerator SelectMagnet()
    {
        MagnetSystem.Instance.SetSelectedPiece(this);
        MagnetSystem.Instance.StartConnectionProcess();
        Transform targetMagnet = null;
        
        while (targetMagnet == null)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100, LayerMask.GetMask("Magnets")))
                {
                    if (MagnetSystem.Instance.IsMagnetAvailable(hit.transform))
                    {
                        targetMagnet = hit.transform;
                        string hexagonMagnetName = magnetConnections[targetMagnet.name];
                        Transform hexagonMagnet = FindCorrespondingHexagonMagnet(targetMagnet.name);
                        
                        if (hexagonMagnet != null)
                        {
                            MagnetSystem.Instance.RegisterPiece(this, hexagonMagnets);
                            MagnetSystem.Instance.OccupyMagnetAndAdjacents(targetMagnet);
                            MagnetSystem.Instance.OccupyMagnetAndAdjacents(hexagonMagnet);
                            
                            isConnected = true;
                            yield return StartCoroutine(MoveToConnectMagnets(targetMagnet, hexagonMagnet));
                        }
                    }
                }
            }
            yield return null;
        }
        
        MagnetSystem.Instance.EndConnectionProcess();
        MagnetSystem.Instance.ClearSelection();
        MagnetSystem.Instance.UpdateMagnetOccupancyFromPhysics();
        GameBoundary.Instance.CheckBoundaries();
    }

    public IEnumerator MoveToConnectMagnets(Transform targetMagnet, Transform hexagonMagnet)
	{
		Vector3 connectionOffset = hexagonMagnet.position - transform.position;
		Vector3 targetPosition = targetMagnet.position - connectionOffset;
		Vector3 startPos = transform.position;
		Vector3 raisedPosition = startPos + Vector3.up * 0.5f; // Levanta 0.5 unidades

		float totalDuration = 1f;
		float liftDuration = 0.2f; // Tiempo para subir
		float moveDuration = 0.6f; // Tiempo para moverse horizontalmente
		float descendDuration = 0.2f; // Tiempo para bajar

		SetCollidersEnabled(false);

		// Fase 1: Levantar el hexágono
		LeanTween.move(gameObject, raisedPosition, liftDuration).setEase(LeanTweenType.easeOutQuad);

		// Esperar a que termine la elevación
		yield return new WaitForSeconds(liftDuration);

		// Fase 2: Mover horizontalmente a la posición objetivo (manteniendo altura)
		LeanTween.move(gameObject, new Vector3(targetPosition.x, raisedPosition.y, targetPosition.z), moveDuration).setEase(LeanTweenType.easeInOutQuad);

		// Esperar a que termine el movimiento horizontal
		yield return new WaitForSeconds(moveDuration);

		// Fase 3: Bajar a la posición final
		LeanTween.move(gameObject, targetPosition, descendDuration).setEase(LeanTweenType.easeInQuad);

		// Esperar a que termine el descenso
		yield return new WaitForSeconds(descendDuration);

		transform.position = targetPosition;
		SetCollidersEnabled(true);
		
		HexagonPiece targetPiece = MagnetSystem.Instance.GetPieceForMagnet(targetMagnet);
		if (targetPiece != null)
		{
			RegisterConnection(targetPiece);
		}
		
		SetMagnetsVisibility(true);
		MagnetSystem.Instance.UpdateMagnetOccupancyFromPhysics();
		targetPiece?.ForcePhysicalConnectionCheck();
		ForcePhysicalConnectionCheck();
		MagnetSystem.Instance.FinalizeMagnetOccupation(targetMagnet);
		MagnetSystem.Instance.FinalizeMagnetOccupation(hexagonMagnet);
		MagnetSystem.Instance.UpdateAvailableMagnetsRegistry();
		//MagnetSystem.Instance.LogAvailableMagnetsRegistry();
		GameManager.Instance.StartPlayerMaker();
		GameManager.Instance.EndConstructionTurn();
	}

    public void RegisterConnection(HexagonPiece otherPiece)
    {
        if (!connectedPieces.Contains(otherPiece))
        {
            connectedPieces.Add(otherPiece);
            otherPiece.connectedPieces.Add(this);
            GameManager.Instance.RegisterConnection(this, otherPiece);
            SetMagnetsVisibility(true);
        }
    }

    public void ForcePhysicalConnectionCheck()
    {
        foreach (Transform magnet in hexagonMagnets)
        {
            MagnetSystem.Instance.CheckPhysicalOccupation(magnet);
        }
    }

    public List<HexagonPiece> GetConnectedPieces()
    {
        return connectedPieces;
    }
    
    public Color PieceColor 
    { 
        get { return pieceHiddenColor; } 
    }

    private Transform FindCorrespondingHexagonMagnet(string mainMagnetName)
    {
        if (magnetConnections.TryGetValue(mainMagnetName, out string hexagonMagnetName))
        {
            foreach (Transform magnet in hexagonMagnets)
            {
                if (magnet.name == hexagonMagnetName)
                {
                    return magnet;
                }
            }
        }
        return null;
    }

    public void SetCollidersEnabled(bool enabled)
    {
        foreach (Transform magnet in hexagonMagnets)
        {
            Collider col = magnet.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = enabled;
            }
        }
    }

    public void SetMagnetsVisibility(bool isVisible)
    {
        foreach (Transform magnet in hexagonMagnets)
        {
            MeshRenderer renderer = magnet.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                bool isAvailable = MagnetSystem.Instance.IsMagnetAvailable(magnet);
                renderer.enabled = isVisible && isAvailable;
            }
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
}