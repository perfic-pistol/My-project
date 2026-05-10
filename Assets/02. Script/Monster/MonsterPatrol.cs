using UnityEngine;
using UnityEngine.AI; // NavMeshAgent 사용을 위한 네임스페이스

// 몬스터의 순찰 및 활동 구역을 관리하는 스크립트
// NavMeshAgent가 NavMesh(네비게이션 메시) 위에서 장애물을 자동으로 피하며 이동함
// NavMesh 설정 방법: 유니티 상단 메뉴 -> Window -> AI -> Navigation -> Bake 버튼 클릭
[RequireComponent(typeof(NavMeshAgent))] // NavMeshAgent 컴포넌트가 없으면 자동으로 추가
public class MonsterPatrol : MonoBehaviour
{
    // =====================================================================
    // 인스펙터 설정값
    // =====================================================================

    [Header("몬스터 데이터 (ScriptableObject)")]
    [Tooltip("몬스터 설정값이 저장된 ScriptableObject를 여기에 연결하세요")]
    public MonsterData data;

    [Header("활동 구역 설정")]
    [Tooltip("활동 구역의 중심 Transform. 비워두면 시작 위치가 자동으로 중심이 됨")]
    public Transform zoneCenter;

    [Header("애니메이터 (선택 사항)")]
    [Tooltip("Animator 컴포넌트를 연결하면 이동/대기 애니메이션이 자동 전환됨")]
    public Animator monsterAnimator;

    // =====================================================================
    // 외부에서 읽을 수 있는 상태값
    // =====================================================================

    // 현재 순찰 상태를 나타내는 열거형
    public enum PatrolState
    {
        Moving,  // 목적지를 향해 이동 중
        Waiting, // 목적지에 도착해서 대기 중
        Stopped  // 외부(전투, 감지 등)에서 순찰을 멈춘 상태
    }

    public PatrolState CurrentState { get; private set; } = PatrolState.Waiting;

    // 현재 활동 구역 중심 위치 (외부에서 읽기 가능)
    public Vector3 ZoneCenterPosition => zoneCenterPosition;

    // =====================================================================
    // 내부 변수
    // =====================================================================

    // NavMeshAgent: 장애물 회피 + 경로 탐색을 담당하는 유니티 내장 컴포넌트
    private NavMeshAgent navAgent;

    // 활동 구역 중심 위치 (Vector3로 저장해서 매 프레임 Transform 접근 비용 절감)
    private Vector3 zoneCenterPosition;

    // 현재 이동 중인 목적지
    private Vector3 currentDestination;

    // 대기 타이머 관련
    private float waitTimer = 0f;
    private float waitDuration = 0f;

