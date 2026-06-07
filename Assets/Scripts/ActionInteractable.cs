using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Clase base para todos los objetos con los que el jugador puede interactuar.
/// Hereda de NetworkBehaviour para que las subclases puedan usar RPCs y NetworkVariables.
/// </summary>
public abstract class ActionInteractable : NetworkBehaviour
{
    [SerializeField] private int priority = 0;
    public int Priority => priority;

    public abstract bool CanInteract(PlayerInteractor interactor);
    public abstract void Interact(PlayerInteractor interactor);
}
