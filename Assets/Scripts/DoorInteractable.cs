using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Puerta sincronizada en red. Cualquier jugador puede abrir/cerrar.
/// REQUERIDO en el prefab: NetworkObject.
/// Agregarlo a la lista de Network Prefabs del NetworkManager.
/// </summary>
public class DoorInteractable : ActionInteractable
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float speed = 5f;

    private NetworkVariable<bool> isOpen = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float currentAngle;

    public override bool CanInteract(PlayerInteractor interactor) => true;

    public override void Interact(PlayerInteractor interactor)
    {
        ToggleDoorServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleDoorServerRpc()
    {
        isOpen.Value = !isOpen.Value;
    }

    private void Update()
    {
        float target = isOpen.Value ? openAngle : 0f;
        currentAngle = Mathf.Lerp(currentAngle, target, Time.deltaTime * speed);

        if (doorPivot != null)
            doorPivot.localRotation = Quaternion.Euler(0, currentAngle, 0);
    }
}