    // 애니메이터 파라미터 해시: 문자열 대신 해시값을 캐싱해서 성능 최적화
    // Animator.SetBool("IsMoving") 대신 해시값을 쓰면 매 프레임 문자열 비교가 없어짐
    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        // 구역 중심 결정: zoneCenter가 지정되어 있으면 그 위치, 없으면 시작 위치
        zoneCenterPosition = zoneCenter != null ? zoneCenter.position : transform.position;
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"[{gameObject.name}] MonsterData가 연결되지 않았습니다! 인스펙터에서 ScriptableObject를 연결해주세요.");
            enabled = false; // 데이터가 없으면 스크립트 비활성화
            return;
        }

        // NavMeshAgent에 ScriptableObject의 이동 속도 적용
        navAgent.speed = data.patrolSpeed;

        // 시작하자마자 첫 번째 목적지 선택
        SetNextPatrolDestination();
    }

    private void Update()
    {
        // 외부에서 순찰을 중단시킨 상태면 아무것도 하지 않음
        if (CurrentState == PatrolState.Stopped) return;

        switch (CurrentState)
        {
            case PatrolState.Moving:
                HandleMoving();
                break;

            case PatrolState.Waiting:
                HandleWaiting();
                break;
        }

        // 애니메이터 이동 파라미터 업데이트
        UpdateAnimator();
    }

    // =====================================================================
    // 상태 처리 함수
    // =====================================================================

    // 이동 중 상태: 목적지 도착 여부 확인
    private void HandleMoving()
    {
        // NavMeshAgent가 아직 경로를 계산 중이면 대기
        if (navAgent.pathPending) return;

        // remainingDistance: 목적지까지 남은 거리
        // waypointReachDistance 이하로 가까워지면 도착으로 판정
        if (navAgent.remainingDistance <= data.waypointReachDistance)
        {
            // 도착 -> 대기 상태 전환
            navAgent.isStopped = true;
            CurrentState = PatrolState.Waiting;

            // 대기 시간을 최솟값~최댓값 사이에서 무작위로 결정
            // 매번 같은 시간 대기하면 부자연스러워 보임
            waitDuration = Random.Range(data.patrolWaitTimeMin, data.patrolWaitTimeMax);
            waitTimer = 0f;

            Debug.Log($"[{gameObject.name}] 목적지 도착. {waitDuration:F1}초 대기");
        }
    }

    // 대기 중 상태: 대기 시간이 끝나면 다음 목적지로 출발
    private void HandleWaiting()
    {
        waitTimer += Time.deltaTime;

        if (waitTimer >= waitDuration)
        {
            SetNextPatrolDestination();
        }
    }

    // =====================================================================
    // 핵심 로직: 다음 순찰 목적지 선택
    // =====================================================================

    // 활동 구역 안에서 NavMesh 위의 유효한 위치를 무작위로 선택
    private void SetNextPatrolDestination()
    {
        Vector3 destination;

        // 유효한 목적지를 찾을 때까지 반복 (최대 시도 횟수 제한으로 무한루프 방지)
        bool found = TryGetRandomPointInZone(out destination);

        if (!found)
        {
            // 유효한 지점을 못 찾으면 구역 중심으로 이동
            destination = zoneCenterPosition;
            Debug.LogWarning($"[{gameObject.name}] 유효한 순찰 지점을 찾지 못해 구역 중심으로 이동합니다.");
        }

        currentDestination = destination;
        navAgent.isStopped = false;
        navAgent.SetDestination(currentDestination);
        CurrentState = PatrolState.Moving;

        Debug.Log($"[{gameObject.name}] 새 목적지 설정: {currentDestination}");
    }

    // 구역 안에서 NavMesh 위의 무작위 지점을 찾는 함수
    // out 키워드: 함수 밖으로 값을 반환하는 방법 (return과 다르게 여러 값을 반환 가능)
    private bool TryGetRandomPointInZone(out Vector3 result)
    {
        for (int i = 0; i < data.randomPointMaxAttempts; i++)
        {
            // 구역 중심에서 반경 안의 무작위 점 생성
            // Random.insideUnitSphere: 반지름 1의 구 안의 무작위 벡터
            Vector3 randomOffset = Random.insideUnitSphere * data.zoneRadius;
            randomOffset.y = 0f; // y축(높이)은 무시 (지면 기준으로만 이동)
            Vector3 randomPoint = zoneCenterPosition + randomOffset;

            // NavMesh.SamplePosition: 해당 위치 근처의 NavMesh 위 유효한 지점 탐색
            // maxDistance: 탐색 반경, NavMesh.AllAreas: 모든 NavMesh 영역 검색
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    // =====================================================================
    // 구역 이탈 방지 (외부에서 매 프레임 또는 필요할 때 호출)
    // =====================================================================

    // 몬스터가 활동 구역을 벗어났는지 확인하고, 벗어났으면 구역 경계로 목적지 수정
    // MonsterDetection.cs 등 다른 스크립트에서 호출해서 사용
    public bool IsOutsideZone()
    {
        // zoneRadius가 0이면 구역 제한 없음
        if (data.zoneRadius <= 0f) return false;

        float distanceFromCenter = Vector3.Distance(transform.position, zoneCenterPosition);
        return distanceFromCenter > data.zoneRadius;
    }

    // 구역 경계 안쪽의 가장 가까운 지점으로 목적지를 강제 변경
    public void ReturnToZone()
    {
        // 현재 위치에서 구역 중심 방향의 단위 벡터 계산
        Vector3 directionToCenter = (zoneCenterPosition - transform.position).normalized;

        // 구역 경계 안쪽 지점 계산 (반경의 90% 지점으로 이동)
        Vector3 targetPoint = zoneCenterPosition - directionToCenter * (data.zoneRadius * 0.9f);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPoint, out hit, 3f, NavMesh.AllAreas))
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(hit.position);
            CurrentState = PatrolState.Moving;
            Debug.Log($"[{gameObject.name}] 활동 구역 이탈! 구역 안으로 복귀");
        }
    }

    // =====================================================================
    // 외부에서 순찰 제어하는 공개 함수
    // (MonsterDetection.cs, MonsterAimAttack.cs 등에서 호출)
    // =====================================================================

    // 순찰 중단 (전투 시작, 조준 중 등에서 호출)
    public void StopPatrol()
    {
        CurrentState = PatrolState.Stopped;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero; // 관성 제거
    }

    // 순찰 재개 (전투 종료, 복귀 후 등에서 호출)
    public void ResumePatrol()
    {
        navAgent.isStopped = false;
        navAgent.speed = data.patrolSpeed; // 속도를 순찰 속도로 복구
        SetNextPatrolDestination();
    }

    // 이동 속도를 전투 속도로 변경 (플레이어 추적 시 호출)
    public void SetChaseSpeed()
    {
        navAgent.speed = data.chaseSpeed;
    }

    // 이동 속도를 돌진 속도로 변경 (돌진 공격 시 호출)
    public void SetChargeSpeed()
    {
        navAgent.speed = data.chargeSpeed;
    }

    // 특정 위치로 즉시 이동 목적지 변경 (플레이어 추적, 마지막 목격지 수색 등)
    public void MoveTo(Vector3 targetPosition)
    {
        if (CurrentState == PatrolState.Stopped) return;

        navAgent.isStopped = false;
        navAgent.SetDestination(targetPosition);
        CurrentState = PatrolState.Moving;
    }

    // =====================================================================
    // 애니메이터 업데이트
    // =====================================================================

    private void UpdateAnimator()
    {
        if (monsterAnimator == null) return;

        // NavMeshAgent의 실제 이동 속도가 0.1 이상이면 이동 중으로 판단
        // velocity.magnitude: 현재 이동 속도의 크기
        bool isMoving = navAgent.velocity.magnitude > 0.1f;
        monsterAnimator.SetBool(AnimIsMoving, isMoving);
    }

    // =====================================================================
    // 에디터용 기즈모: 씬 뷰에서 활동 구역을 시각적으로 확인
    // 실제 게임에서는 보이지 않음 (에디터 전용)
    // =====================================================================
    private void OnDrawGizmosSelected()
    {
        // data가 없거나 zoneRadius가 0이면 그리지 않음
        if (data == null || data.zoneRadius <= 0f) return;

        // 구역 중심 위치 결정 (플레이 중 vs 에디터 상태 구분)
        Vector3 center = Application.isPlaying
            ? zoneCenterPosition
            : (zoneCenter != null ? zoneCenter.position : transform.position);

        // 활동 구역 원 (초록색 반투명)
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(center, data.zoneRadius);

        // 활동 구역 테두리 (초록색 선)
        Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
        DrawCircleGizmo(center, data.zoneRadius);

        // 감지 범위 원 (노란색 선)
        if (data.detectionRadius > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            DrawCircleGizmo(transform.position, data.detectionRadius);
        }

        // 현재 이동 목적지 표시 (파란색)
        if (Application.isPlaying && CurrentState == PatrolState.Moving)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(currentDestination, 0.3f);
            Gizmos.DrawLine(transform.position, currentDestination);
        }
    }

    // 원을 선분으로 그리는 헬퍼 함수 (유니티 기즈모는 원 그리기 함수가 없음)
    private void DrawCircleGizmo(Vector3 center, float radius)
    {
        int segments = 32; // 원을 이루는 선분 개수 (많을수록 부드럽지만 에디터 성능에 영향)
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

            Vector3 point1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0f, Mathf.Sin(angle1) * radius);
            Vector3 point2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0f, Mathf.Sin(angle2) * radius);

            Gizmos.DrawLine(point1, point2);
        }
    }
}