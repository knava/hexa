using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class SistemaAvataresJugadores : MonoBehaviour
{
    public static SistemaAvataresJugadores Instance;
    
    [Header("Prefabs y Referencias")]
    public GameObject prefabAvatarJugador;
    
    [Header("Configuración Visual")]
    public Material[] materialesAvatares;
    public Sprite[] iconosJugadores;
    
    [Header("Componentes Avatar")]
    public Vector3 offsetAvatar = new Vector3(-2f, 0f, 0f); // Offset respecto a la mano
    public bool mantenerEscalaPrefab = true; // NUEVO: Respetar la escala del prefab

    private Dictionary<int, GameObject> avataresJugadores = new Dictionary<int, GameObject>();
    private Dictionary<int, AvatarJugador> componentesAvatar = new Dictionary<int, AvatarJugador>();
    private Transform contenedorManos;

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
        // Buscar el contenedorManos automáticamente
        contenedorManos = GameObject.Find("contenedorManos")?.transform;
        if (contenedorManos == null)
        {
            Debug.LogError("❌ No se encontró el GameObject 'contenedorManos'");
        }
    }

    public void InicializarAvatares(int cantidadJugadores, List<PlayerTotem> jugadores)
    {
        // Limpiar avatares existentes
        foreach (var avatar in avataresJugadores.Values)
        {
            if (avatar != null) Destroy(avatar);
        }
        avataresJugadores.Clear();
        componentesAvatar.Clear();

        for (int i = 0; i < cantidadJugadores; i++)
        {
            int playerID = i + 1;
            
            // Buscar la mano del jugador en el contenedorManos
            Transform manoJugador = contenedorManos?.Find($"Mano_Jugador_{playerID}");
            if (manoJugador == null)
            {
                Debug.LogWarning($"⚠️ No se encontró Mano_Jugador_{playerID} en contenedorManos");
                continue;
            }
            
            // Crear avatar como hijo de la mano del jugador
            GameObject avatarObj = Instantiate(prefabAvatarJugador, manoJugador);
            avatarObj.name = $"Avatar_Jugador_{playerID}";
            
            // Posicionar el avatar relativo a la mano (usando el offset)
            avatarObj.transform.localPosition = offsetAvatar;
            avatarObj.transform.localRotation = Quaternion.identity;
            
            // NUEVO: Respetar la escala del prefab
            if (mantenerEscalaPrefab)
            {
                // Mantener la escala original del prefab (0.5 en tu caso)
                avatarObj.transform.localScale = prefabAvatarJugador.transform.localScale;
                Debug.Log($"✅ Avatar escala mantenida: {avatarObj.transform.localScale}");
            }
            else
            {
                // Escala por defecto (comportamiento anterior)
                avatarObj.transform.localScale = Vector3.one;
            }
            
            // Configurar componente AvatarJugador
            AvatarJugador avatarComponent = avatarObj.GetComponent<AvatarJugador>();
            if (avatarComponent != null)
            {
                PlayerTotem jugador = jugadores.Find(p => p.playerID == playerID);
                if (jugador != null)
                {
                    avatarComponent.Configurar(playerID, jugador.playerColor, jugador.GetComponent<AIController>() != null);
                    
                    // Aplicar material/color
                    if (i < materialesAvatares.Length)
                    {
                        avatarComponent.AplicarMaterial(materialesAvatares[i]);
                    }
                    else
                    {
                        avatarComponent.AplicarColor(jugador.playerColor);
                    }
                }
                
                componentesAvatar.Add(playerID, avatarComponent);
            }
            
            avataresJugadores.Add(playerID, avatarObj);
            
            Debug.Log($"✅ Avatar Jugador {playerID} - Escala: {avatarObj.transform.localScale}");
        }
        
        Debug.Log($"✅ Avatares inicializados para {avataresJugadores.Count} jugadores");
    }

    public void MostrarAvatares()
    {
        foreach (var avatar in avataresJugadores.Values)
        {
            if (avatar != null)
            {
                avatar.SetActive(true);
            }
        }
    }

    public void OcultarAvatares()
    {
        foreach (var avatar in avataresJugadores.Values)
        {
            if (avatar != null)
            {
                avatar.SetActive(false);
            }
        }
    }

    public void ResaltarAvatar(int playerID, bool resaltar)
    {
        if (componentesAvatar.TryGetValue(playerID, out AvatarJugador avatar))
        {
            avatar.Resaltar(resaltar);
        }
    }

    public void ResaltarTodosLosAvatares(bool resaltar)
    {
        foreach (var avatar in componentesAvatar.Values)
        {
            if (avatar != null)
            {
                avatar.Resaltar(resaltar);
            }
        }
    }

    public void ResaltarJugadoresDisponibles(List<int> jugadoresDisponibles)
    {
        // Primero quitar resaltado de todos
        ResaltarTodosLosAvatares(false);
        
        // Luego resaltar solo los disponibles
        foreach (int playerID in jugadoresDisponibles)
        {
            ResaltarAvatar(playerID, true);
        }
    }

    public GameObject GetAvatar(int playerID)
    {
        avataresJugadores.TryGetValue(playerID, out GameObject avatar);
        return avatar;
    }

    public List<int> GetJugadoresDisponibles()
    {
        List<int> jugadores = new List<int>();
        foreach (var kvp in avataresJugadores)
        {
            jugadores.Add(kvp.Key);
        }
        return jugadores;
    }

    // Método para obtener la posición mundial del avatar
    public Vector3 GetPosicionAvatar(int playerID)
    {
        if (avataresJugadores.TryGetValue(playerID, out GameObject avatar) && avatar != null)
        {
            return avatar.transform.position;
        }
        return Vector3.zero;
    }

    // Método para debug
    [ContextMenu("Debug Estado Avatares")]
    public void DebugEstadoAvatares()
    {
        Debug.Log("=== DEBUG AVATARES ===");
        foreach (var kvp in avataresJugadores)
        {
            if (kvp.Value != null)
            {
                Debug.Log($"Jugador {kvp.Key}: {kvp.Value.name} - Parent: {kvp.Value.transform.parent?.name} - Posición: {kvp.Value.transform.position}");
            }
            else
            {
                Debug.Log($"Jugador {kvp.Key}: NULL");
            }
        }
    }
}