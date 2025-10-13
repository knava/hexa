using UnityEngine;


public class ControlDado : MonoBehaviour
{
	public event System.Action<int> OnDiceStopped;
    private float ejeX;
    private float ejeY;
    private float ejeZ;
    private Vector3 posicionInicial;
    private Rigidbody rbDado;
    private bool dadoEnMovimiento = false;
    public ControlCara[] lados = new ControlCara[6];
    private int valorDado;
    private int ladoOculto;
    private int intentosFallidos = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        posicionInicial = this.transform.position;
        rbDado = this.GetComponent<Rigidbody>();
        rbDado.isKinematic = true;
        //PrepararDado();

    }

    // Update is called once per frame
    void Update()
    {
        
        if (rbDado.IsSleeping() && dadoEnMovimiento)
        {
            dadoEnMovimiento = false;
            ladoOculto = ComprobarLados();
            valorDado = 7 - ladoOculto;
            if(valorDado == 7)
            {
                intentosFallidos++;
                if(intentosFallidos >= 2)
                {
                    PrepararDado();
                    return;
                }
                rbDado.AddForce(3f, 0, 0, ForceMode.Impulse);
                dadoEnMovimiento = true;
            }
            else
            {
                intentosFallidos = 0;
                ControlMenu.instancia.ActualizarValor(valorDado);
				OnDiceStopped?.Invoke(valorDado); // Notificar el resultado
            }
        }
        if (!dadoEnMovimiento)
        {
            ControlMenu.instancia.ActualizarValor(valorDado);
        }
    }

    public void PrepararDado()
    {
        
        this.transform.position = posicionInicial;
        rbDado.linearVelocity = new Vector3(0f, 0f, 0f);
        ControlMenu.instancia.LimpiarValores();
        rbDado.isKinematic = false;
        dadoEnMovimiento = true;
        intentosFallidos = 0;

        this.transform.position = posicionInicial + new Vector3(0, 4f, 0); // Sube 7 unidades en Y

        ejeX = Random.Range(0f, 271f);
        ejeY = Random.Range(0f, 271f);
        ejeZ = Random.Range(0f, 271f);
        this.transform.Rotate(ejeX, ejeY, ejeZ);
        ejeX = Random.Range(-3f, 3f);
        ejeY = Random.Range(-5f, -2f); // Fuerza hacia abajo más fuerte
        ejeZ = Random.Range(-3f, 3f);
        rbDado.AddForce(ejeX,ejeY,ejeZ,ForceMode.Impulse);
    }
    int ComprobarLados()
    {
        int valor = 0;
        for(int i = 0; i < 6; i++)
        {
            if (lados[i].CompruebaSuelo())
            {
                valor = i + 1;
            }
        }
        return valor;
    }
}
