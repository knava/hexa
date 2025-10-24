using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class HexagonPiece : MonoBehaviour
{
    [Header("Configuración Especial")]
    public bool isMainPiece = false;
    public bool isStealCardPiece = false;
    
    [Header("Configuración de Materiales")]
    public int targetMaterialIndex = 2;
    public Material highlightMaterial;
    
    [Header("Componentes Visuales")]
    [SerializeField] private Renderer[] coloredRenderers;
    
    // Sistema de conexión por imanes
    private List<Transform> hexagonMagnets = new List<Transform>();
    public Dictionary<string, string> magnetConnections = new Dictionary<string, string>()
    {
        {"Magnet_1", "Magnet_2"}, {"Magnet_2", "Magnet_1"},
        {"Magnet_3", "Magnet_4"}, {"Magnet_4", "Magnet_3"},
        {"Magnet_5", "Magnet_6"}, {"Magnet_6", "Magnet_5"}
    };
    
    // Estado de la pieza
    public List<HexagonPiece> connectedPieces = new List<HexagonPiece>();
    private Renderer hexRenderer;
    private Material originalMaterial;
    private Material targetMaterial;
    private Color originalColor;
    private Color pieceHiddenColor;
    
    // Flags de estado
    public bool isConnected = false;
    public bool isFlipped = false;
    public bool isAnimating = false;
    public bool isSelectable = false;

    void Awake()
    {
        InitializeMaterials();
        FindAndAssignHexagonMagnets();
        
        // Inicializar pieza principal si es necesario
        if (isMainPiece)
        {
            StartCoroutine(InitializeMainPiece());
        }
    }

    /// <summary>
    /// Inicializa los materiales de la pieza hexagonal
    /// </summary>
    private void InitializeMaterials()
    {
        hexRenderer = GetComponent<Renderer>();
        if (hexRenderer == null) return;

        originalMaterial = hexRenderer.material;
        
        // Configurar material objetivo si existe
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

    /// <summary>
    /// Inicializa la pieza principal registrando sus imanes
    /// </summary>
    private IEnumerator InitializeMainPiece()
    {
        // Esperar a que el sistema de imanes esté disponible
        while (MagnetSystem.Instance == null)
        {
            yield return null;
        }
        
        MagnetSystem.Instance.RegisterPiece(this, hexagonMagnets);
        
        // Configurar colores de los imanes disponibles
        foreach (Transform magnet in hexagonMagnets)
        {
            SetMagnetColor(magnet, MagnetSystem.Instance.availableColor);
        }
    }

    /// <summary>
    /// Busca y asigna los imanes del modelo hexagonal
    /// </summary>
    private void FindAndAssignHexagonMagnets()
    {
        hexagonMagnets.Clear();
        
        // Buscar todos los imanes en los hijos
        hexagonMagnets = GetComponentsInChildren<Transform>()
            .Where(t => t.name.StartsWith("Magnet_"))
            .OrderBy(t => t.name)
            .ToList();
        
        // Configurar cada imán
        foreach (Transform magnet in hexagonMagnets)
        {
            magnet.gameObject.layer = LayerMask.NameToLayer("Magnets");
            
            // Asegurar que tiene collider
            if (magnet.GetComponent<Collider>() == null)
            {
                magnet.gameObject.AddComponent<SphereCollider>();
            }
        }
    }

    /// <summary>
    /// Maneja el clic del mouse en la pieza
    /// </summary>
    void OnMouseDown()
    {
        bool isHumanTurn = GameManager.Instance.currentPhase == GamePhase.BoardConstruction && 
                          GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].GetComponent<AIController>() == null;

        // Verificar condiciones para voltear pieza
        if (!isMainPiece && !isConnected && !isFlipped && !isAnimating && 
            isHumanTurn && MagnetSystem.Instance.CanSelectPiece && 
            !MagnetSystem.Instance.IsAnyPieceBeingConnected())
        {
            StartCoroutine(FlipPiece());
        }
        
        // Verificar si es seleccionable para movimiento
        if (isSelectable)
        {
            GameManager.Instance.SelectHexagon(this);
        }
    }

    /// <summary>
    /// Voltea la pieza con animación
    /// </summary>
    public IEnumerator FlipPiece(bool isAITurn = false)
    {
        isAnimating = true;
        float duration = 0.8f;
        Vector3 originalPosition = transform.position;
        Vector3 raisedPosition = originalPosition + Vector3.up * 0.5f;
        
        // Animación de elevación y rotación
        LeanTween.move(gameObject, raisedPosition, duration * 0.3f).setEase(LeanTweenType.easeOutQuad);
        LeanTween.rotate(gameObject, Vector3.zero, duration).setEase(LeanTweenType.easeInOutBack);
        LeanTween.move(gameObject, originalPosition, duration * 0.7f).setDelay(duration * 0.3f);
        
        // Actualizar color del material
        if (targetMaterial != null)
        {
            targetMaterial.color = pieceHiddenColor;
        }
        
        yield return new WaitForSeconds(duration);
        
        // Actualizar estado
        isFlipped = true;
        GameManager.Instance?.RegisterHexagonFlip(this);
        isAnimating = false;
        
        // Iniciar selección de imán si no es turno de IA
        if (!isAITurn)
        {
            StartCoroutine(SelectMagnet());
        }
    }

    /// <summary>
    /// Establece si la pieza es seleccionable para movimiento
    /// </summary>
    public void SetSelectable(bool selectable)
    {
        isSelectable = selectable;
        hexRenderer.material = selectable ? highlightMaterial : originalMaterial;
    }

    /// <summary>
    /// Establece el color oculto de la pieza
    /// </summary>
    public void SetHiddenColor(Color color)
    {
        if (isStealCardPiece) return;
        pieceHiddenColor = color;
    }

    /// <summary>
    /// Selecciona un imán para conectar la pieza
    /// </summary>
    private IEnumerator SelectMagnet()
    {
        MagnetSystem.Instance.SetSelectedPiece(this);
        MagnetSystem.Instance.StartConnectionProcess();
        Transform targetMagnet = null;
        
        // Esperar selección de imán por el jugador
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
                            // Registrar y conectar la pieza
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
        
        // Finalizar proceso de conexión
        MagnetSystem.Instance.EndConnectionProcess();
        MagnetSystem.Instance.ClearSelection();
        MagnetSystem.Instance.UpdateMagnetOccupancyFromPhysics();
        GameBoundary.Instance.CheckBoundaries();
    }

    /// <summary>
    /// Mueve la pieza para conectar con otro imán
    /// </summary>
    public IEnumerator MoveToConnectMagnets(Transform targetMagnet, Transform hexagonMagnet)
    {
        Vector3 connectionOffset = hexagonMagnet.position - transform.position;
        Vector3 targetPosition = targetMagnet.position - connectionOffset;
        Vector3 startPos = transform.position;
        Vector3 raisedPosition = startPos + Vector3.up * 0.5f;

        float liftDuration = 0.2f;
        float moveDuration = 0.6f;
        float descendDuration = 0.2f;

        SetCollidersEnabled(false);

        // Fase 1: Levantar la pieza
        LeanTween.move(gameObject, raisedPosition, liftDuration).setEase(LeanTweenType.easeOutQuad);
        yield return new WaitForSeconds(liftDuration);

        // Fase 2: Mover horizontalmente
        LeanTween.move(gameObject, new Vector3(targetPosition.x, raisedPosition.y, targetPosition.z), moveDuration).setEase(LeanTweenType.easeInOutQuad);
        yield return new WaitForSeconds(moveDuration);

        // Fase 3: Bajar a posición final
        LeanTween.move(gameObject, targetPosition, descendDuration).setEase(LeanTweenType.easeInQuad);
        yield return new WaitForSeconds(descendDuration);

        transform.position = targetPosition;
        SetCollidersEnabled(true);
        
        // Registrar conexión con la pieza objetivo
        HexagonPiece targetPiece = MagnetSystem.Instance.GetPieceForMagnet(targetMagnet);
        if (targetPiece != null)
        {
            RegisterConnection(targetPiece);
        }
        
        // Actualizar estado de los imanes
        SetMagnetsVisibility(true);
        MagnetSystem.Instance.UpdateMagnetOccupancyFromPhysics();
        targetPiece?.ForcePhysicalConnectionCheck();
        ForcePhysicalConnectionCheck();
        MagnetSystem.Instance.FinalizeMagnetOccupation(targetMagnet);
        MagnetSystem.Instance.FinalizeMagnetOccupation(hexagonMagnet);
        MagnetSystem.Instance.UpdateAvailableMagnetsRegistry();
        
        // Notificar fin de turno de construcción
        GameManager.Instance.StartPlayerMaker();
        GameManager.Instance.EndConstructionTurn();
    }

    /// <summary>
    /// Registra una conexión con otra pieza hexagonal
    /// </summary>
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

    /// <summary>
    /// Fuerza la verificación física de conexiones
    /// </summary>
    public void ForcePhysicalConnectionCheck()
    {
        foreach (Transform magnet in hexagonMagnets)
        {
            MagnetSystem.Instance.CheckPhysicalOccupation(magnet);
        }
    }

    /// <summary>
    /// Obtiene las piezas conectadas a esta
    /// </summary>
    public List<HexagonPiece> GetConnectedPieces()
    {
        return connectedPieces;
    }
    
    /// <summary>
    /// Propiedad para obtener el color de la pieza
    /// </summary>
    public Color PieceColor 
    { 
        get { return pieceHiddenColor; } 
    }

    /// <summary>
    /// Encuentra el imán correspondiente en esta pieza
    /// </summary>
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

    /// <summary>
    /// Habilita/deshabilita los colliders de los imanes
    /// </summary>
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

    /// <summary>
    /// Establece la visibilidad de los imanes
    /// </summary>
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

    /// <summary>
    /// Establece el color de un imán específico
    /// </summary>
    private void SetMagnetColor(Transform magnet, Color color)
    {
        Renderer renderer = magnet.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }
}