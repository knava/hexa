using UnityEngine;

public class Carta3D : MonoBehaviour
{
    [Header("Materiales")]
    public Material dorsoMaterial;
    public Material frenteMaterial;
    
    [Header("Referencias")]
    public GameObject borde; // Asignar desde el inspector el GameObject "Borde"

    private MeshRenderer meshRenderer;
    private bool bocaAbajo = true;
    private bool puedeGirar = true;
    private MazoFisico mazoPadre;
    private bool esRoboPorComer = false;
    private bool enManoIA = false;
    private Vector3 escalaHumano = new Vector3(1.2f, 1.6f, 2f);
    private Vector3 escalaIA = new Vector3(0.84f, 1.12f, 1.4f);
    
    private bool estaSeleccionada = false;
    public bool EstaSeleccionada 
    { 
        get { return estaSeleccionada; } 
        set { 
            estaSeleccionada = value; 
            ActualizarBorde();
        } 
    }

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        
        // Si no se asignó el borde desde el inspector, buscarlo automáticamente
        if (borde == null)
        {
            borde = transform.Find("Borde")?.gameObject;
            if (borde == null)
            {
                Debug.LogWarning("⚠️ No se encontró GameObject 'Borde' en los hijos");
            }
        }
        
        // Asegurar collider
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
        
