using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PushableObject : ActionInteractable
{
    [SerializeField] private float pushSpeed = 2f;
    [SerializeField] private float snapDistance = 1f;

    private Rigidbody rb;
    private PlayerInteractor activeInteractor;
    private Vector3 pushOffset;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 🔒 Estado inicial: bloqueado
        rb.isKinematic = true;
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return Vector3.Distance(transform.position, interactor.transform.position) < 2f;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        // 🔻 SALIR del modo push
        if (activeInteractor == interactor)
        {
            ExitPush();
            return;
        }

        // 🔺 ENTRAR en modo push
        EnterPush(interactor);
    }

    private void EnterPush(PlayerInteractor interactor)
    {
        Vector3 dir = (interactor.transform.position - transform.position).normalized;
        Vector3 snapPosition = transform.position + dir * snapDistance;

        activeInteractor = interactor;
        pushOffset = snapPosition - transform.position;

        // 🔓 Activar físicas
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;

        interactor.StartPush(this, snapPosition);

        // Mirar al objeto
        Vector3 lookDir = transform.position - interactor.transform.position;
        lookDir.y = 0;
        interactor.transform.rotation = Quaternion.LookRotation(lookDir);
    }

    private void ExitPush()
    {
        if (activeInteractor != null)
        {
            activeInteractor.StopPush();
        }

        activeInteractor = null;

        // 🔒 Bloquear nuevamente
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void ApplyPush(Vector2 input, Camera cam)
    {
        if (activeInteractor == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;

        Vector3 move = right * input.x + forward * input.y;

        rb.linearVelocity = new Vector3(
            move.x * pushSpeed,
            rb.linearVelocity.y,
            move.z * pushSpeed
        );

        // 🔁 Mantener al player pegado
        Vector3 targetPos = transform.position + pushOffset;

        activeInteractor.transform.position = new Vector3(
            targetPos.x,
            activeInteractor.transform.position.y,
            targetPos.z
        );
    }
}