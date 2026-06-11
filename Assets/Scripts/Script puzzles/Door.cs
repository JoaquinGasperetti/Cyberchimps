using UnityEngine;

public class Door : MonoBehaviour
{
    public Transform puerta;          
    public Vector3 posicionAbierta;   
    public Vector3 posicionCerrada;  
    public float velocidad = 2f;      

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
