using UnityEngine;

public class DoorInteractable : ActionInteractable
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float speed = 5f;

    private bool isOpen;
    private float currentAngle;

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return true;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        isOpen = !isOpen;
    }

    private void Update()
    {
        float target = isOpen ? openAngle : 0f;
        currentAngle = Mathf.Lerp(currentAngle, target, Time.deltaTime * speed);

        if (doorPivot != null)
        {
            doorPivot.localRotation = Quaternion.Euler(0, currentAngle, 0);
        }
    }
}