        // Inicializar con dorso visible
        MostrarDorso();
    }

    public void SetMazoPadre(MazoFisico mazo)
    {
        mazoPadre = mazo;
    }
    
    public void SetEsRoboPorComer(bool estado)
    {
        esRoboPorComer = estado;
    }

    void OnMouseDown()
	{
		Debug.Log("Clic detectado en carta: " + gameObject.name);
		
		// Verificar si está en modo selección de Dinamita
		ManoJugador mano = GetComponentInParent<ManoJugador>();
		if (mano != null && mano.seleccionDinamitaHabilitada)
		{
			mano.ProcesarClicCartaDinamita(gameObject);
			return;
		}
		
		// Lógica existente para otros casos...
		bool esJugadorHumano = EstaEnManoJugador1() && !enManoIA;
		
		if (esJugadorHumano)
		{
			// Usar el sistema de selección única del ManoJugador
			if (mano != null)
			{
				mano.SeleccionarCarta(gameObject);
			}
			return;
		}
		
		if (esRoboPorComer)
		{
			// Delegar al sistema de robo por comer
			if (mazoPadre != null)
			{
				mazoPadre.ProcesarClicCarta(gameObject);
			}
			return;
		}
		
		if (puedeGirar && mazoPadre != null && bocaAbajo)
		{
			// Lógica original de robo normal
			mazoPadre.ProcesarClicCarta(gameObject);
		}
	}

    private bool EstaEnManoJugador1()
    {
        ManoJugador mano = GetComponentInParent<ManoJugador>();
        if (mano != null)
        {
            Debug.Log($"✅ Carta en mano del jugador {mano.playerID} - Es Jugador 1: {mano.playerID == 1}");
            return mano.playerID == 1 && !mano.esIA;
        }
        
        Debug.Log("❌ No se encontró componente ManoJugador en parents");
        return false;
    }

    private void ActualizarBorde()
    {
        if (borde != null)
        {
            borde.SetActive(estaSeleccionada);
            Debug.Log($"{(estaSeleccionada ? "✅" : "❌")} Borde {(estaSeleccionada ? "activado" : "desactivado")}");
        }
    }

    public void GirarCartaConAnimacion()
    {
        if (puedeGirar)
        {
            puedeGirar = false;
            Debug.Log("Iniciando animación de giro...");
            
            LeanTween.rotateY(gameObject, 90f, 0.5f)
                .setOnComplete(() => {
                    // Cambiar material durante el giro
                    if (!bocaAbajo)
                    {
                        meshRenderer.material = frenteMaterial;
                    }
                    else
                    {
                        meshRenderer.material = dorsoMaterial;
                    }
                    
                    LeanTween.rotateY(gameObject, 0f, 0.5f)
                        .setOnComplete(() => {
                            bocaAbajo = !bocaAbajo;
                            puedeGirar = true;
                            
                            if (mazoPadre != null && !bocaAbajo)
                            {
                                mazoPadre.AgregarCartaAMano(gameObject);
                            }
                        });
                });
        }
    }

    public void MostrarDorso()
    {
        bocaAbajo = true;
        if (meshRenderer != null && dorsoMaterial != null)
        {
            meshRenderer.material = dorsoMaterial;
        }
    }

    public void MostrarFrente()
    {
        bocaAbajo = false;
        if (meshRenderer != null && frenteMaterial != null)
        {
            meshRenderer.material = frenteMaterial;
        }
    }

    public void SetFrenteMaterial(Material nuevoFrente)
    {
        frenteMaterial = nuevoFrente;
    }

    public void SetPuedeGirar(bool estado)
    {
        puedeGirar = estado;
    }

    public Material GetFrenteMaterial()
    {
        return frenteMaterial;
    }
    
    public void SetEnManoIA(bool esIA)
	{
		enManoIA = esIA;
		
		// Actualizar escala inmediatamente
		if (esIA)
		{
			transform.localScale = escalaIA;
		}
		else
		{
			// ✅ Usar escala estándar cuando no está en mano de IA
			transform.localScale = escalaHumano;
		}
		
		Debug.Log($"🤖 Carta asignada a {(esIA ? "IA" : "Humano")} - Escala: {transform.localScale}");
	}

    public void CambiarEscala(bool nuevoEsIA)
    {
        if (nuevoEsIA == enManoIA)
        {
            // Si sigue siendo el mismo tipo (IA->IA o Humano->Humano), no cambiar escala
            return;
        }
        
        enManoIA = nuevoEsIA;
        
        // Aplicar la nueva escala con animación suave
        Vector3 nuevaEscala = nuevoEsIA ? escalaIA : escalaHumano;
        LeanTween.scale(gameObject, nuevaEscala, 0.3f)
            .setEase(LeanTweenType.easeOutBack);
    }
    
    public CardType GetTipoCarta()
    {
        if (MazoFisico.Instance == null) return CardType.Piedra;
        
        if (frenteMaterial == MazoFisico.Instance.frenteOro)
            return CardType.Oro;
        else if (frenteMaterial == MazoFisico.Instance.frenteOpalo)
            return CardType.Piedra;
        else if (frenteMaterial == MazoFisico.Instance.frenteDinamita)
            return CardType.Dinamita;
        else
            return CardType.Piedra; // Por defecto
    }
    
    public bool EsCartaDeAccion()
    {
        return GetTipoCarta() == CardType.Dinamita;
    }
    
    public void Deseleccionar()
	{
		if (estaSeleccionada)
		{
			estaSeleccionada = false;
			ActualizarBorde();
			Debug.Log($"🔴 Carta {gameObject.name} deseleccionada");
		}
	}
    
    // Método público para diagnóstico
    public bool GetEnManoIA()
    {
        return enManoIA;
    }
    
    // Método para debug
    [ContextMenu("Debug Estado Carta")]
    public void DebugEstadoCarta()
    {
        Debug.Log($"🃏 Estado Carta {gameObject.name}:");
        Debug.Log($"- Seleccionada: {estaSeleccionada}");
        Debug.Log($"- BocaAbajo: {bocaAbajo}");
        Debug.Log($"- EnManoIA: {enManoIA}");
        Debug.Log($"- Tipo: {GetTipoCarta()}");
        Debug.Log($"- Es Acción: {EsCartaDeAccion()}");
        Debug.Log($"- Parent: {transform.parent?.name}");
        Debug.Log($"- Borde: {(borde != null ? "Asignado" : "Null")}");
    }
}