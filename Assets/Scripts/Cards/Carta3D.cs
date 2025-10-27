using UnityEngine;

public class Carta3D : MonoBehaviour
{
    [Header("Materiales")]
    public Material dorsoMaterial;
    public Material frenteMaterial;
    
    [Header("Referencias")]
    public GameObject borde; // GameObject "Borde" para selección visual

    // Componentes y referencias
    private MeshRenderer meshRenderer;
    private MazoFisico mazoPadre;
    
    // Estados de la carta
    private bool bocaAbajo = true;
    private bool puedeGirar = true;
    private bool esRoboPorComer = false;
    private bool enManoIA = false;
    
    // Escalas para diferentes contextos
    private Vector3 escalaHumano = new Vector3(1.2f, 1.6f, 2f);
    private Vector3 escalaIA = new Vector3(0.84f, 1.12f, 1.4f);
    
    // Estado de selección
    private bool estaSeleccionada = false;
    
    /// <summary>
    /// Propiedad para controlar el estado de selección de la carta
    /// </summary>
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
        
        // Buscar automáticamente el borde si no está asignado
        if (borde == null)
        {
            borde = transform.Find("Borde")?.gameObject;
        }
        
        // Asegurar que la carta tiene collider
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
        
        // Inicializar con dorso visible
        MostrarDorso();
    }

    /// <summary>
    /// Establece la referencia al mazo padre
    /// </summary>
    public void SetMazoPadre(MazoFisico mazo)
    {
        mazoPadre = mazo;
    }
    
    /// <summary>
    /// Establece si la carta está en modo robo por comer
    /// </summary>
    public void SetEsRoboPorComer(bool estado)
    {
        esRoboPorComer = estado;
    }

    /// <summary>
    /// Maneja el clic del mouse en la carta
    /// </summary>
    void OnMouseDown()
    {
        // Verificar si está en modo selección de Dinamita
        ManoJugador mano = GetComponentInParent<ManoJugador>();
        if (mano != null && mano.seleccionDinamitaHabilitada)
        {
            mano.ProcesarClicCartaDinamita(gameObject);
            return;
        }
        
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
        
        // Lógica original de robo normal
        if (puedeGirar && mazoPadre != null && bocaAbajo)
        {
            mazoPadre.ProcesarClicCarta(gameObject);
        }
    }

    /// <summary>
    /// Verifica si la carta está en la mano del jugador humano (Jugador 1)
    /// </summary>
    private bool EstaEnManoJugador1()
    {
        ManoJugador mano = GetComponentInParent<ManoJugador>();
        if (mano != null)
        {
            return mano.playerID == 1 && !mano.esIA;
        }
        
        return false;
    }

    /// <summary>
    /// Actualiza la visibilidad del borde según el estado de selección
    /// </summary>
    private void ActualizarBorde()
    {
        if (borde != null)
        {
            borde.SetActive(estaSeleccionada);
        }
    }

    /// <summary>
    /// Gira la carta con animación
    /// </summary>
    public void GirarCartaConAnimacion()
    {
        if (puedeGirar)
        {
            puedeGirar = false;
            
            // Animación de giro en dos fases
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
                    
                    // Segunda fase de la animación
                    LeanTween.rotateY(gameObject, 0f, 0.5f)
                        .setOnComplete(() => {
                            bocaAbajo = !bocaAbajo;
                            puedeGirar = true;
                            
                            // Notificar al mazo cuando se completa el giro
                            if (mazoPadre != null && !bocaAbajo)
                            {
                                mazoPadre.AgregarCartaAMano(gameObject);
                            }
                        });
                });
        }
    }

    /// <summary>
    /// Muestra el dorso de la carta
    /// </summary>
    public void MostrarDorso()
    {
        bocaAbajo = true;
        if (meshRenderer != null && dorsoMaterial != null)
        {
            meshRenderer.material = dorsoMaterial;
        }
    }

    /// <summary>
    /// Muestra el frente de la carta
    /// </summary>
    public void MostrarFrente()
    {
        bocaAbajo = false;
        if (meshRenderer != null && frenteMaterial != null)
        {
            meshRenderer.material = frenteMaterial;
        }
    }

    /// <summary>
    /// Establece el material frontal de la carta
    /// </summary>
    public void SetFrenteMaterial(Material nuevoFrente)
    {
        frenteMaterial = nuevoFrente;
    }

    /// <summary>
    /// Habilita o deshabilita la capacidad de girar la carta
    /// </summary>
    public void SetPuedeGirar(bool estado)
    {
        puedeGirar = estado;
    }

    /// <summary>
    /// Obtiene el material frontal de la carta
    /// </summary>
    public Material GetFrenteMaterial()
    {
        return frenteMaterial;
    }
    
    /// <summary>
    /// Establece si la carta está en mano de IA y ajusta la escala
    /// </summary>
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
            transform.localScale = escalaHumano;
        }
    }

    /// <summary>
    /// Cambia la escala de la carta según el tipo de dueño
    /// </summary>
    public void CambiarEscala(bool nuevoEsIA)
    {
        if (nuevoEsIA == enManoIA)
        {
            // Si sigue siendo el mismo tipo, no cambiar escala
            return;
        }
        
        enManoIA = nuevoEsIA;
        
        // Aplicar la nueva escala con animación suave
        Vector3 nuevaEscala = nuevoEsIA ? escalaIA : escalaHumano;
        LeanTween.scale(gameObject, nuevaEscala, 0.3f)
            .setEase(LeanTweenType.easeOutBack);
    }
    
    /// <summary>
    /// Obtiene el tipo de carta basado en su material frontal
    /// </summary>
    public CardType GetTipoCarta()
    {
        if (MazoFisico.Instance == null) return CardType.Piedra;
        
        if (frenteMaterial == MazoFisico.Instance.frenteOro)
            return CardType.Oro;
        else if (frenteMaterial == MazoFisico.Instance.frenteOpalo)
            return CardType.Piedra;
        else if (frenteMaterial == MazoFisico.Instance.frenteDinamita)
            return CardType.Dinamita;
		else if (frenteMaterial == MazoFisico.Instance.frenteDiamante)
			return CardType.Diamante;
			else
			return CardType.Piedra;
    }
    
    /// <summary>
    /// Verifica si la carta es de acción (Dinamita)
    /// </summary>
    public bool EsCartaDeAccion()
    {
        return GetTipoCarta() == CardType.Dinamita || GetTipoCarta() == CardType.Diamante;
    }
    
    /// <summary>
    /// Deselecciona la carta
    /// </summary>
    public void Deseleccionar()
    {
        if (estaSeleccionada)
        {
            estaSeleccionada = false;
            ActualizarBorde();
        }
    }
    
    /// <summary>
    /// Obtiene si la carta está en mano de IA
    /// </summary>
    public bool GetEnManoIA()
    {
        return enManoIA;
    }
}