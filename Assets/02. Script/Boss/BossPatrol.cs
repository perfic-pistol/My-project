using UnityEngine;
using UnityEngine.AI;

// 보스의 순찰과 이동을 담당하는 컴포넌트
// 상태에 따라 NavMeshAgent의 목적지를 설정함
// BossBlackboard와 NavMeshAgent가 같은 오브젝트에 있어야 함
[RequireComponent(typeof(BossBlackboard))]
[RequireComponent(typeof(NavMeshAgent))]
public class BossPatrol : MonoBehaviour
{
    [Tooltip("보스 데이터 스크립터블 오브젝트. 인스펙터에서 연결하세요")]
    public BossData MonsterData;

    private BossBlackboard blackboard;
    private NavMeshAgent agent;

    // NavMeshAgent가 활성화 상태이고 NavMesh 위에 올라가 있는지 확인하는 프로퍼티
    // agent 관련 함수를 호출하기 전에 반드시 이 값이 true인지 확인해야 함
    // NavMesh 위에 없는 상태에서 호출하면 오류 발생
    private bool IsAgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        blackboard = GetComponent<BossBlackboard>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (MonsterData == null)
        {
            Debug.LogError("[BossPatrol] BossData가 연결되지 않았습니다.");
            enabled = false;
            return;
        }

        // NavMeshAgent 기본 수치 적용
        // agent.isOnNavMesh 는 아직 false일 수 있지만
        // 수치 설정(speed, radius 등)은 NavMesh 배치 전에 해도 안전함
        ApplyAgentBaseSettings();

        // 상태 변경 이벤트 구독
        blackboard.OnStateChanged += OnStateChanged;

