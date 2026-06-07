using UnityEngine;
using Unity.Netcode;

public class PlayerInteractor : NetworkBehaviour
{
    [SerializeField] private float interactionRadius = 2f;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private Transform holdPoint;

    private PlayerInputHandler input;
    private Camera mainCamera;

    private ActionInteractable heldInteractable;
    private PushableObject activePushable;

    public bool IsPushing { get; private set; }

    // --- AQUÍ ESTÁ LA LÍNEA QUE FALTABA ---
    // Esto le avisa al Animator si el personaje tiene un objeto en las manos
    public bool IsHolding => heldInteractable != null;

    public Transform HoldPoint => holdPoint;
    public Camera MainCamera => mainCamera;
    public Vector2 MoveInput => input != null ? input.MoveInput : Vector2.zero;

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Buscamos la cámara principal una vez spawneado,
            // así evitamos agarrar la cámara del jugador equivocado
            mainCamera = Camera.main;
        }
        else
        {
            enabled = false;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (input == null || !input.ActionPressedThisFrame) return;

        if (heldInteractable != null)
        {
            heldInteractable.Interact(this);
            return;
        }

        if (IsPushing && activePushable != null)
        {
            activePushable.Interact(this);
            return;
        }

        ActionInteractable target = FindBestInteractable();
        if (target != null)
        {
            target.Interact(this);
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (IsPushing && activePushable != null)
        {
            activePushable.ApplyPush(input.MoveInput, mainCamera);
        }
    }

    private ActionInteractable FindBestInteractable()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactionRadius, interactableMask);

        ActionInteractable best = null;
        float bestScore = float.MinValue;

        foreach (var hit in hits)
        {
            ActionInteractable interactable = hit.GetComponentInParent<ActionInteractable>();
            if (interactable == null || !interactable.CanInteract(this)) continue;

            float distance = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
            float score = interactable.Priority * 10f - distance;

            if (score > bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }

        return best;
    }

    public void StartPush(PushableObject pushable, Vector3 snapPosition)
    {
        IsPushing = true;
        activePushable = pushable;
        transform.position = snapPosition;
    }

    public void StopPush()
    {
        IsPushing = false;
        activePushable = null;
    }

    public void SetHeldInteractable(ActionInteractable interactable)
    {
        heldInteractable = interactable;
    }

    public void ClearHeldInteractable(ActionInteractable interactable)
    {
        if (heldInteractable == interactable)
            heldInteractable = null;
    }
}
