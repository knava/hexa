using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class MazoFisico : MonoBehaviour
{
    public static MazoFisico Instance;
    
    [Header("Configuración de Cartas")]
    public GameObject cartaPrefab;
    public Material dorsoMaterial;
    public Material frenteOro;
    public Material frenteOpalo;
    public Material frenteDinamita;
	public Material frenteDiamante;
	
	[Header("Cantidad Exacta de Cartas por Tipo")]
	public int cantidadOro = 4;
	public int cantidadPiedra = 4;
	public int cantidadDinamita = 1;
	public int cantidadDiamante = 1;
    
    [Header("Posición en Mano")]
    public Transform centroMano;
    public float radioAbanico = 1.0f;
    public float anguloAbanico = 60f;

    [Header("Estado del Mazo")]
    public bool mazoHabilitado = false;
    public bool cartaYaRobada = false;
    
    [Header("Manos de Jugadores")]
    public GameObject prefabManoJugador;
    public Transform contenedorManos;
    public Vector3[] posicionesManos;
    
    [Header("Robo por Comer")]
    public bool roboPorComerHabilitado = false;
    private int jugadorAtacanteID;
    private int jugadorVictimaID;
    
    // Diccionarios de gestión
    public Dictionary<int, ManoJugador> manosJugadores = new Dictionary<int, ManoJugador>();
    private Dictionary<int, Transform> centrosManos = new Dictionary<int, Transform>();
    private List<GameObject> cartasMazo = new List<GameObject>();

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

    void OnEnable()
    {
        // Cuando se habilita el mazo, generar cartas si no existen
        if (cartasMazo.Count == 0)
        {
            GenerarMazoConfigurable();
        }
        else
        {
            // Si ya hay cartas, asegurarse de que estén activas
            ReactivarCartasMazo();
        }
    }

    void OnDisable()
    {
        // Cuando se deshabilita el mazo, resetear estado
        mazoHabilitado = false;
        cartaYaRobada = false;
    }
	
    /// <summary>
	/// Genera el mazo con cantidades exactas configuradas desde el Inspector
	/// </summary>
	private void GenerarMazoConfigurable()
	{
		// Validar configuración primero
		if (!ValidarConfiguracionMazo())
		{
			Debug.LogError("❌ No se puede generar el mazo - Configuración inválida");
			return;
		}
		
		cartasMazo.Clear();
		
		List<Material> materialesCartas = new List<Material>();
		
		// Agregar cartas según las cantidades configuradas
		AgregarCartasPorTipo(materialesCartas, frenteOro, cantidadOro, "Oro");
		AgregarCartasPorTipo(materialesCartas, frenteOpalo, cantidadPiedra, "Piedra");
		AgregarCartasPorTipo(materialesCartas, frenteDinamita, cantidadDinamita, "Dinamita");
		AgregarCartasPorTipo(materialesCartas, frenteDiamante, cantidadDiamante, "Diamante");
		
		// Calcular total de cartas
		int totalCartas = cantidadOro + cantidadPiedra + cantidadDinamita + cantidadDiamante;
		
		Debug.Log($"🃏 Generando mazo configurado - Total: {totalCartas} cartas");

		// Barajar las cartas
		materialesCartas = BarajarLista(materialesCartas);

		// Instanciar cartas
		for (int i = 0; i < totalCartas; i++)
		{
			Vector3 posicion = transform.position + new Vector3(i * 0.001f, 0, 0);
			GameObject carta = Instantiate(cartaPrefab, posicion, Quaternion.identity, transform);
			carta.transform.rotation = Quaternion.Euler(90, 0, 0);
			
			Carta3D cartaScript = carta.GetComponent<Carta3D>();
			cartaScript.SetFrenteMaterial(materialesCartas[i]);
			cartaScript.MostrarDorso();
			cartaScript.SetMazoPadre(this);
			cartaScript.SetPuedeGirar(false);
			
			cartasMazo.Add(carta);
		}
		
		Debug.Log($"✅ Mazo generado con {totalCartas} cartas exactas");
	}

	/// <summary>
	/// Método auxiliar para agregar cartas de un tipo específico
	/// </summary>
	private void AgregarCartasPorTipo(List<Material> materiales, Material material, int cantidad, string tipoNombre)
	{
		for (int i = 0; i < cantidad; i++)
		{
			materiales.Add(material);
		}
		Debug.Log($"   - {tipoNombre}: {cantidad} cartas");
	}
	
	/// <summary>
	/// Valida que la configuración del mazo sea correcta
	/// </summary>
	private bool ValidarConfiguracionMazo()
	{
		bool configuracionValida = true;
		
		if (cantidadOro < 0)
		{
			Debug.LogError("❌ Cantidad de Oro no puede ser negativa");
			configuracionValida = false;
		}
		
		if (cantidadPiedra < 0)
		{
			Debug.LogError("❌ Cantidad de Piedra no puede ser negativa");
			configuracionValida = false;
		}
		
		if (cantidadDinamita < 0)
		{
			Debug.LogError("❌ Cantidad de Dinamita no puede ser negativa");
			configuracionValida = false;
		}
		
		if (cantidadDiamante < 0)
		{
			Debug.LogError("❌ Cantidad de Diamante no puede ser negativa");
			configuracionValida = false;
		}
		
		int total = cantidadOro + cantidadPiedra + cantidadDinamita + cantidadDiamante;
		if (total <= 0)
		{
			Debug.LogError("❌ El mazo debe tener al menos una carta");
			configuracionValida = false;
		}
		
		if (total > 50) // Límite razonable
		{
			Debug.LogWarning("⚠️ El mazo tiene muchas cartas (" + total + "), puede afectar el rendimiento");
		}
		
		return configuracionValida;
	}

    /// <summary>
    /// Reactiva todas las cartas del mazo
    /// </summary>
    private void ReactivarCartasMazo()
    {
        foreach (GameObject carta in cartasMazo)
        {
            if (carta != null)
            {
                carta.SetActive(true);
                carta.GetComponent<Carta3D>().SetPuedeGirar(mazoHabilitado);
            }
        }
    }

    /// <summary>
    /// Habilita el robo de una carta del mazo
    /// </summary>
    public void HabilitarRoboUnaCarta()
    {
        mazoHabilitado = true;
        cartaYaRobada = false;
        
        // Verificar si el jugador actual es IA
        if (GameManager.Instance != null && GameManager.Instance.IsCurrentPlayerAI())
        {
            StartCoroutine(IniciarRoboAutomaticoIACoroutine());
        }
        else
        {
            // Habilitar interacción solo con la carta superior para humanos
            GameObject cartaSuperior = GetCartaSuperior();
            if (cartaSuperior != null)
            {
                cartaSuperior.GetComponent<Carta3D>().SetPuedeGirar(true);
                
                // Feedback visual
                LeanTween.moveY(cartaSuperior, cartaSuperior.transform.position.y + 0.1f, 0.3f)
                    .setEase(LeanTweenType.easeOutBack);
            }
        }
    }

    /// <summary>
    /// Inicia el robo automático para IA
    /// </summary>
    private IEnumerator IniciarRoboAutomaticoIACoroutine()
    {
        yield return new WaitForSeconds(0.3f);
        
        if (GameManager.Instance != null && mazoHabilitado && !cartaYaRobada)
        {
            int currentPlayerID = GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].playerID;
            RobarCartaParaJugador(currentPlayerID);
        }
    }

    /// <summary>
    /// Deshabilita el robo de cartas
    /// </summary>
    public void DeshabilitarRobo()
    {
        mazoHabilitado = false;
        cartaYaRobada = true;
        
        // Deshabilitar interacción con todas las cartas
        foreach (GameObject carta in cartasMazo)
        {
            if (carta != null)
            {
                carta.GetComponent<Carta3D>().SetPuedeGirar(false);
            }
        }
    }

    /// <summary>
    /// Procesa el clic en una carta
    /// </summary>
    public void ProcesarClicCarta(GameObject carta)
    {
        if (roboPorComerHabilitado)
        {
            ProcesarRoboPorComer(carta);
            return;
        }
        if (!mazoHabilitado || cartaYaRobada) return;

        // Solo permitir robar la carta superior para humanos
        if (cartasMazo.Contains(carta) && carta == GetCartaSuperior())
        {
            int currentPlayerID = GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].playerID;
            
            if (manosJugadores.TryGetValue(currentPlayerID, out ManoJugador mano) && !mano.esIA)
            {
                // Para humano: iniciar animación de robo
                cartaYaRobada = true;
                carta.GetComponent<Carta3D>().GirarCartaConAnimacion();
                DeshabilitarRobo();
            }
        }
    }

    /// <summary>
    /// Agrega una carta a la mano del jugador actual
    /// </summary>
    public void AgregarCartaAMano(GameObject carta)
    {
        // Asegurar que la carta se remueve completamente del mazo
        if (cartasMazo.Contains(carta))
        {
            cartasMazo.Remove(carta);
        }

        // Obtener el jugador actual
        int currentPlayerID = GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].playerID;
        
        // Agregar a la mano del jugador correspondiente
        if (manosJugadores.TryGetValue(currentPlayerID, out ManoJugador mano))
        {
            // Actualizar escala si es necesario
            Carta3D cartaScript = carta.GetComponent<Carta3D>();
            if (cartaScript != null)
            {
                cartaScript.CambiarEscala(mano.esIA);
            }
            
            mano.AgregarCarta(carta);
            
            // Verificar si el mazo está vacío después de agregar la carta
            VerificarYOcultarMazo();
            
            // Si es jugador humano, finalizar el robo después de agregar la carta
            if (!mano.esIA)
            {
                StartCoroutine(FinalizarRoboHumanoCoroutine());
            }
        }

        // Deshabilitar robo después de agarrar carta
        DeshabilitarRobo();
    }
    
    /// <summary>
    /// Verifica si el mazo está vacío y lo oculta
    /// </summary>
    private void VerificarYOcultarMazo()
    {
        if (cartasMazo.Count == 0)
        {
            // Desactivar todo el GameObject del mazo
            gameObject.SetActive(false);
            
            // Notificar al Game Manager que el juego terminó
            if (GameManager.Instance != null && !GameManager.Instance.juegoTerminado)
            {
                GameManager.Instance.FinDelJuego();
            }
        }
    }

    /// <summary>
    /// Finaliza el robo para jugador humano
    /// </summary>
    private IEnumerator FinalizarRoboHumanoCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        
        // Finalizar el robo de carta
        if (GameManager.Instance != null)
        {
            GameManager.Instance.FinalizarRoboCarta();
        }
    }

    /// <summary>
    /// Obtiene la carta superior del mazo
    /// </summary>
    public GameObject GetCartaSuperior()
    {
        if (cartasMazo.Count == 0)
        {
            // Si no hay cartas, verificar y ocultar el mazo
            VerificarYOcultarMazo();
            return null;
        }
        return cartasMazo[cartasMazo.Count - 1];
    }

    /// <summary>
    /// Baraja una lista de materiales
    /// </summary>
    private List<Material> BarajarLista(List<Material> lista)
    {
        for (int i = 0; i < lista.Count; i++)
        {
            int randomIndex = Random.Range(i, lista.Count);
            Material temp = lista[i];
            lista[i] = lista[randomIndex];
            lista[randomIndex] = temp;
        }
        return lista;
    }

    /// <summary>
    /// Obtiene la cantidad de cartas restantes en el mazo
    /// </summary>
    public int CartasRestantesEnMazo()
    {
        return cartasMazo.Count;
    }

    /// <summary>
    /// Habilita el mazo para robo
    /// </summary>
    public static void HabilitarRobo()
    {
        MazoFisico instance = FindObjectOfType<MazoFisico>(true);
        if (instance != null)
        {
            instance.gameObject.SetActive(true);
            instance.HabilitarRoboUnaCarta();
        }
    }
    
    /// <summary>
    /// Inicializa las manos de los jugadores
    /// </summary>
    public void InicializarManosJugadores(int cantidadJugadores, int cantidadIA)
    {
        // Limpiar manos existentes
        foreach (Transform child in contenedorManos)
        {
            Destroy(child.gameObject);
        }
        manosJugadores.Clear();
        centrosManos.Clear();

        for (int i = 1; i <= cantidadJugadores; i++)
        {
            GameObject manoObj = Instantiate(prefabManoJugador, contenedorManos);
            manoObj.name = $"Mano_Jugador_{i}";
            
            // Posicionar la mano según el índice
            if (i - 1 < posicionesManos.Length)
            {
                manoObj.transform.position = posicionesManos[i - 1];
            }

            ManoJugador mano = manoObj.GetComponent<ManoJugador>();
            mano.playerID = i;
            mano.esIA = (i > (cantidadJugadores - cantidadIA));
            
            manosJugadores.Add(i, mano);
            centrosManos.Add(i, mano.centroMano);
        }
    }

    /// <summary>
    /// Roba una carta para un jugador específico
    /// </summary>
    public void RobarCartaParaJugador(int playerID)
    {
        if (!mazoHabilitado || cartaYaRobada) return;

        GameObject cartaSuperior = GetCartaSuperior();
        if (cartaSuperior == null) return;

        if (manosJugadores.TryGetValue(playerID, out ManoJugador mano))
        {
            cartaYaRobada = true;
            
            if (mano.esIA)
            {
                // Para IA: robo automático sin animación
                if (cartasMazo.Contains(cartaSuperior))
                {
                    cartasMazo.Remove(cartaSuperior);
                }
                mano.AgregarCarta(cartaSuperior);
                
                // Verificar si el mazo está vacío
                VerificarYOcultarMazo();
                
                // Finalizar inmediatamente el robo para IA
                DeshabilitarRobo();
                
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.Invoke("FinalizarRoboCarta", 0.1f);
                }
            }
            else
            {
                // Para humano: animación normal
                cartaSuperior.GetComponent<Carta3D>().GirarCartaConAnimacion();
            }
        }
    }
    
    /// <summary>
    /// Habilita el robo por comer entre jugadores
    /// </summary>
    public void HabilitarRoboPorComer(int atacanteID, int victimaID)
    {
        // Verificar que los jugadores existen
        if (!manosJugadores.ContainsKey(atacanteID) || !manosJugadores.ContainsKey(victimaID))
        {
            GameManager.Instance?.FinalizarRoboPorComer();
            return;
        }

        roboPorComerHabilitado = true;
        jugadorAtacanteID = atacanteID;
        jugadorVictimaID = victimaID;
        
        // Habilitar carta superior del mazo
        GameObject cartaSuperior = GetCartaSuperior();
        if (cartaSuperior != null)
        {
            Carta3D cartaScript = cartaSuperior.GetComponent<Carta3D>();
            if (cartaScript != null)
            {
                cartaScript.SetPuedeGirar(true);
                cartaScript.SetEsRoboPorComer(true);
                
                // Feedback visual
                LeanTween.moveY(cartaSuperior, cartaSuperior.transform.position.y + 0.1f, 0.3f)
                    .setEase(LeanTweenType.easeOutBack);
            }
        }
        
        // Habilitar cartas de la mano del jugador víctima
        HabilitarCartasManoVictima();
        
        if (manosJugadores.TryGetValue(atacanteID, out ManoJugador manoAtacante))
        {
            if (manoAtacante.esIA)
            {
                // Pequeño delay para que se habiliten las cartas visualmente
                Invoke("ForzarDecisionIA", 0.5f);
            }
        }
    }

    /// <summary>
    /// Habilita las cartas de la mano de la víctima para robo
    /// </summary>
    private void HabilitarCartasManoVictima()
    {
        if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictima))
        {
            if (manoVictima.CantidadCartas > 0)
            {
                manoVictima.HabilitarCartasParaRobo();
            }
        }
    }
    
    /// <summary>
    /// Procesa el robo por comer
    /// </summary>
    private void ProcesarRoboPorComer(GameObject carta)
    {
        if (!roboPorComerHabilitado) return;
        
        Carta3D cartaScript = carta?.GetComponent<Carta3D>();
        if (cartaScript == null) return;
        
        try
        {
            // Verificar si la carta es del mazo
            if (cartasMazo.Contains(carta))
            {
                RobarCartaDelMazo(carta);
            }
            else
            {
                // Verificar si la carta es de la mano de la víctima
                if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictima))
                {
                    if (manoVictima.ContieneCarta(carta))
                    {
                        RobarCartaDeMano(carta);
                        manoVictima.ReorganizarMano();
                    }
                    else
                    {
                        return;
                    }
                }
            }
            
            // Deshabilitar todas las cartas después de robar
            DeshabilitarRoboPorComer();
            
            // Asegurar reorganización final
            if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictimaFinal))
            {
                manoVictimaFinal.ReorganizarMano();
            }
            
            // Finalizar el robo
            GameManager.Instance?.FinalizarRoboPorComer();
        }
        catch (System.Exception)
        {
            DeshabilitarRoboPorComer();
            GameManager.Instance?.FinalizarRoboPorComer();
        }
    }

    /// <summary>
    /// Roba una carta del mazo durante robo por comer
    /// </summary>
    private void RobarCartaDelMazo(GameObject carta)
    {
        // Asegurar remoción completa
        if (cartasMazo.Contains(carta))
        {
            cartasMazo.Remove(carta);
        }
        
        if (manosJugadores.TryGetValue(jugadorAtacanteID, out ManoJugador manoAtacante))
        {
            // Determinar si el nuevo dueño es IA
            bool nuevoDueñoEsIA = manoAtacante.esIA;
            
            // Actualizar escala de la carta
            Carta3D cartaScript = carta.GetComponent<Carta3D>();
            if (cartaScript != null)
            {
                cartaScript.CambiarEscala(nuevoDueñoEsIA);
                cartaScript.MostrarFrente();
            }
            
            manoAtacante.AgregarCarta(carta);
            
            // Reorganizar la mano del jugador comido
            if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictima))
            {
                manoVictima.ReorganizarMano();
            }
            
            // Verificar si el mazo está vacío
            VerificarYOcultarMazo();
        }
    }

    /// <summary>
    /// Roba una carta de la mano de la víctima
    /// </summary>
    private void RobarCartaDeMano(GameObject carta)
    {
        if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictima) &&
            manosJugadores.TryGetValue(jugadorAtacanteID, out ManoJugador manoAtacante))
        {
            // Determinar los tipos de dueño
            bool victimaEsIA = manoVictima.esIA;
            bool atacanteEsIA = manoAtacante.esIA;
            
            // Remover carta de la mano de la víctima
            manoVictima.RemoverCarta(carta);
            manoVictima.ReorganizarMano();
            
            // Actualizar escala solo si cambia el tipo de dueño
            Carta3D cartaScript = carta.GetComponent<Carta3D>();
            if (cartaScript != null && victimaEsIA != atacanteEsIA)
            {
                cartaScript.CambiarEscala(atacanteEsIA);
            }
            
            // Agregar carta a la mano del atacante
            manoAtacante.AgregarCarta(carta);
        }
    }

    /// <summary>
    /// Deshabilita el robo por comer
    /// </summary>
    private void DeshabilitarRoboPorComer()
    {
        roboPorComerHabilitado = false;
        
        // Deshabilitar carta del mazo
        GameObject cartaSuperior = GetCartaSuperior();
        if (cartaSuperior != null)
        {
            cartaSuperior.GetComponent<Carta3D>().SetPuedeGirar(false);
            cartaSuperior.GetComponent<Carta3D>().SetEsRoboPorComer(false);
        }
        
        // Deshabilitar cartas de la mano de la víctima
        if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictima))
        {
            manoVictima.DeshabilitarCartasParaRobo();
        }
    }
    
    /// <summary>
    /// Fuerza la decisión de la IA durante robo por comer
    /// </summary>
    private void ForzarDecisionIA()
    {
        if (!roboPorComerHabilitado) return;
        
        // Buscar el AIController del jugador atacante
        if (GameManager.Instance != null && GameManager.Instance.jugadorQueComio != null)
        {
            AIController aiController = GameManager.Instance.jugadorQueComio.GetComponent<AIController>();
            if (aiController != null)
            {
                aiController.DecidirRoboPorComer();
            }
        }
    }
    
    /// <summary>
    /// Verifica si un jugador tiene carta de dinamita
    /// </summary>
    public bool TieneCartaDinamita(int playerID)
    {
        if (manosJugadores.TryGetValue(playerID, out ManoJugador mano))
        {
            foreach (GameObject carta in mano.GetCartas())
            {
                Carta3D cartaScript = carta.GetComponent<Carta3D>();
                if (cartaScript != null && cartaScript.GetTipoCarta() == CardType.Dinamita)
                {
                    return true;
                }
            }
        }
        return false;
    }
}