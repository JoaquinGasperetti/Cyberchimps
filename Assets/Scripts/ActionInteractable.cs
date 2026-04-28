using UnityEngine;

public abstract class ActionInteractable : MonoBehaviour
{
    [SerializeField] private int priority = 0;
    public int Priority => priority;

    public abstract bool CanInteract(PlayerInteractor interactor);
    public abstract void Interact(PlayerInteractor interactor);
}