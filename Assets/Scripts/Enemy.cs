using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Transform player;
    public Transform[] patrolPoints;  
    public float visionRange = 10f;
    public float visionAngle = 45f;
    public float speed = 3f;

    private int currentPatrolIndex = 0;
    private enum State { Patrol, Chase }
    private State currentState = State.Patrol;

    void Update()
    {
        switch (currentState)
        {
            case State.Patrol:
                Patrol();
                if (CanSeePlayer()) currentState = State.Chase;
                break;

            case State.Chase:
                Chase();
                if (!CanSeePlayer()) currentState = State.Patrol;
                break;
        }
    }

    void Patrol()
    {
        Transform targetPoint = patrolPoints[currentPatrolIndex];
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPoint.position) < 0.2f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    void Chase()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            player.position,
            speed * Time.deltaTime
        );
        transform.LookAt(player);
    }

    bool CanSeePlayer()
    {
        Vector3 dirToPlayer = player.position - transform.position;
        float distance = dirToPlayer.magnitude;

        if (distance < visionRange)
        {
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle < visionAngle)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, dirToPlayer.normalized, out hit, visionRange))
                {
                    if (hit.transform == player) return true;
                }
            }
        }
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Vector3 leftLimit = Quaternion.Euler(0, -visionAngle, 0) * transform.forward * visionRange;
        Vector3 rightLimit = Quaternion.Euler(0, visionAngle, 0) * transform.forward * visionRange;

        Gizmos.DrawLine(transform.position, transform.position + leftLimit);
        Gizmos.DrawLine(transform.position, transform.position + rightLimit);
    }
}
