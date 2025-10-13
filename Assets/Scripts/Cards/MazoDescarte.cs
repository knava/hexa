using System.Collections.Generic;
using UnityEngine;

public class MazoDescarte : MonoBehaviour
{
    public static MazoDescarte Instance;
    
    [Header("Configuración Descarte")]
    public Transform posicionDescarte;
    public float separacionCartas = 0.001f;
    public Material dorsoMaterial;
    
    [Header("Estado")]
    public List<GameObject> cartasDescarte = new List<GameObject>();
    
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
    
    public void AgregarCartaDescarte(GameObject carta)
    {
        if (carta == null) return;
        
        // Remover de cualquier mano anterior
        carta.transform.SetParent(transform);
        
        // Posicionar la carta en el mazo de descarte
        Vector3 posicion = posicionDescarte.position + new Vector3(0, cartasDescarte.Count * separacionCartas, 0);
        carta.transform.position = posicion;
        carta.transform.rotation = Quaternion.Euler(90, 0, 0);
        
        // Mostrar dorso
        Carta3D cartaScript = carta.GetComponent<Carta3D>();
        if (cartaScript != null)
        {
            cartaScript.MostrarDorso();
            cartaScript.SetPuedeGirar(false);
        }
        
        cartasDescarte.Add(carta);
        
        Debug.Log($"🗑️ Carta agregada al descarte. Total: {cartasDescarte.Count}");
    }
    
    public int CantidadCartasDescarte()
    {
        return cartasDescarte.Count;
    }
    
    // Método para debug
    [ContextMenu("Debug Estado Descarte")]
    public void DebugEstadoDescarte()
    {
        Debug.Log($"📊 Mazo Descarte: {cartasDescarte.Count} cartas");
    }
}
