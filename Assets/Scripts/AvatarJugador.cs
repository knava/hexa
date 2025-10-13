using UnityEngine;
using TMPro;

public class AvatarJugador : MonoBehaviour
{
    [Header("Componentes Visuales")]
    public MeshRenderer rendererBase;        // Renderer del avatar principal
    public MeshRenderer rendererIcono;       // Opcional: para iconos
    public TextMeshPro textoNombre;          // Texto con "Jugador X"
    public GameObject indicadorSeleccion;    // Anillo/efecto alrededor cuando está seleccionado
    public GameObject indicadorIA;           // Texto/icono que indica que es IA
    
    [Header("Configuración")]
    public Color colorResaltado = Color.yellow;
    public Color colorNormal = Color.white;
    public float escalaResaltado = 1.2f;
    
    private int playerID;
    private Color colorJugador;
    private bool esIA;
    private Material materialBaseOriginal;
    private Vector3 escalaOriginal;

    void Awake()
    {
        // Guardar referencia original
        if (rendererBase != null)
        {
            materialBaseOriginal = rendererBase.material;
        }
        escalaOriginal = transform.localScale;
        
        // Ocultar indicadores por defecto
        if (indicadorSeleccion != null)
        {
            indicadorSeleccion.SetActive(false);
        }
        
        if (indicadorIA != null)
            indicadorIA.SetActive(false);
            
        // NUEVO: Verificar que el texto esté configurado
        if (textoNombre == null)
        {
            Debug.LogWarning($"⚠️ TextoNombre no asignado en {gameObject.name}");
            
            // Intentar buscar el componente automáticamente
            textoNombre = GetComponentInChildren<TextMeshPro>();
            if (textoNombre != null)
            {
                Debug.Log($"✅ TextoNombre encontrado automáticamente: {textoNombre.gameObject.name}");
            }
        }
    }
	
	void OnMouseDown()
	{
		Debug.Log($"🎯 Avatar Jugador {playerID} clickeado");
		
		// NUEVO: Notificar al sistema de cartas si estamos en modo selección
		if (GestionBotonesCartas.Instance != null)
		{
			// El sistema manejará la selección si está en modo correspondiente
		}
	}

    public void Configurar(int id, Color color, bool esJugadorIA)
    {
        playerID = id;
        colorJugador = color;
        esIA = esJugadorIA;

        // NUEVO: Configurar texto con formato J1, J2, etc.
        if (textoNombre != null)
        {
            textoNombre.text = $"J{id}";  // Formato J1, J2, J3...
            
            Debug.Log($"✅ Texto configurado: {textoNombre.text}");
        }
        else
        {
            Debug.LogError($"❌ TextoNombre es NULL - no se puede configurar texto para Jugador {id}");
        }

        // Configurar indicador IA
        if (indicadorIA != null)
        {
            indicadorIA.SetActive(esIA);
        }

        // Aplicar color base
        AplicarColor(color);
        
        Debug.Log($"✅ Avatar Jugador {id} configurado - Texto: J{id}");
    }

    public void AplicarColor(Color color)
    {
        if (rendererBase != null)
        {
            rendererBase.material.color = color;
        }
    }

    public void AplicarMaterial(Material material)
    {
        if (rendererBase != null)
        {
            rendererBase.material = material;
        }
    }

    // NUEVO MÉTODO: Aplicar icono al avatar
    public void AplicarIcono(Sprite icono)
    {
        // Si tenemos un renderer de icono específico, aplicamos el sprite como textura
        if (rendererIcono != null && icono != null)
        {
            // Crear un material temporal con el sprite
            Material materialIcono = new Material(Shader.Find("Standard"));
            materialIcono.mainTexture = icono.texture;
            rendererIcono.material = materialIcono;
            
            Debug.Log($"🖼️ Icono aplicado al avatar Jugador {playerID}");
        }
        else
        {
            Debug.LogWarning($"⚠️ No se puede aplicar icono - rendererIcono: {rendererIcono != null}, icono: {icono != null}");
        }
    }

    // ALTERNATIVA: Si prefieres usar GameObjects hijos para iconos
    public void AplicarIconoAlternativo(Sprite icono)
    {
        // Buscar o crear un GameObject hijo para el icono
        GameObject iconoObj = transform.Find("Icono")?.gameObject;
        if (iconoObj == null)
        {
            // Crear un Quad para mostrar el icono
            iconoObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            iconoObj.name = "Icono";
            iconoObj.transform.SetParent(transform);
            iconoObj.transform.localPosition = new Vector3(0, 0.8f, 0); // Encima del avatar
            iconoObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            
            // Remover el collider por defecto
            Destroy(iconoObj.GetComponent<Collider>());
        }
        
        // Aplicar el sprite como textura
        MeshRenderer iconoRenderer = iconoObj.GetComponent<MeshRenderer>();
        if (iconoRenderer != null && icono != null)
        {
            Material materialIcono = new Material(Shader.Find("Unlit/Transparent"));
            materialIcono.mainTexture = icono.texture;
            iconoRenderer.material = materialIcono;
        }
    }

    public void Resaltar(bool resaltar)
    {
        // Controlar el indicador de selección (anillo alrededor)
        if (indicadorSeleccion != null)
        {
            indicadorSeleccion.SetActive(resaltar);
            Debug.Log($"{(resaltar ? "🟡" : "⚪")} IndicadorSeleccion {(resaltar ? "activado" : "desactivado")} - Jugador {playerID}");
        }

        // Efecto de escala
        if (resaltar)
        {
            transform.localScale = escalaOriginal * escalaResaltado;
        }
        else
        {
            transform.localScale = escalaOriginal;
        }
    }

    public void SetInteractuable(bool interactuable)
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = interactuable;
        }
    }

    public int GetPlayerID()
    {
        return playerID;
    }

    public bool EsIA()
    {
        return esIA;
    }

    [ContextMenu("Debug Estado Avatar")]
    public void DebugEstadoAvatar()
    {
        Debug.Log($"🎭 Avatar Jugador {playerID}:");
        Debug.Log($"- Color: {colorJugador}");
        Debug.Log($"- Es IA: {esIA}");
        Debug.Log($"- Indicador Selección: {(indicadorSeleccion != null ? indicadorSeleccion.activeInHierarchy : "Null")}");
        Debug.Log($"- Posición: {transform.position}");
    }
}