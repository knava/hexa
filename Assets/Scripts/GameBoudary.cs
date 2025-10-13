using UnityEngine;

public class GameBoundary : MonoBehaviour
{
    public static GameBoundary Instance; // Singleton
    
    [Header("Settings")]
    public float boundaryRadius = 10f; // Radio del perímetro (ajustable en el Inspector)
    public LayerMask magnetLayer; // Capa de los imanes (mantenida por posible uso futuro)
    
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

    public void CheckBoundaries()
    {
        if (MagnetSystem.Instance == null)
        {
            #if UNITY_EDITOR
            Debug.LogError("MagnetSystem.Instance no está inicializado.");
            #endif
            return;
        }
        foreach (Transform magnet in MagnetSystem.Instance.allMagnets)
        {
            float distance = Vector3.Distance(transform.position, magnet.position);
            if (distance > boundaryRadius)
            {
                MagnetSystem.Instance.ForceDisableMagnet(magnet);
            }
        }
    }

    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, boundaryRadius);
    }
    #endif
}