        // Start()는 NavMesh 배치보다 먼저 실행될 수 있으므로
        // 첫 순찰 목적지 설정은 한 프레임 뒤로 미룸
        // 이렇게 하면 NavMesh 위에 올라간 뒤 SetDestination이 호출됨
        Invoke(nameof(StartPatrol), 0.1f);
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 이벤트 구독 해제 (메모리 누수 방지)
        if (blackboard != null)
            blackboard.OnStateChanged -= OnStateChanged;
    }

    private void Update()
    {
        // agent가 NavMesh 위에 없으면 이동 관련 처리를 모두 건너뜀
        // 이 체크가 없으면 remainingDistance 호출 시 오류 발생
        if (!IsAgentReady) return;

        switch (blackboard.CurrentState)
        {
            case BossBlackboard.BossState.Patrol:
                UpdatePatrol();
                break;

            case BossBlackboard.BossState.Search:
                UpdateSearch();
                break;

            case BossBlackboard.BossState.Attack:
                UpdateAttackMovement();
                break;

            // Investigate 상태는 OnStateChanged에서 목적지를 한 번 설정하면 끝
            // 별도의 Update 처리 없이 도착을 감지해서 Search로 전환
            case BossBlackboard.BossState.Investigate:
                CheckInvestigateArrival();
                break;
        }
    }

    // ====================================================
    // 상태 변경 이벤트 수신
    // ====================================================

    // BossBlackboard의 상태가 바뀔 때 자동으로 호출됨
    private void OnStateChanged(BossBlackboard.BossState newState)
    {
        // agent가 준비되지 않았으면 상태 전환 이동 명령을 무시
        if (!IsAgentReady) return;

        switch (newState)
        {
            case BossBlackboard.BossState.Patrol:
                agent.speed = MonsterData.patrolSpeed;
                StartPatrol();
                break;

            case BossBlackboard.BossState.Investigate:
                agent.speed = MonsterData.investigateSpeed;
                agent.SetDestination(blackboard.LastLoudSoundPosition);
                break;

            case BossBlackboard.BossState.Search:
                agent.speed = MonsterData.investigateSpeed;
                blackboard.SearchTimer = MonsterData.searchTimeout;
                MoveToRandomSearchPoint();
                break;

            case BossBlackboard.BossState.Attack:
                agent.speed = MonsterData.chaseSpeed;
                blackboard.AttackLostTimer = MonsterData.attackLostTimeout;
                if (blackboard.PlayerTransform != null)
                    agent.SetDestination(blackboard.PlayerTransform.position);
                break;
        }
    }

    // ====================================================
    // 상태별 Update 처리
    // ====================================================

    // 순찰: 목적지 근처에 오면 다음 목적지를 바로 선택해서 멈추지 않고 이동
    private void UpdatePatrol()
    {
        if (!agent.pathPending && agent.remainingDistance <= MonsterData.patrolRepickDistance)
        {
            MoveToRandomWanderPoint();
        }
    }

    // 조사: 소리 난 위치에 도착하면 탐색 상태로 전환
    private void CheckInvestigateArrival()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            Debug.Log("[BossPatrol] 조사 위치 도착. 탐색 시작.");
            blackboard.CurrentState = BossBlackboard.BossState.Search;
        }
    }

    // 탐색: 소리 난 위치 주변 배회 + 타임아웃 처리
    private void UpdateSearch()
    {
        blackboard.SearchTimer -= Time.deltaTime;

        if (blackboard.SearchTimer <= 0f)
        {
            Debug.Log("[BossPatrol] 탐색 타임아웃. 순찰로 복귀.");
            blackboard.CurrentState = BossBlackboard.BossState.Patrol;
            return;
        }

        // 순찰과 동일하게 멈추지 않고 배회
        if (!agent.pathPending && agent.remainingDistance <= MonsterData.patrolRepickDistance)
        {
            MoveToRandomSearchPoint();
        }
    }

    // 공격: 플레이어 추적 및 타이머 관리
    private void UpdateAttackMovement()
    {
        blackboard.AttackLostTimer -= Time.deltaTime;

        if (blackboard.AttackLostTimer <= 0f)
        {
            Debug.Log("[BossPatrol] 추적 타임아웃. 탐색으로 복귀.");
            blackboard.CurrentState = BossBlackboard.BossState.Search;
            return;
        }

        // 플레이어가 1미터 이상 이동했을 때만 경로 재계산 (성능 최적화)
        if (blackboard.PlayerTransform != null)
        {
            float delta = Vector3.Distance(agent.destination, blackboard.PlayerTransform.position);
            if (delta > 1f)
            {
                agent.SetDestination(blackboard.PlayerTransform.position);
                blackboard.LastKnownPlayerPosition = blackboard.PlayerTransform.position;
            }
        }
    }

    // ====================================================
    // 이동 보조 함수
    // ====================================================

    // 순찰 시작 - 첫 목적지 설정
    private void StartPatrol()
    {
        // Invoke로 지연 호출될 때 이미 오브젝트가 파괴됐거나
        // agent가 아직 NavMesh 위에 없으면 건너뜀
        if (!IsAgentReady) return;

        MoveToRandomWanderPoint();
    }

    // 현재 위치 주변 랜덤 NavMesh 위치로 이동 (순찰용)
    private void MoveToRandomWanderPoint()
    {
        MoveToRandomNavMeshPoint(transform.position, MonsterData.patrolWanderRadius);
    }

    // 소리 난 위치 주변 랜덤 NavMesh 위치로 이동 (탐색용)
    private void MoveToRandomSearchPoint()
    {
        Vector3 center = blackboard.HasLoudSoundPosition
            ? blackboard.LastLoudSoundPosition
            : transform.position;

        MoveToRandomNavMeshPoint(center, MonsterData.searchRadius);
    }

    // NavMesh 위의 랜덤 위치를 찾아 이동 명령
    // 최대 10번 시도하여 유효한 위치를 찾음 (무한 루프 방지)
    private void MoveToRandomNavMeshPoint(Vector3 center, float radius)
    {
        // agent가 준비되지 않았으면 SetDestination 호출 자체를 막음
        if (!IsAgentReady) return;

        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * radius;
            randomDir += center;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, radius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }

        // 10번 시도 후 실패하면 현재 위치 근처의 NavMesh 위 점으로 이동
        NavMeshHit fallback;
        if (NavMesh.SamplePosition(transform.position, out fallback, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(fallback.position);
        }
    }

    // NavMeshAgent 기본 수치 적용
    private void ApplyAgentBaseSettings()
    {
        agent.angularSpeed = MonsterData.angularSpeed;
        agent.acceleration = MonsterData.acceleration;
        agent.stoppingDistance = MonsterData.stoppingDistance;
        agent.radius = MonsterData.agentRadius;
        agent.height = MonsterData.agentHeight;
        agent.autoBraking = true;
        agent.speed = MonsterData.patrolSpeed;
    }

    // 에디터 씬 뷰에서 순찰 및 탐색 범위 시각화
    private void OnDrawGizmosSelected()
    {
        if (MonsterData == null) return;

        // 순찰 배회 범위 - 초록색
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.08f);
        Gizmos.DrawSphere(transform.position, MonsterData.patrolWanderRadius);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, MonsterData.patrolWanderRadius);

        // 탐색 배회 범위 - 하늘색 (블랙보드에 위치 정보 있을 때만)
        if (Application.isPlaying && blackboard != null && blackboard.HasLoudSoundPosition)
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(blackboard.LastLoudSoundPosition, MonsterData.searchRadius);
        }
    }
}