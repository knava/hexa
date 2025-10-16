using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class MazoFisico : MonoBehaviour
{
    public static MazoFisico Instance;
    
    [Header("Configuración de Cartas")]
    public GameObject cartaPrefab;
    public Material dorsoMaterial;
    public Material frenteOro;
    public Material frenteOpalo;
	public Material frenteDinamita;
    public int cartasEnMazo = 10;
    
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
	public Vector3[] posicionesManos; // Asigna posiciones para cada mano en el inspector
	
	[Header("Robo por Comer")]
	public bool roboPorComerHabilitado = false;
	private int jugadorAtacanteID;
	private int jugadorVictimaID;
	
	public Dictionary<int, ManoJugador> manosJugadores = new Dictionary<int, ManoJugador>();
	private Dictionary<int, Transform> centrosManos = new Dictionary<int, Transform>();

    // Listas de control
    private List<GameObject> cartasMazo = new List<GameObject>();
    private List<GameObject> cartasMano = new List<GameObject>();

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
        // Solo generar cartas si el mazo está activo
        if (gameObject.activeSelf)
        {
            GenerarMazo50_50();
        }
    }

    void OnEnable()
    {
        // Cuando se habilita el mazo, generar cartas si no existen
        if (cartasMazo.Count == 0)
        {
            GenerarMazo50_50();
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

    private void GenerarMazo50_50()
	{
		cartasMazo.Clear();
		
		List<Material> materialesCartas = new List<Material>();
		
		// Crear distribución 40% ORO, 40% PIEDRA, 20% DINAMITA
		for (int i = 0; i < cartasEnMazo; i++)
		{
			// Determinar el tipo de carta según porcentajes
			float probabilidad = Random.Range(0f, 100f);
			Material materialFrente;
			
			if (probabilidad < 40f)
			{
				materialFrente = frenteOro;        // 40% Oro
			}
			else if (probabilidad < 80f)
			{
				materialFrente = frenteOpalo;      // 40% Piedra
			}
			else
			{
				materialFrente = frenteDinamita;   // 20% Dinamita
			}
			
			materialesCartas.Add(materialFrente);
		}

		// Barajar las cartas
		materialesCartas = BarajarLista(materialesCartas);

		// Instanciar cartas
		for (int i = 0; i < cartasEnMazo; i++)
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

		Debug.Log($"Mazo generado con {cartasEnMazo} cartas (40% ORO, 40% PIEDRA, 20% DINAMITA)");
	}

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

    public void HabilitarRoboUnaCarta()
	{
		mazoHabilitado = true;
		cartaYaRobada = false;
		//Debug.Log("✅ Robo por CASILLA habilitado - Solo mazo");
		
		// Verificar si el jugador actual es IA
		if (GameManager.Instance != null && GameManager.Instance.IsCurrentPlayerAI())
		{
			//Debug.Log("🤖 IA detectada, iniciando robo automático por CASILLA...");
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

	private IEnumerator IniciarRoboAutomaticoIACoroutine()
	{
		// Pequeño delay para asegurar que el estado esté sincronizado
		yield return new WaitForSeconds(0.3f);
		
		if (GameManager.Instance != null && mazoHabilitado && !cartaYaRobada)
		{
			int currentPlayerID = GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].playerID;
			RobarCartaParaJugador(currentPlayerID);
		}
	}

    public void DeshabilitarRobo()
    {
        mazoHabilitado = false;
        cartaYaRobada = true;
        //Debug.Log("❌ Robo deshabilitado");
		
        // Deshabilitar interacción con todas las cartas
        foreach (GameObject carta in cartasMazo)
        {
            if (carta != null)
            {
                carta.GetComponent<Carta3D>().SetPuedeGirar(false);
            }
        }
        
    }

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
			// Obtener el jugador actual del GameManager
			int currentPlayerID = GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].playerID;
			
			if (manosJugadores.TryGetValue(currentPlayerID, out ManoJugador mano) && !mano.esIA)
			{
				// Para humano: iniciar animación de robo
				cartaYaRobada = true;
				carta.GetComponent<Carta3D>().GirarCartaConAnimacion();
				DeshabilitarRobo();
			}
			else
			{
				Debug.LogWarning("No se puede robar carta - jugador no encontrado o es IA");
			}
		}
	}

    public void AgregarCartaAMano(GameObject carta)
	{
		// ✅ Asegurar que la carta se remueve completamente del mazo
		if (cartasMazo.Contains(carta))
		{
			cartasMazo.Remove(carta);
			Debug.Log($"📋 Carta removida del mazo. Cartas restantes: {cartasMazo.Count}");
		}
		else
		{
			Debug.LogWarning("⚠️ La carta no estaba en cartasMazo");
		}

		// Obtener el jugador actual
		int currentPlayerID = GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].playerID;
		
		// Agregar a la mano del jugador correspondiente
		if (manosJugadores.TryGetValue(currentPlayerID, out ManoJugador mano))
		{
			// Actualizar escala si es necesario (cuando la carta viene del mazo)
			Carta3D cartaScript = carta.GetComponent<Carta3D>();
			if (cartaScript != null)
			{
				cartaScript.CambiarEscala(mano.esIA);
			}
			
			mano.AgregarCarta(carta);
			
			// ✅ VERIFICAR SI EL MAZO ESTÁ VACÍO después de agregar la carta
			VerificarYOcultarMazo();
			
			// Si es jugador humano, finalizar el robo después de agregar la carta
			if (!mano.esIA)
			{
				// Pequeño delay para que se complete la animación
				StartCoroutine(FinalizarRoboHumanoCoroutine());
			}
		}
		else
		{
			Debug.LogError($"No se encontró mano para el jugador {currentPlayerID}");
		}

		// Deshabilitar robo después de agarrar carta
		DeshabilitarRobo();
	}
	
	private void VerificarYOcultarMazo()
	{
		if (cartasMazo.Count == 0)
		{
			Debug.Log("🏁 MAZO VACÍO - Ocultando mazo...");
			
			// Desactivar todo el GameObject del mazo
			gameObject.SetActive(false);
			
			// ✅ NOTIFICAR AL GAME MANAGER QUE EL JUEGO TERMINÓ
			if (GameManager.Instance != null && !GameManager.Instance.juegoTerminado)
			{
				GameManager.Instance.FinDelJuego();
			}
		}
		else
		{
			Debug.Log($"📚 Mazo con {cartasMazo.Count} cartas restantes");
		}
	}

	private IEnumerator FinalizarRoboHumanoCoroutine()
	{
		// Esperar a que termine la animación de la carta
		yield return new WaitForSeconds(0.5f);
		
		// Finalizar el robo de carta
		if (GameManager.Instance != null)
		{
			GameManager.Instance.FinalizarRoboCarta();
		}
	}

    public GameObject GetCartaSuperior()
	{
		if (cartasMazo.Count == 0)
		{
			// ✅ Si no hay cartas, verificar y ocultar el mazo
			VerificarYOcultarMazo();
			return null;
		}
		return cartasMazo[cartasMazo.Count - 1];
	}

    private void ReorganizarMano()
    {
        if (centroMano == null || cartasMano.Count == 0) return;

        for (int i = 0; i < cartasMano.Count; i++)
        {
            GameObject carta = cartasMano[i];
            
            // Calcular posición en arco circular
            float angulo = CalculateCardAngle(i, cartasMano.Count);
            Vector3 posicion = CalculateCardPosition(angulo);
            Quaternion rotacion = CalculateCardRotation(angulo);

            // Animación suave a la nueva posición
            LeanTween.move(carta, posicion, 0.5f)
                .setEase(LeanTweenType.easeOutBack);
                
            LeanTween.rotate(carta, rotacion.eulerAngles, 0.5f)
                .setEase(LeanTweenType.easeOutBack);
        }
    }

    private float CalculateCardAngle(int index, int totalCards)
    {
        float anguloPorCarta = anguloAbanico / Mathf.Max(1, totalCards - 1);
        float anguloInicio = -anguloAbanico / 2f;
        return anguloInicio + (index * anguloPorCarta);
    }

    private Vector3 CalculateCardPosition(float angulo)
    {
        float x = Mathf.Sin(angulo * Mathf.Deg2Rad) * radioAbanico;
        float z = Mathf.Cos(angulo * Mathf.Deg2Rad) * radioAbanico;
        return centroMano.position + new Vector3(x, 0, z);
    }

    private Quaternion CalculateCardRotation(float angulo)
    {
        return Quaternion.Euler(90, angulo, 0);
    }

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

    public int CartasRestantesEnMazo()
    {
        return cartasMazo.Count;
    }

    public int CartasEnMano()
    {
        return cartasMano.Count;
    }

    // Métodos para debug
    [ContextMenu("Debug Habilitar Robo")]
    public void DebugHabilitarRobo()
    {
        HabilitarRoboUnaCarta();
    }

    [ContextMenu("Debug Deshabilitar Robo")]
    public void DebugDeshabilitarRobo()
    {
        DeshabilitarRobo();
    }

    [ContextMenu("Debug Estado Mazo")]
    public void DebugEstadoMazo()
    {
        Debug.Log($"Cartas en mazo: {CartasRestantesEnMazo()}, Cartas en mano: {CartasEnMano()}, Habilitado: {mazoHabilitado}");
    }
	public static void HabilitarRobo()
	{
		MazoFisico instance = FindObjectOfType<MazoFisico>(true); // Buscar en inactivos
		if (instance != null)
		{
			instance.gameObject.SetActive(true);
			instance.HabilitarRoboUnaCarta();
		}
		/*else
		{
			Debug.LogError("No se encontró MazoFisico en la escena");
		}*/
	}
	
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

	public void RobarCartaParaJugador(int playerID)
	{
		if (!mazoHabilitado || cartaYaRobada) return;

		GameObject cartaSuperior = GetCartaSuperior();
		if (cartaSuperior == null)
		{
			Debug.LogWarning("⚠️ No hay cartas en el mazo para robar");
			return;
		}

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
				Debug.Log($"Jugador IA {playerID} robó una carta. Restantes: {cartasMazo.Count}");
				
				// ✅ VERIFICAR SI EL MAZO ESTÁ VACÍO
				VerificarYOcultarMazo();
				
				// IMPORTANTE: Finalizar inmediatamente el robo para IA
				DeshabilitarRobo();
				
				if (GameManager.Instance != null)
				{
					// Llamar con delay para asegurar que todo se procese
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
	
	public void HabilitarRoboPorComer(int atacanteID, int victimaID)
	{
		// Verificar que los jugadores existen
		if (!manosJugadores.ContainsKey(atacanteID) || !manosJugadores.ContainsKey(victimaID))
		{
			//Debug.LogError($"❌ Jugadores no encontrados: Atacante {atacanteID}, Víctima {victimaID}");
			GameManager.Instance?.FinalizarRoboPorComer();
			return;
		}

		roboPorComerHabilitado = true;
		jugadorAtacanteID = atacanteID;
		jugadorVictimaID = victimaID;
		
		//Debug.Log($"🎯 Habilitando robo por comer: Jugador {atacanteID} puede robar de {victimaID}");
		
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
		else
		{
			Debug.LogWarning("⚠️ No hay cartas en el mazo");
		}
		
		// Habilitar cartas de la mano del jugador víctima
		HabilitarCartasManoVictima();
		
		if (manosJugadores.TryGetValue(atacanteID, out ManoJugador manoAtacante))
		{
			if (manoAtacante.esIA)
			{
				//Debug.Log($"🤖 IA {atacanteID} detectada, decidiendo automáticamente...");
				
				// Pequeño delay para que se habiliten las cartas visualmente
				Invoke("ForzarDecisionIA", 0.5f);
			}
		}
	}

	// Método para habilitar cartas de la mano de la víctima
	private void HabilitarCartasManoVictima()
	{
		if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictima))
		{
			if (manoVictima.CantidadCartas > 0)
			{
				manoVictima.HabilitarCartasParaRobo();
				//Debug.Log($"🃏 Habilitadas {manoVictima.CantidadCartas()} cartas del jugador {jugadorVictimaID}");
			}
			else
			{
				Debug.LogWarning($"⚠️ Jugador {jugadorVictimaID} no tiene cartas en la mano");
			}
		}
		else
		{
			Debug.LogError($"❌ No se encontró mano para el jugador {jugadorVictimaID}");
		}
	}
	
	private void ProcesarRoboPorComer(GameObject carta)
	{
		if (!roboPorComerHabilitado) return;
		
		Carta3D cartaScript = carta?.GetComponent<Carta3D>();
		if (cartaScript == null)
		{
			return;
		}
		
		try
		{
			// Verificar si la carta es del mazo
			if (cartasMazo.Contains(carta))
			{
				Debug.Log($"🎯 Jugador {jugadorAtacanteID} robó carta del mazo");
				RobarCartaDelMazo(carta);
			}
			else
			{
				// Verificar si la carta es de la mano de la víctima
				if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictima))
				{
					if (manoVictima.ContieneCarta(carta))
					{
						Debug.Log($"🎯 Jugador {jugadorAtacanteID} robó carta del jugador {jugadorVictimaID}");
						RobarCartaDeMano(carta);
						
						// ✅ CORRECCIÓN ADICIONAL: Reorganizar también aquí por si acaso
						manoVictima.ReorganizarMano();
					}
					else
					{
						Debug.LogWarning("⚠️ La carta no pertenece a la víctima");
						return;
					}
				}
			}
			
			// Deshabilitar todas las cartas después de robar
			DeshabilitarRoboPorComer();
			
			// ✅ CORRECCIÓN: Asegurar reorganización final
			if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictimaFinal))
			{
				manoVictimaFinal.ReorganizarMano();
			}
			
			// Finalizar el robo
			GameManager.Instance?.FinalizarRoboPorComer();
		}
		catch (System.Exception e)
		{
			Debug.LogError($"❌ Error en ProcesarRoboPorComer: {e.Message}");
			DeshabilitarRoboPorComer();
			GameManager.Instance?.FinalizarRoboPorComer();
		}
	}

	// Método para robar carta del mazo
	private void RobarCartaDelMazo(GameObject carta)
	{
		// ✅ Asegurar remoción completa
		if (cartasMazo.Contains(carta))
		{
			cartasMazo.Remove(carta);
			Debug.Log($"🎯 Carta robada del mazo. Restantes: {cartasMazo.Count}");
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
			
			manoAtacante.AgregarCarta(carta); // ✅ ESTE se reorganiza automáticamente
			
			// ✅ CORRECCIÓN: REORGANIZAR LA MANO DEL JUGADOR COMIDO
			if (manosJugadores.TryGetValue(jugadorVictimaID, out ManoJugador manoVictima))
			{
				manoVictima.ReorganizarMano();
				Debug.Log($"🔄 Reorganizada mano del jugador comido {jugadorVictimaID}");
			}
			
			// ✅ VERIFICAR SI EL MAZO ESTÁ VACÍO
			VerificarYOcultarMazo();
		}
	}

	// Método para robar carta de la mano de la víctima
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
			
			// ✅ CORRECCIÓN: Reorganizar la mano de la víctima inmediatamente
			manoVictima.ReorganizarMano();
			
			// Actualizar escala solo si cambia el tipo de dueño (IA->Humano o Humano->IA)
			Carta3D cartaScript = carta.GetComponent<Carta3D>();
			if (cartaScript != null && victimaEsIA != atacanteEsIA)
			{
				cartaScript.CambiarEscala(atacanteEsIA);
			}
			
			// Agregar carta a la mano del atacante
			manoAtacante.AgregarCarta(carta);
			
			Debug.Log($"✅ Carta robada de jugador {jugadorVictimaID} a jugador {jugadorAtacanteID}");
		}
	}

	// Método para deshabilitar robo por comer
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
}