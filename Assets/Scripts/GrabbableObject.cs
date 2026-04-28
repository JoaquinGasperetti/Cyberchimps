using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : ActionInteractable
{
    [SerializeField] private float throwForce = 8f;

    private Rigidbody rb;
    private PlayerInteractor holder;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return holder == null || holder == interactor;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (holder == null)
            Grab(interactor);
        else if (holder == interactor)
            Throw(interactor);
    }

    private void Grab(PlayerInteractor interactor)
    {
        holder = interactor;

        rb.isKinematic = true;
        rb.detectCollisions = false;

        transform.SetParent(interactor.HoldPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        interactor.SetHeldInteractable(this);
    }

    private void Throw(PlayerInteractor interactor)
    {
        transform.SetParent(null);

        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;

        holder = null;
        interactor.ClearHeldInteractable(this);

        Vector3 dir = interactor.transform.forward;
        rb.AddForce(dir * throwForce, ForceMode.Impulse);
    }
}