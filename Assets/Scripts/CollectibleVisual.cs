using UnityEngine;

public class CollectibleVisual : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Floating")]
    [SerializeField] private float floatAmplitude = 0.25f;
    [SerializeField] private float floatFrequency = 1f;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.localPosition;
    }

    private void Update()
    {
        Rotate();
        Float();
    }

    private void Rotate()
    {
        transform.Rotate(
            0f,
            rotationSpeed * Time.deltaTime,
            0f,
            Space.Self
        );
    }

    private void Float()
    {
        Vector3 position = startPosition;

        position.y += Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;

        transform.localPosition = position;
    }
}