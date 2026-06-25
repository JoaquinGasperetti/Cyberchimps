using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Maneja los parámetros del Animator del jugador en red.
/// Se ejecuta en TODOS los clientes — cada uno calcula las animaciones
/// localmente usando la velocidad del Rigidbody sincronizada por NetworkTransform.
/// El NetworkAnimator propaga los triggers puntuales (salto, acciones).
///
/// SETUP en el prefab Player:
///   1. Agregá este script al prefab Player
///   2. Asegurate que el Animator tenga: Assets/Animations/Player.controller
///   3. Agregá NetworkAnimator (Add Component → Netcode → Network Animator)
///      y asignale el Animator en su campo "Animator"
///
/// Parámetros del Animator Controller que se usan:
///   Speed               (Float) — velocidad horizontal → Blend Tree movimiento
///   YVelocity           (Float) — velocidad vertical   → subir / caer
///   IsGrounded          (Bool)  — en el suelo
///   IsPushing           (Bool)  — empujando una caja
///   estaSosteniendoCaja (Bool)  — sosteniendo un objeto
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorController : NetworkBehaviour
{
    private static readonly int SpeedHash        = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash    = Animator.StringToHash("YVelocity");
    private static readonly int IsGroundedHash   = Animator.StringToHash("IsGrounded");
    private static readonly int IsPushingHash    = Animator.StringToHash("IsPushing");
    private static readonly int IsHoldingHash    = Animator.StringToHash("estaSosteniendoCaja");

    [SerializeField] private float speedDampTime = 0.1f;

    private Animator      animator;
    private Rigidbody     rb;
    private PlayerInteractor interactor;
    private PlayerController playerController;

    private void Awake()
    {
        animator   = GetComponent<Animator>();
        rb         = GetComponent<Rigidbody>();
        interactor = GetComponent<PlayerInteractor>();
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // Calculamos animaciones localmente en todos los clientes.
        // El Rigidbody del jugador remoto es kinematic, pero NetworkTransform
        // actualiza su posición → podemos derivar velocidad del delta de posición.
        UpdateParameters();
    }

    private void UpdateParameters()
    {
        if (animator == null) return;

        Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;

        // Velocidad horizontal
        float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
        animator.SetFloat(SpeedHash, speed, speedDampTime, Time.deltaTime);

        // Velocidad vertical
        animator.SetFloat(YVelocityHash, vel.y);

        // Usar el estado grounded del PlayerController si está disponible
        bool grounded;
        if (playerController != null)
        {
            grounded = playerController.IsGrounded;
        }
        else
        {
            // Fallback si PlayerController no está disponible
            grounded = rb == null || rb.isKinematic || Mathf.Abs(vel.y) < 0.25f;
        }
        animator.SetBool(IsGroundedHash, grounded);

        // Empujando
        bool pushing = interactor != null && interactor.IsPushing;
        animator.SetBool(IsPushingHash, pushing);

        // Sosteniendo objeto
        bool holding = interactor != null && interactor.IsHolding;
        animator.SetBool(IsHoldingHash, holding);
    }
}
