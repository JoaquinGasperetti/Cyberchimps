using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EnemyController : NetworkBehaviour
{
    private enum State { Patrol, Chase, Return }

    [Header("Patrulla")]
    [Tooltip("Puntos de la ronda. Pueden ser hijos del prefab: se guardan en mundo " +
             "al spawnear, asi no se arrastran cuando el enemigo se mueve.")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 2f;
    [Tooltip("Segundos que frena en cada punto antes de seguir")]
    [SerializeField] private float waitAtPoint = 0.6f;

    [Header("Persecución")]
    [SerializeField] private float chaseSpeed = 4.2f;
    [SerializeField] private float visionRange = 9f;
    [Tooltip("Semiángulo del cono en grados: 60 = cono total de 120")]
    [SerializeField] private float visionHalfAngle = 60f;
    [Tooltip("Segundos que sigue persiguiendo despues de perderlo de vista")]
    [SerializeField] private float loseSightGrace = 1.5f;
    [Tooltip("Capas que tapan la vision (Default + Ground)")]
    [SerializeField] private LayerMask sightBlockers = (1 << 0) | (1 << 6);
    [Tooltip("Altura de los ojos respecto del pivote")]
    [SerializeField] private float eyeHeight = 0.6f;
    [SerializeField] private float turnSpeed = 540f;

    [Header("Suelo")]
    [Tooltip("Pega el enemigo al piso cada frame: sin esto flota en rampas y escalones")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundMask = 1 << 6;
    [SerializeField] private float groundProbeHeight = 1.5f;

    [Header("Daño")]
    [Tooltip("Si esta en true el jugador lo mata cayendole encima")]
    [SerializeField] private bool canBeStomped = false;
    [Tooltip("Margen para decidir que los pies del jugador estan arriba del enemigo")]
    [SerializeField] private float stompTolerance = 0.3f;

    [Header("Visual")]
    [Tooltip("Hijo con el modelo. El saltito se le aplica a el y no al pivote, " +
             "asi NetworkTransform sincroniza una posicion limpia.")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private float hopHeight = 0.18f;
    [SerializeField] private float hopFrequency = 7f;

    private const float ArriveDistance = 0.25f;
    private const float TargetRefreshInterval = 0.5f;

    // el jugador y su collider juntos: buscar el collider cada frame para
    // apuntar el raycast no hacia falta
    private readonly struct Target
    {
        public readonly PlayerLives Lives;
        public readonly Collider Collider;
        public readonly Transform Transform;

        public Target(PlayerLives lives, Collider collider)
        {
            Lives = lives;
            Collider = collider;
            Transform = lives.transform;
        }

        public Vector3 AimPoint => Collider != null ? Collider.bounds.center : Transform.position;
    }

    private readonly List<Target> targets = new List<Target>();
    private float nextTargetRefresh;

    private State state = State.Patrol;
    private Vector3[] patrolWorld = new Vector3[0];
    private int patrolIndex;
    private float waitTimer;

    private Transform chaseTarget;
    private float lastSeenTime;

    private Collider damageCollider;
    private Vector3 modelBaseLocalPos;
    private Vector3 lastVisualPos;
    private float hopPhase;

    private void Awake()
    {
        damageCollider = GetComponent<Collider>();

        if (modelRoot != null)
            modelBaseLocalPos = modelRoot.localPosition;
    }

    public override void OnNetworkSpawn()
    {
        lastVisualPos = transform.position;

        // la IA corre solo en el server; los clientes reciben la posicion
        // por NetworkTransform y solo animan el saltito
        if (!IsServer) return;

        CachePatrolPoints();
        RefreshTargets();
    }

    private void CachePatrolPoints()
    {
        if (patrolPoints == null)
        {
            patrolWorld = new Vector3[0];
            return;
        }

        var points = new List<Vector3>(patrolPoints.Length);
        foreach (var point in patrolPoints)
        {
            if (point != null)
                points.Add(point.position);
        }

        patrolWorld = points.ToArray();
    }

    private void Update()
    {
        if (!IsServer) return;

        // los jugadores spawnean despues que el nivel, y en 2P puede entrar
        // uno mas tarde: por eso se refresca en vez de cachear una sola vez
        if (Time.time >= nextTargetRefresh)
        {
            nextTargetRefresh = Time.time + TargetRefreshInterval;
            RefreshTargets();
        }

        UpdateState();

        switch (state)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Chase: TickChase(); break;
            case State.Return: TickReturn(); break;
        }

        if (snapToGround) SnapToGround();
    }

    private void RefreshTargets()
    {
        targets.Clear();

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        foreach (var client in nm.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            var lives = playerObject.GetComponent<PlayerLives>();
            if (lives == null) continue;

            targets.Add(new Target(lives, playerObject.GetComponent<Collider>()));
        }
    }

    private void UpdateState()
    {
        Transform visible = FindVisibleTarget();

        if (visible != null)
        {
            chaseTarget = visible;
            lastSeenTime = Time.time;
            state = State.Chase;
            return;
        }

        if (state != State.Chase) return;

        // no lo suelta de una: sigue un rato hacia donde lo vio por ultima vez
        if (Time.time - lastSeenTime < loseSightGrace) return;

        chaseTarget = null;
        state = State.Return;
        patrolIndex = NearestPatrolIndex();
    }

    private Transform FindVisibleTarget()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        Transform best = null;
        float bestDistance = float.MaxValue;

        foreach (var target in targets)
        {
            if (target.Lives == null) continue;

            // sin vidas ya esta en game over, no tiene sentido seguirlo
            if (target.Lives.CurrentLives <= 0) continue;

            Vector3 aim = target.AimPoint;
            Vector3 toTarget = aim - eye;
            float distance = toTarget.magnitude;

            if (distance > visionRange || distance < 0.001f) continue;

            // el cono se mide plano: midiendolo en 3D un jugador parado en una
            // plataforma justo arriba quedaba fuera de angulo por la altura
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, flat) > visionHalfAngle) continue;

            if (!HasLineOfSight(eye, toTarget / distance, distance)) continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = target.Transform;
            }
        }

        return best;
    }

    private bool HasLineOfSight(Vector3 eye, Vector3 direction, float distance)
    {
        // QueryTriggerInteraction.Ignore para que el propio trigger de daño
        // no se tape la vista a si mismo
        if (!Physics.Raycast(eye, direction, out RaycastHit hit, distance,
                             sightBlockers, QueryTriggerInteraction.Ignore))
            return true;

        // el otro jugador no cuenta como pared
        return hit.collider.CompareTag("Player");
    }

    private void TickPatrol()
    {
        // sin puntos se queda fijo mirando: sirve como trampa de zona
        if (patrolWorld.Length == 0) return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        if (MoveTowardsFlat(patrolWorld[patrolIndex], patrolSpeed, ArriveDistance))
        {
            patrolIndex = (patrolIndex + 1) % patrolWorld.Length;
            waitTimer = waitAtPoint;
        }
    }

    private void TickChase()
    {
        if (chaseTarget == null)
        {
            state = State.Return;
            return;
        }

        // sin distancia de frenado: se le encima y el trigger cobra el golpe
        MoveTowardsFlat(chaseTarget.position, chaseSpeed, 0f);
    }

    private void TickReturn()
    {
        if (patrolWorld.Length == 0)
        {
            state = State.Patrol;
            return;
        }

        if (MoveTowardsFlat(patrolWorld[patrolIndex], patrolSpeed, ArriveDistance))
            state = State.Patrol;
    }

    private bool MoveTowardsFlat(Vector3 target, float speed, float stopDistance)
    {
        Vector3 position = transform.position;
        Vector3 flatTarget = new Vector3(target.x, position.y, target.z);
        Vector3 toTarget = flatTarget - position;

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            // solo yaw: con LookAt el zapato se inclinaba al perseguir a alguien
            // que estaba mas arriba o mas abajo
            Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (toTarget.magnitude <= stopDistance) return true;

        transform.position = Vector3.MoveTowards(position, flatTarget, speed * Time.deltaTime);

        return Vector3.Distance(transform.position, flatTarget) <= stopDistance;
    }

    private int NearestPatrolIndex()
    {
        int nearest = patrolIndex;
        float best = float.MaxValue;

        for (int i = 0; i < patrolWorld.Length; i++)
        {
            float distance = Vector3.SqrMagnitude(patrolWorld[i] - transform.position);
            if (distance >= best) continue;

            best = distance;
            nearest = i;
        }

        return nearest;
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundProbeHeight;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                             groundProbeHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            return;

        transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
    }

    private void OnTriggerEnter(Collider other) => TryHit(other);

    // tambien en Stay: si el jugador respawnea y vuelve a quedar pegado, o si el
    // enemigo lo alcanza estando quieto, Enter ya no dispara
    private void OnTriggerStay(Collider other) => TryHit(other);

    private void TryHit(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;

        var lives = other.GetComponentInParent<PlayerLives>();
        if (lives == null) return;

        if (canBeStomped && IsStomping(other))
        {
            NetworkObject.Despawn(true);
            return;
        }

        // PlayerLives ya trae su propia ventana de gracia, no hace falta
        // un cooldown aparte aca
        lives.LoseLifeFromServer();
    }

    private bool IsStomping(Collider playerCollider)
    {
        if (damageCollider == null) return false;

        // el rigidbody del jugador remoto es kinematic en el server, asi que su
        // velocidad no sirve: alcanza con que los pies esten por encima
        return playerCollider.bounds.min.y >= damageCollider.bounds.max.y - stompTolerance;
    }

    private void LateUpdate()
    {
        if (modelRoot == null) return;

        // el saltito lo calcula cada peer con el delta de posicion, asi el
        // cliente lo ve moverse igual sin mandar nada por red
        float delta = (transform.position - lastVisualPos).magnitude;
        lastVisualPos = transform.position;

        float speed = delta / Mathf.Max(Time.deltaTime, 0.0001f);
        float intensity = Mathf.Clamp01(speed / Mathf.Max(patrolSpeed, 0.01f));

        hopPhase += Time.deltaTime * hopFrequency * intensity;

        float height = Mathf.Abs(Mathf.Sin(hopPhase)) * hopHeight * intensity;
        modelRoot.localPosition = modelBaseLocalPos + Vector3.up * height;
    }

    private void OnDrawGizmosSelected()
    {
        DrawVisionGizmo();
        DrawPatrolGizmo();
    }

    private void DrawVisionGizmo()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(eye, visionRange);

        Vector3 left = Quaternion.Euler(0f, -visionHalfAngle, 0f) * transform.forward * visionRange;
        Vector3 right = Quaternion.Euler(0f, visionHalfAngle, 0f) * transform.forward * visionRange;

        Gizmos.DrawLine(eye, eye + left);
        Gizmos.DrawLine(eye, eye + right);
    }

    private void DrawPatrolGizmo()
    {
        // en play usa las posiciones cacheadas, en editor las del inspector
        int count = Application.isPlaying ? patrolWorld.Length
                                          : (patrolPoints != null ? patrolPoints.Length : 0);
        if (count == 0) return;

        Gizmos.color = Color.cyan;

        Vector3 previous = Vector3.zero;
        bool hasPrevious = false;
        Vector3 first = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            if (!TryGetPatrolPoint(i, out Vector3 point)) continue;

            Gizmos.DrawWireSphere(point, 0.2f);

            if (hasPrevious) Gizmos.DrawLine(previous, point);
            else first = point;

            previous = point;
            hasPrevious = true;
        }

        // cierra la ronda porque el recorrido es ciclico
        if (hasPrevious && previous != first) Gizmos.DrawLine(previous, first);
    }

    private bool TryGetPatrolPoint(int index, out Vector3 point)
    {
        if (Application.isPlaying)
        {
            point = patrolWorld[index];
            return true;
        }

        Transform transformPoint = patrolPoints[index];
        point = transformPoint != null ? transformPoint.position : Vector3.zero;
        return transformPoint != null;
    }
}
