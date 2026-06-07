using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Agregá este script al prefab del Player.
/// En OnNetworkSpawn, si somos el owner (jugador local),
/// busca la CinemachineCamera en escena y la apunta a este transform.
///
/// SETUP:
/// - Agregá este script al prefab Player
/// - Opcionalmente asigná un Transform hijo como "cameraTarget"
///   (ej. un empty a altura del torso) para que la cámara no siga los pies
/// </summary>
public class PlayerCameraTarget : NetworkBehaviour
{
    [Tooltip("Transform que la cámara va a seguir. Si no se asigna, usa el transform del Player.")]
    [SerializeField] private Transform cameraTarget;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // Usar el propio transform si no hay target específico
        Transform target = cameraTarget != null ? cameraTarget : transform;

        // Buscar la CinemachineCamera en escena
        CinemachineCamera vcam = FindAnyObjectByType<CinemachineCamera>();

        if (vcam == null)
        {
            Debug.LogWarning("[PlayerCameraTarget] No se encontró CinemachineCamera en escena.");
            return;
        }

        // Asignar el target y activar la cámara
        vcam.Target.TrackingTarget = target;
        vcam.Target.LookAtTarget = target;
        vcam.enabled = true;

        Debug.Log($"[PlayerCameraTarget] Cámara asignada al jugador local: {gameObject.name}");
    }
}