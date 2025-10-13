using UnityEngine;

public class ControlCara : MonoBehaviour
{
    private bool enElSuelo = false;

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.tag == "Tapete")
        {
            enElSuelo=true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        enElSuelo = false; 
    }

    public bool CompruebaSuelo()
    {
        return enElSuelo;
    }
}
