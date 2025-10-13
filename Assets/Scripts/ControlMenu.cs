using TMPro;
using UnityEngine;

public class ControlMenu : MonoBehaviour
{
    public static ControlMenu instancia;
    [SerializeField] private TMP_Text dado1;
    private int valor1 = 0;

    private void OnEnable()
    {
        if(instancia == null)
        {
            instancia = this;
        }
    }

    public void ActualizarValor(int dado)
    {
        if(valor1 == 0)
        {
            valor1 = dado;
        }

        if(valor1 != 0)
        {
            dado1.text = "Dado: " + valor1.ToString();
        }
    }

    public void LimpiarValores()
    {
        valor1 = 0;
        dado1.text = "Dado: " + valor1.ToString();
    }
}
