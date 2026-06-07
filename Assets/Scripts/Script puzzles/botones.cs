using UnityEngine;

public class ButtonController : MonoBehaviour
{
    public Door puertaController;
    public int botonID; 

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (botonID == 1)
                puertaController.Boton1Presionar(true);
            else if (botonID == 2)
                puertaController.Boton2Presionar(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (botonID == 1)
                puertaController.Boton1Presionar(false);
            else if (botonID == 2)
                puertaController.Boton2Presionar(false);
        }
    }
}
