using UnityEngine;

public class FishJumpPath : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform centerPoint;

    [Header("Movement")]
    [SerializeField] private float jumpDuration = 2f;
    [SerializeField] private float startDelay = 0f;

    [Header("Jump Shape")]
    [SerializeField] private float jumpHeightMultiplier = 1f;

    [SerializeField]
    private AnimationCurve jumpCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Rotation")]
    [SerializeField] private bool rotateDuringJump = true;
    [SerializeField] private float rotationAmount = 360f;

    private float timer;
    private float spawnTime;

    private void Start()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (pointA == null || pointB == null || centerPoint == null)
            return;

        if (Time.time - spawnTime < startDelay)
            return;

        timer += Time.deltaTime;

        float normalizedTime =
            Mathf.PingPong(timer / jumpDuration, 1f);

        bool goingForward =
            Mathf.FloorToInt(timer / jumpDuration) % 2 == 0;

        Vector3 start = goingForward ? pointA.position : pointB.position;
        Vector3 end = goingForward ? pointB.position : pointA.position;

        UpdatePosition(start, end, normalizedTime);

        if (rotateDuringJump)
        {
            UpdateRotation(normalizedTime, goingForward);
        }
    }

    private void UpdatePosition(Vector3 start, Vector3 end, float t)
    {
        Vector3 basePosition = Vector3.Lerp(start, end, t);

        float curveValue = jumpCurve.Evaluate(t);

        float centerHeight =
            centerPoint.position.y - Mathf.Lerp(start.y, end.y, t);

        basePosition.y += centerHeight * curveValue * jumpHeightMultiplier;

        transform.position = basePosition;
    }

    private void UpdateRotation(float t, bool goingForward)
    {
        float rotationProgress =
            goingForward ? t : 1f - t;

        float xRotation =
            rotationAmount * rotationProgress;

        Vector3 direction =
            goingForward
            ? (pointB.position - pointA.position).normalized
            : (pointA.position - pointB.position).normalized;

        Quaternion lookRotation =
            Quaternion.LookRotation(direction);

        transform.rotation =
            lookRotation *
            Quaternion.Euler(xRotation, 0f, 0f);
    }


    private void OnDrawGizmos()
    {
        if (pointA == null || pointB == null || centerPoint == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(pointA.position, 0.2f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(pointB.position, 0.2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(centerPoint.position, 0.25f);

        Gizmos.color = Color.green;

        Vector3 previous = pointA.position;

        for (int i = 1; i <= 30; i++)
        {
            float t = i / 30f;

            Vector3 pos =
                Vector3.Lerp(pointA.position, pointB.position, t);

            float centerHeight =
                centerPoint.position.y -
                Mathf.Lerp(pointA.position.y, pointB.position.y, t);

            pos.y +=
                centerHeight *
                jumpCurve.Evaluate(t) *
                jumpHeightMultiplier;

            Gizmos.DrawLine(previous, pos);
            previous = pos;
        }
    }

}