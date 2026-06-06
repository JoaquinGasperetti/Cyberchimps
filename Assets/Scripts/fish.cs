using UnityEngine;

public class FishDirectionalCube : MonoBehaviour
{
    public float jumpHeight = 3f;       // altura máxima del salto
    public float jumpDistance = 5f;     // distancia del salto en la dirección del cubo
    public float jumpDuration = 2f;     // tiempo total del salto
    public float startDelay = 0f;       // retraso inicial para desincronizar

    private Vector3 startPos;
    private Vector3 endPos;
    private float timer;

    void Start()
    {
        startPos = transform.position;
        // El salto se calcula en la dirección hacia donde apunta el cubo
        endPos = startPos + transform.forward * jumpDistance;
    }

    void Update()
    {
        if (Time.time < startDelay) return;

        timer += Time.deltaTime;
        float t = timer / jumpDuration;

        if (t <= 1f)
        {
            // Fase de subida: parábola en la dirección del cubo
            float height = Mathf.Sin(Mathf.PI * t) * jumpHeight;
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            pos.y += height;
            transform.position = pos;

            // Rotación completa de 360° durante el salto
            float currentRotation = 360f * t;
            transform.rotation = Quaternion.Euler(currentRotation, 0f, 0f) * Quaternion.LookRotation(transform.forward);
        }
        else if (t <= 2f)
        {
            // Fase de bajada: parábola inversa de regreso por debajo
            float t2 = t - 1f;
            float height = -Mathf.Sin(Mathf.PI * t2) * jumpHeight;
            Vector3 pos = Vector3.Lerp(endPos, startPos, t2);
            pos.y += height;
            transform.position = pos;

            // Rotación inversa para volver a la zona inicial
            float currentRotation = 360f * (1f - t2);
            transform.rotation = Quaternion.Euler(currentRotation, 0f, 0f) * Quaternion.LookRotation(transform.forward);
        }
        else
        {
            // Reinicia el ciclo
            timer = 0f;
        }
    }
}
