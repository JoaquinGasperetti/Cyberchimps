using UnityEngine;

public class Door : MonoBehaviour
{
    public Transform puerta;          // Asigna el objeto de la puerta
    public Vector3 posicionAbierta;   // Posición de la puerta cuando está abierta
    public Vector3 posicionCerrada;   // Posición de la puerta cuando está cerrada
    public float velocidad = 2f;      // Velocidad de apertura/cierre

    private bool boton1Presionado = false;
    private bool boton2Presionado = false;

    void Update()
    {
       
        if (boton1Presionado && boton2Presionado)
        {
            puerta.localPosition = Vector3.Lerp(puerta.localPosition, posicionAbierta, Time.deltaTime * velocidad);
        }
        else
        {
          
            puerta.localPosition = Vector3.Lerp(puerta.localPosition, posicionCerrada, Time.deltaTime * velocidad);
        }
    }

    
    public void Boton1Presionar(bool estado)
    {
        boton1Presionado = estado;
    }

    public void Boton2Presionar(bool estado)
    {
        boton2Presionado = estado;
    }
}
