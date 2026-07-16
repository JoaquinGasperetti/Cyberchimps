using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerCameraTarget : NetworkBehaviour
{
    [Tooltip("Transform que la cámara va a seguir. Si no se asigna, usa el transform del Player.")]
    [SerializeField] private Transform cameraTarget;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        Transform target = cameraTarget != null ? cameraTarget : transform;

        CinemachineCamera vcam = FindAnyObjectByType<CinemachineCamera>();

        if (vcam == null)
        {
            Debug.LogWarning("[PlayerCameraTarget] No se encontró CinemachineCamera en escena.");
            return;
        }

        vcam.Target.TrackingTarget = target;
        vcam.Target.LookAtTarget = target;
        vcam.enabled = true;

        Debug.Log($"[PlayerCameraTarget] Cámara asignada al jugador local: {gameObject.name}");
    }
}