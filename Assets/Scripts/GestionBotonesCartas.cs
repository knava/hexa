using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GestionBotonesCartas : MonoBehaviour
{
    public static GestionBotonesCartas Instance;
    
    [Header("Referencias UI")]
    public Button botonUtilizar;
    public TextMeshProUGUI textoBotonUtilizar;
    public TextMeshProUGUI textoMensaje;
    
    [Header("Configuración Visual")]
    public Color colorNormal = Color.white;
    public Color colorDeshabilitado = Color.gray;
    
    // Estado interno
    private ManoJugador manoJugadorActual;
    private GameObject cartaSeleccionada;
    private bool cartaEnUso = false;
    
    // Estados para selección de objetivo
    private bool esperandoSeleccionObjetivo = false;
    private int jugadorObjetivoID = -1;
    private CardType tipoCartaEnUso;

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
        // Configurar botón inicialmente deshabilitado
        if (botonUtilizar != null)
        {
            botonUtilizar.gameObject.SetActive(false);
            botonUtilizar.onClick.AddListener(UtilizarCartaSeleccionada);
        }
        
        // Ocultar texto al inicio
        if (textoMensaje != null)
        {
            textoMensaje.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Actualizar estado del botón cada frame
        ActualizarEstadoBoton();
        
        // Manejar clics en avatares durante selección de objetivo
        if (esperandoSeleccionObjetivo && Input.GetMouseButtonDown(0))
        {
            ManejarClicEnAvatar();
        }
    }

    /// <summary>
    /// Actualiza el estado del botón de utilizar carta
    /// </summary>
    public void ActualizarEstadoBoton()
    {
        // Verificar si ya se tiró el dado en este turno
        if (GameManager.Instance != null && GameManager.Instance.dadoTiradoEnEsteTurno)
        {
            OcultarBotonUtilizar();
            return;
        }
        
        // Si estamos en medio de una acción de carta, no actualizar
        if (esperandoSeleccionObjetivo || cartaEnUso) 
        {
            OcultarBotonUtilizar();
            return;
        }

        // Verificar si es el turno del jugador humano
        bool esTurnoJugador = GameManager.Instance != null && 
                              GameManager.Instance.currentPlayerIndex == 0 &&
                              GameManager.Instance.currentPhase == GamePhase.TotemMovement;

        if (!esTurnoJugador)
        {
            OcultarBotonUtilizar();
            return;
        }

        // Buscar la mano del jugador actual si no la tenemos
        if (manoJugadorActual == null)
        {
            manoJugadorActual = BuscarManoJugadorActual();
        }

        // Verificar si hay una carta de acción seleccionada
        bool puedeMostrarBoton = esTurnoJugador && 
                                manoJugadorActual != null && 
                                manoJugadorActual.GetCartaSeleccionada() != null;

        if (puedeMostrarBoton)
        {
            GameObject carta = manoJugadorActual.GetCartaSeleccionada();
            Carta3D cartaScript = carta?.GetComponent<Carta3D>();
            
            if (cartaScript != null && cartaScript.EsCartaDeAccion())
            {
                MostrarBotonUtilizar();
                cartaSeleccionada = carta;
                tipoCartaEnUso = cartaScript.GetTipoCarta();
            }
            else
            {
                OcultarBotonUtilizar();
            }
        }
        else
        {
            OcultarBotonUtilizar();
        }
    }

    /// <summary>
    /// Busca la mano del jugador humano actual
    /// </summary>
    private ManoJugador BuscarManoJugadorActual()
    {
        ManoJugador[] todasLasManos = FindObjectsOfType<ManoJugador>();
        
        foreach (ManoJugador mano in todasLasManos)
        {
            if (mano.playerID == 1 && !mano.esIA)
            {
                return mano;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Muestra el botón de utilizar carta
    /// </summary>
    private void MostrarBotonUtilizar()
    {
        if (botonUtilizar != null && !botonUtilizar.gameObject.activeInHierarchy)
        {
            botonUtilizar.gameObject.SetActive(true);
            botonUtilizar.interactable = true;
            
            if (textoBotonUtilizar != null)
            {
                textoBotonUtilizar.color = colorNormal;
            }
        }
    }

    /// <summary>
    /// Oculta el botón de utilizar carta
    /// </summary>
    private void OcultarBotonUtilizar()
    {
        if (botonUtilizar != null && botonUtilizar.gameObject.activeInHierarchy)
        {
            botonUtilizar.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Muestra un mensaje temporal al jugador
    /// </summary>
    public void MostrarMensaje(string mensaje)
    {
        if (textoMensaje != null)
        {
            textoMensaje.text = mensaje;
            textoMensaje.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Oculta el mensaje actual
    /// </summary>
    public void OcultarMensaje()
    {
        if (textoMensaje != null)
        {
            textoMensaje.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Ejecuta la acción de la carta seleccionada
    /// </summary>
    public void UtilizarCartaSeleccionada()
    {
        if (cartaSeleccionada == null || cartaEnUso) return;

        Carta3D cartaScript = cartaSeleccionada.GetComponent<Carta3D>();
        if (cartaScript == null) return;

        tipoCartaEnUso = cartaScript.GetTipoCarta();
        
        // Desactivar botón de tirar dado inmediatamente
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetDiceButtonVisibility(false);
        }
        
        cartaEnUso = true;

        // Ejecutar la acción según el tipo de carta
        switch (tipoCartaEnUso)
        {
            case CardType.Dinamita:
                IniciarSeleccionObjetivoDinamita();
                break;
            default:
                cartaEnUso = false;
                // Reactivar botón de dado si hay error
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetDiceButtonVisibility(true);
                }
                break;
        }
    }

    /// <summary>
    /// Inicia la selección de objetivo para la carta Dinamita
    /// </summary>
    private void IniciarSeleccionObjetivoDinamita()
    {
        // Ocultar botón utilizar temporalmente
        OcultarBotonUtilizar();
        
        // Activar modo selección de objetivo
        esperandoSeleccionObjetivo = true;
        
        // Mostrar mensaje al jugador
        MostrarMensaje("Selecciona un jugador objetivo para la DINAMITA");
        
        // Resaltar avatares de jugadores disponibles
        List<int> jugadoresDisponibles = ObtenerJugadoresObjetivo();
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarJugadoresDisponibles(jugadoresDisponibles);
        }
    }

    /// <summary>
    /// Obtiene la lista de jugadores que pueden ser objetivo
    /// </summary>
    private List<int> ObtenerJugadoresObjetivo()
    {
        List<int> jugadoresDisponibles = new List<int>();
        
        if (SistemaAvataresJugadores.Instance != null)
        {
            var todosJugadores = SistemaAvataresJugadores.Instance.GetJugadoresDisponibles();
            foreach (int playerID in todosJugadores)
            {
                // Excluir al jugador actual (Jugador 1)
                if (playerID != 1)
                {
                    // Verificar que el jugador objetivo tiene cartas
                    if (MazoFisico.Instance != null && 
                        MazoFisico.Instance.manosJugadores.TryGetValue(playerID, out ManoJugador manoObjetivo))
                    {
                        if (manoObjetivo.CantidadCartas > 0)
                        {
                            jugadoresDisponibles.Add(playerID);
                        }
                    }
                }
            }
        }
        
        if (jugadoresDisponibles.Count == 0)
        {
            CancelarSeleccionObjetivo();
        }
        
        return jugadoresDisponibles;
    }

    /// <summary>
    /// Maneja el clic en avatar durante selección de objetivo
    /// </summary>
    private void ManejarClicEnAvatar()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            AvatarJugador avatar = hit.collider.GetComponent<AvatarJugador>();
            if (avatar != null)
            {
                int playerIDObjetivo = avatar.GetPlayerID();
                
                // Verificar que el jugador objetivo es válido
                List<int> jugadoresDisponibles = ObtenerJugadoresObjetivo();
                if (jugadoresDisponibles.Contains(playerIDObjetivo))
                {
                    jugadorObjetivoID = playerIDObjetivo;
                    ConfirmarSeleccionObjetivo();
                }
            }
        }
    }

    /// <summary>
    /// Confirma la selección y ejecuta la acción
    /// </summary>
    private void ConfirmarSeleccionObjetivo()
    {
        // Ocultar mensaje
        OcultarMensaje();
        
        // Quitar resaltado de avatares
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarTodosLosAvatares(false);
        }
        
        // Ejecutar la acción de la Dinamita
        EjecutarDinamita(jugadorObjetivoID);
        
        // Resetear estado
        esperandoSeleccionObjetivo = false;
        jugadorObjetivoID = -1;
    }

    /// <summary>
    /// Cancela la selección de objetivo
    /// </summary>
    private void CancelarSeleccionObjetivo()
    {
        // Ocultar mensaje
        OcultarMensaje();
        
        // Quitar resaltado de avatares
        if (SistemaAvataresJugadores.Instance != null)
        {
            SistemaAvataresJugadores.Instance.ResaltarTodosLosAvatares(false);
        }
        
        esperandoSeleccionObjetivo = false;
        jugadorObjetivoID = -1;
        cartaEnUso = false;
        
        // Reactivar botón de tirar dado al cancelar
        bool esTurnoJugador = GameManager.Instance != null && 
                              GameManager.Instance.currentPlayerIndex == 0 &&
                              GameManager.Instance.currentPhase == GamePhase.TotemMovement;
        
        if (esTurnoJugador && UIManager.Instance != null)
        {
            UIManager.Instance.SetDiceButtonVisibility(true);
        }
        
        // Volver a mostrar botón utilizar
        ActualizarEstadoBoton();
    }

    /// <summary>
    /// Ejecuta la lógica de la carta Dinamita
    /// </summary>
    private void EjecutarDinamita(int jugadorObjetivoID)
    {
        // Obtener la mano del jugador objetivo
        if (MazoFisico.Instance != null && 
            MazoFisico.Instance.manosJugadores.TryGetValue(jugadorObjetivoID, out ManoJugador manoObjetivo))
        {
            int cartasTotales = manoObjetivo.CantidadCartas;
            
            // Calcular cuántas cartas descartar (mitad, redondeando hacia arriba)
            int cartasADescartar = Mathf.CeilToInt(cartasTotales / 2f);
            
            if (cartasADescartar > 0)
            {
                // Iniciar proceso de selección de cartas para descartar
                IniciarSeleccionCartasParaDescarte(manoObjetivo, cartasADescartar);
            }
            else
            {
                TerminarUsoDinamita();
            }
        }
        else
        {
            TerminarUsoDinamita();
        }
    }

    /// <summary>
    /// Inicia la selección de cartas para descartar con Dinamita
    /// </summary>
    private void IniciarSeleccionCartasParaDescarte(ManoJugador manoObjetivo, int cartasADescartar)
    {
        // Mostrar mensaje al jugador
        MostrarMensaje($"Selecciona {cartasADescartar} cartas del Jugador {manoObjetivo.playerID} para descartar");
        
        // Habilitar las cartas del jugador objetivo para selección
        manoObjetivo.HabilitarCartasParaSeleccionDinamita(cartasADescartar);
        
        // Iniciar corrutina que espera la selección
        StartCoroutine(EsperarSeleccionCartas(manoObjetivo, cartasADescartar));
    }

    /// <summary>
    /// Espera a que se seleccionen las cartas requeridas
    /// </summary>
    private IEnumerator EsperarSeleccionCartas(ManoJugador manoObjetivo, int cartasADescartar)
    {
        // Esperar a que se seleccionen las cartas requeridas
        while (manoObjetivo.CartasSeleccionadasParaDinamita.Count < cartasADescartar)
        {
            yield return null;
        }
        
        // Proceder con el descarte de las cartas seleccionadas
        ProcesarDescarteDinamita(manoObjetivo);
    }

    /// <summary>
    /// Procesa el descarte de las cartas seleccionadas
    /// </summary>
    private void ProcesarDescarteDinamita(ManoJugador manoObjetivo)
    {
        // Obtener las cartas seleccionadas
        List<GameObject> cartasSeleccionadas = new List<GameObject>(manoObjetivo.CartasSeleccionadasParaDinamita);
        
        // Mover cada carta seleccionada al descarte
        foreach (GameObject carta in cartasSeleccionadas)
        {
            if (MazoDescarte.Instance != null)
            {
                // Asegurar que la carta tenga la escala correcta antes de mover al descarte
                Carta3D cartaScript = carta.GetComponent<Carta3D>();
                if (cartaScript != null)
                {
                    cartaScript.SetEnManoIA(false);
                }
                
                MazoDescarte.Instance.AgregarCartaDescarte(carta);
                manoObjetivo.RemoverCarta(carta);
            }
        }
        
        // Limpiar la selección de dinamita
        manoObjetivo.LimpiarSeleccionDinamita();
        
        // Ocultar mensaje
        OcultarMensaje();
        
        // Terminar el uso de la Dinamita
        TerminarUsoDinamita();
    }

    /// <summary>
    /// Cancela la selección de cartas
    /// </summary>
    public void CancelarSeleccionCartas()
    {
        if (jugadorObjetivoID != -1)
        {
            // Obtener la mano del jugador objetivo
            if (MazoFisico.Instance != null && 
                MazoFisico.Instance.manosJugadores.TryGetValue(jugadorObjetivoID, out ManoJugador manoObjetivo))
            {
                manoObjetivo.LimpiarSeleccionDinamita();
            }
        }
        
        OcultarMensaje();
    }

    /// <summary>
    /// Termina el uso de la carta Dinamita
    /// </summary>
    private void TerminarUsoDinamita()
    {
        // Mover la carta de dinamita al descarte
        if (cartaSeleccionada != null && manoJugadorActual != null)
        {
            if (MazoDescarte.Instance != null)
            {
                // Deseleccionar antes de mover
                Carta3D cartaScript = cartaSeleccionada.GetComponent<Carta3D>();
                if (cartaScript != null)
                {
                    cartaScript.Deseleccionar();
                }
                
                MazoDescarte.Instance.AgregarCartaDescarte(cartaSeleccionada);
                manoJugadorActual.RemoverCarta(cartaSeleccionada);
            }
        }

        // Deseleccionar todas las cartas
        if (manoJugadorActual != null)
        {
            manoJugadorActual.DeseleccionarTodasLasCartas();
        }

        // Reactivar botón de tirar dado (si todavía es nuestro turno)
        bool esTurnoJugador = GameManager.Instance != null && 
                              GameManager.Instance.currentPlayerIndex == 0 &&
                              GameManager.Instance.currentPhase == GamePhase.TotemMovement;
        
        if (esTurnoJugador && UIManager.Instance != null)
        {
            UIManager.Instance.SetDiceButtonVisibility(true);
        }

        // Terminar el turno
        cartaEnUso = false;
        TerminarTurnoDespuesDeUsarCarta();
    }

    /// <summary>
    /// Termina el turno después de usar una carta de acción
    /// </summary>
    private void TerminarTurnoDespuesDeUsarCarta()
    {
        StartCoroutine(TerminarTurnoCoroutine());
    }

    /// <summary>
    /// Corrutina para terminar el turno con delay
    /// </summary>
    private IEnumerator TerminarTurnoCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndTurn();
        }
    }

    /// <summary>
    /// Fuerza la actualización del estado del botón
    /// </summary>
    public void ForzarActualizacionBoton()
    {
        ActualizarEstadoBoton();
    }

    /// <summary>
    /// Se llama cuando se activa el dado
    /// </summary>
    public void OnDiceActivated()
    {
        OcultarBotonUtilizar();
        
        // Deseleccionar cualquier carta seleccionada permanentemente
        if (manoJugadorActual != null)
        {
            manoJugadorActual.DeseleccionarTodasLasCartas();
            cartaSeleccionada = null;
        }
        
        // Asegurar que no se pueda volver a seleccionar cartas de acción
        if (GameManager.Instance != null)
        {
            GameManager.Instance.dadoTiradoEnEsteTurno = true;
        }
    }

    /// <summary>
    /// Se llama cuando se desactiva el dado
    /// </summary>
    public void OnDiceDeactivated()
    {
        // Esperar un frame para que se actualice el estado del juego
        StartCoroutine(RevisarEstadoBotonDespuesDeDado());
    }

    /// <summary>
    /// Revisa el estado del botón después de usar el dado
    /// </summary>
    private IEnumerator RevisarEstadoBotonDespuesDeDado()
    {
        yield return null;
        
        // Solo actualizar si no estamos en medio de una acción de carta
        if (!esperandoSeleccionObjetivo && !cartaEnUso)
        {
            ActualizarEstadoBoton();
        }
    }
}