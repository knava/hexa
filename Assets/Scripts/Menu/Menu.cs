using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class Menu : MonoBehaviour
{
    public TMP_Dropdown jugadoresDropdown;
    public TMP_Dropdown iaDropdown; // Nuevo dropdown para seleccionar cantidad de IA
    public GameObject playerPrefab;
    public static int cantidadJugadoresAEinstanciar;
    public static int cantidadIA; // Nueva variable estática para la cantidad de IA

    public void Jugar() 
	{
		int totalJugadores = jugadoresDropdown.value + 2; // 2 a 6 jugadores
		int totalIA = iaDropdown.value; // 0 a (totalJugadores - 1) IA

		// Validación para evitar más IA que jugadores
		if (totalIA >= totalJugadores) {
			Debug.LogError("¡No puede haber más IA que jugadores totales!");
			return;
		}

		// Guarda los valores estáticos para usarlos al generar el tablero
		cantidadJugadoresAEinstanciar = totalJugadores;
		cantidadIA = totalIA;
		
		Debug.Log($"Jugadores: {totalJugadores} (Humanos: {totalJugadores - totalIA}, IA: {totalIA})");
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
	}

    public void Salir()
    {
        Debug.Log("Salir...");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
