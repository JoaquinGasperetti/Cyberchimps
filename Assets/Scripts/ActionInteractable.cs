using UnityEngine;
using Unity.Netcode;

public abstract class ActionInteractable : NetworkBehaviour
{
    [SerializeField] private int priority = 0;
    public int Priority => priority;

    public abstract bool CanInteract(PlayerInteractor interactor);
    public abstract void Interact(PlayerInteractor interactor);
}
