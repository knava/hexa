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
		
		// Agregar la carta a la lista primero
		cartasDescarte.Add(carta);
		
		// Luego reorganizar TODO el mazo
		ReorganizarMazoDescarte();
		
		// Configurar la carta
		carta.transform.SetParent(transform);
		carta.transform.localScale = new Vector3(1.2f, 1.6f, 2f);
		
		Carta3D cartaScript = carta.GetComponent<Carta3D>();
		if (cartaScript != null)
		{
			cartaScript.MostrarDorso();
			cartaScript.SetPuedeGirar(false);
			cartaScript.SetEnManoIA(false);
		}
		
		Debug.Log($"🗑️ Carta agregada al descarte. Total: {cartasDescarte.Count}");
	}

	// ✅ MÉTODO PARA REORGANIZAR TODO EL MAZO
	public void ReorganizarMazoDescarte()
	{
		for (int i = 0; i < cartasDescarte.Count; i++)
		{
			GameObject carta = cartasDescarte[i];
			if (carta != null)
			{
				carta.transform.SetParent(transform);
				carta.transform.localPosition = new Vector3(0f, 0f, i * 0.002f);
				carta.transform.localRotation = Quaternion.Euler(90, 0, 0);
				carta.transform.localScale = new Vector3(1.2f, 1.6f, 2f);
			}
		}
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
