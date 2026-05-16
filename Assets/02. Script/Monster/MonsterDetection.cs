using UnityEngine;

// 몬스터의 플레이어 감지를 담당하는 스크립트
// MonsterPatrol.cs 와 함께 같은 몬스터 오브젝트에 붙여서 사용
//
// 감지 방식 2가지:
//   1. 원형 범위 감지: 몬스터 주변 일정 반경 안에 플레이어가 들어오면 감지 시도
//   2. 시야 감지: 몬스터 정면 시야각 안에 플레이어가 보이면 감지 시도
//
// 두 방식 모두 플레이어와 몬스터 사이에 벽이 있으면 감지하지 못함
// 한 번 감지하면 hasDetectedPlayer 플래그가 true 가 되어 경계 상태 유지
[RequireComponent(typeof(MonsterPatrol))]
public class MonsterDetection : MonoBehaviour
{
    // =====================================================================
    // 인스펙터 설정값
    // =====================================================================

    [Header("몬스터 데이터 (MonsterPatrol 과 동일한 ScriptableObject 연결)")]
    [Tooltip("MonsterPatrol 에 연결한 것과 같은 MonsterData ScriptableObject 를 연결하세요")]
    public MonsterData data;

    [Header("시야 기준점")]
    [Tooltip("몬스터의 눈 위치 Transform (머리 또는 카메라 위치 오브젝트 연결). 비워두면 몬스터 오브젝트 중심 사용")]
    public Transform eyeTransform;

    [Header("감지 주기 최적화")]
    [Tooltip("감지 체크를 몇 초마다 한 번 실행할지 설정. 0이면 매 프레임 체크 (성능 주의)")]
    [Range(0f, 0.5f)]
    public float detectionInterval = 0.1f;

    [Header("애니메이터 (선택 사항)")]
    [Tooltip("Animator 컴포넌트 연결 시 Alert 파라미터가 자동 전환됨")]
    public Animator monsterAnimator;

    // =====================================================================
    // 외부에서 읽을 수 있는 상태값
    // =====================================================================

    // 현재 플레이어를 인식하고 있는 상태인지 (매 감지 주기마다 갱신)
    public bool IsDetectingPlayer { get; private set; } = false;

    // 플레이어를 한 번이라도 감지한 적 있는지 (한 번 true 가 되면 절대 false 로 돌아오지 않음)
    // 이 플래그가 true 이면 경계(Alert) 상태 유지
    public bool HasDetectedPlayer { get; private set; } = false;

    // 마지막으로 플레이어를 목격한 위치 (경계 상태에서 이 위치로 수색 이동)
    public Vector3 LastKnownPlayerPosition { get; private set; }

    // 현재 감지된 플레이어의 Transform (감지 중일 때만 유효, 아니면 null)
    public Transform DetectedPlayer { get; private set; } = null;

    // =====================================================================
    // 내부 변수
    // =====================================================================

    // MonsterPatrol 컴포넌트 참조 (감지 시 순찰 중단/재개 제어)
    private MonsterPatrol patrol;

    // MonsterBrain 참조 (돌진 활성화 여부 확인용)
    private MonsterBrain brain;

    // 감지 체크 타이머
    private float detectionTimer = 0f;

    // 시야 기준점 (eyeTransform 이 없으면 이 오브젝트의 Transform 사용)
    private Transform eyePoint;

    // [개선] 플레이어 Transform 캐시
    // ForceDetectPlayer() 에서 FindWithTag("Player") 는 씬 전체를 순회해서 느림
    // 한 번 찾은 뒤 여기에 저장해두고 재사용 -> 피격마다 씬 탐색하는 부하 제거
    private Transform cachedPlayerTransform;

    // 성능 최적화: Physics.OverlapSphere 결과를 저장할 배열을 미리 할당
    // new Collider[1]: 플레이어는 한 명이므로 크기 1 로 충분
    // 매 프레임 new 로 배열 생성 시 GC(가비지 컬렉터) 부하 발생 -> 미리 할당으로 방지
    private readonly Collider[] overlapResults = new Collider[1];

    // 애니메이터 파라미터 해시 캐싱 (문자열 비교보다 빠름)
    private static readonly int AnimIsAlert = Animator.StringToHash("IsAlert");
    private static readonly int AnimIsDetecting = Animator.StringToHash("IsDetecting");

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Awake()
    {
        // 같은 오브젝트의 MonsterPatrol 컴포넌트를 자동으로 찾아 저장
        patrol = GetComponent<MonsterPatrol>();
        brain = GetComponent<MonsterBrain>();

        // eyeTransform 이 연결되어 있으면 그것을 기준으로, 없으면 자기 자신 Transform 사용
        eyePoint = eyeTransform != null ? eyeTransform : transform;
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"[{gameObject.name}] MonsterDetection: MonsterData 가 연결되지 않았습니다!");
            enabled = false;
            return;
        }

        // [개선] Start 에서 플레이어를 미리 한 번 찾아 캐시
        // 이후 ForceDetectPlayer() 에서는 캐시를 재사용해서 FindWithTag 반복 호출 방지
        // 플레이어가 씬에 있을 때 Start 가 실행되므로 대부분 여기서 성공적으로 캐시됨
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] MonsterDetection: 씬에서 Player 태그 오브젝트를 찾지 못했습니다. Player 태그가 설정되어 있는지 확인하세요.");
        }
    }

    private void Update()
    {
        // detectionInterval 마다 한 번씩만 감지 체크 실행 (매 프레임 실행하면 성능 부하)
        detectionTimer += Time.deltaTime;
        if (detectionTimer < detectionInterval) return;
        detectionTimer = 0f;

        // 감지 체크 실행
        RunDetection();
    }

    // =====================================================================
    // 핵심 감지 로직
    // =====================================================================

    // 매 감지 주기마다 실행되는 메인 감지 함수
    // MonsterData 의 detectionMode 에 따라 다른 감지 방식 실행
    private void RunDetection()
    {
        bool detected = false;
        Transform playerTransform = null;

        // 돌진이 한 번이라도 활성화된 경우 -> 벽 관통 광역 감지 (chargeDetectionRadius)
        // 벽 차단 체크 없이 반경 안에 있으면 무조건 감지
        if (brain != null && brain.IsChargeActivated)
        {
            detected = TryDetectByChargeRadius(out playerTransform);
            if (detected && playerTransform != null)
            {
                OnPlayerDetected(playerTransform);
            }
            else
            {
                OnPlayerLost();
            }
            return;
        }

        switch (data.detectionMode)
        {
            // 원형 범위만 사용 (1스테이지)
            case MonsterData.DetectionMode.RadiusOnly:
                detected = TryDetectByRadius(out playerTransform);
                break;

            // 시야각만 사용
            case MonsterData.DetectionMode.SightOnly:
                detected = TryDetectBySight(out playerTransform);
                break;

            // 원형 범위 + 시야각 모두 사용 (2, 3스테이지)
            // 둘 중 하나라도 감지되면 감지 성공
            case MonsterData.DetectionMode.RadiusAndSight:
                if (!TryDetectByRadius(out playerTransform))
                    TryDetectBySight(out playerTransform);
                detected = playerTransform != null;
                break;
        }

        // 감지 결과 처리
        if (detected && playerTransform != null)
        {
            OnPlayerDetected(playerTransform);
        }
        else
        {
            OnPlayerLost();
        }
    }

    // 돌진 활성화 시 전용 감지: 벽 관통, chargeDetectionRadius 반경, 무조건 감지
    private bool TryDetectByChargeRadius(out Transform playerTransform)
    {
        playerTransform = null;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            data.chargeDetectionRadius, // 돌진 전용 광역 반경 (기본 100m)
            overlapResults,
            data.playerLayer
        );

        if (hitCount == 0) return false;

        // 벽 차단 체크 없이 바로 감지 성공 (돌진 중에는 벽 무시)
        playerTransform = overlapResults[0].transform;
        return true;
    }

    // 감지 방법 1: 원형 범위 감지
    // Physics.OverlapSphereNonAlloc: 구 안의 콜라이더를 배열에 저장 (NonAlloc = GC 없음)
    private bool TryDetectByRadius(out Transform playerTransform)
    {
        playerTransform = null;

        // OverlapSphereNonAlloc: 반환값은 배열에 채워진 콜라이더 개수
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,     // 검사 중심 (몬스터 위치)
            data.detectionRadius,   // 검사 반경
            overlapResults,         // 결과 저장 배열 (미리 할당한 배열 재사용)
            data.playerLayer        // 플레이어 레이어만 검사 (다른 오브젝트 무시)
        );

        if (hitCount == 0) return false; // 범위 안에 플레이어 없음

        // 범위 안에 플레이어가 있음 -> 벽 차단 여부 확인
        Transform candidate = overlapResults[0].transform;
        if (HasLineOfSight(candidate))
        {
            playerTransform = candidate;
            return true;
        }

        return false; // 벽으로 막혀있음
    }

    // 감지 방법 2: 시야각 감지
    private bool TryDetectBySight(out Transform playerTransform)
    {
        playerTransform = null;

        // 시야 범위(sightRange) 안에 플레이어가 있는지 먼저 확인
        int hitCount = Physics.OverlapSphereNonAlloc(
            eyePoint.position,
            data.sightRange,
            overlapResults,
            data.playerLayer
        );

        if (hitCount == 0) return false;

        Transform candidate = overlapResults[0].transform;
        Vector3 directionToPlayer = (candidate.position - eyePoint.position).normalized;

        // ── 수평 시야각 체크 (좌우) ──────────────────────────────────────
        // xz 평면에 투영해서 좌우 각도만 비교
        // y 성분을 제거하면 위아래 높이차가 수평 각도에 영향을 주지 않음
        Vector3 flatForward = eyePoint.forward;
        flatForward.y = 0f;
        flatForward = flatForward.normalized;

        Vector3 flatToPlayer = directionToPlayer;
        flatToPlayer.y = 0f;
        flatToPlayer = flatToPlayer.normalized;

        // flatForward 또는 flatToPlayer 가 0벡터이면 (정수직 방향) 수평 각도 체크 생략
        float horizontalAngle = 0f;
        if (flatForward.sqrMagnitude > 0.001f && flatToPlayer.sqrMagnitude > 0.001f)
            horizontalAngle = Vector3.Angle(flatForward, flatToPlayer);

        if (horizontalAngle > data.sightAngle * 0.5f) return false;

        // ── 수직 시야각 체크 (위아래) ────────────────────────────────────
        // 플레이어 방향의 수직 각도(고도각)를 계산
        // Mathf.Asin: 방향벡터의 y 성분으로 상하 각도를 구함
        // 결과값 범위: -90도(정아래) ~ +90도(정위)
        float verticalAngle = Mathf.Asin(Mathf.Clamp(directionToPlayer.y, -1f, 1f))
                              * Mathf.Rad2Deg;

        if (Mathf.Abs(verticalAngle) > data.sightVerticalAngle * 0.5f) return false;

        // 수평/수직 시야각 모두 통과 -> 벽 차단 여부 확인
        if (HasLineOfSight(candidate))
        {
            playerTransform = candidate;
            return true;
        }

        return false;
    }

    // 몬스터 눈 위치에서 플레이어까지 시선이 통하는지 확인 (벽 차단 검사)
    // 플레이어의 머리/중심/발 세 지점으로 Ray 를 쏴서
    // 하나라도 통과되면 감지 성공으로 처리
    // 세 지점 모두 막혀야 완전히 차단된 것으로 판단 -> 벽 모서리 틈 오판 방지
    private bool HasLineOfSight(Transform target)
    {
        Vector3 origin = eyePoint.position;

        // 플레이어 콜라이더 기준으로 발/중심/머리 세 지점 계산
        Vector3 footPos = target.position;
        Vector3 centerPos = target.position + Vector3.up;
        Vector3 headPos = target.position + Vector3.up * 1.8f;

        Collider targetCol = target.GetComponent<Collider>();
        if (targetCol != null)
        {
            Bounds b = targetCol.bounds;
            footPos = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z);
            centerPos = b.center;
            headPos = new Vector3(b.center.x, b.max.y - 0.1f, b.center.z);
        }

        // 세 지점 중 하나라도 시선이 통하면 감지 성공
        if (IsPointVisible(origin, centerPos)) return true;
        if (IsPointVisible(origin, headPos)) return true;
        if (IsPointVisible(origin, footPos)) return true;

        // 세 지점 모두 막혀있으면 감지 실패
        return false;
    }

    // origin 에서 targetPoint 까지 obstacleLayer 에 막히지 않는지 확인
    private bool IsPointVisible(Vector3 origin, Vector3 targetPoint)
    {
        Vector3 direction = (targetPoint - origin).normalized;
        float distance = Vector3.Distance(origin, targetPoint);

        bool blocked = Physics.Raycast(origin, direction, distance, data.obstacleLayer);

        Debug.DrawRay(origin, direction * distance,
            blocked ? Color.red : Color.green, detectionInterval);

        return !blocked;
    }

    // =====================================================================
    // 감지 결과 처리
    // =====================================================================

    // 플레이어를 감지했을 때 호출
    private void OnPlayerDetected(Transform player)
    {
        IsDetectingPlayer = true;
        DetectedPlayer = player;

        // 마지막 목격 위치 갱신 (Brain 의 수색 이동에 사용)
        LastKnownPlayerPosition = player.position;

        if (!HasDetectedPlayer)
        {
            Debug.Log($"[{gameObject.name}] 플레이어 최초 감지!");
            HasDetectedPlayer = true;
        }

        UpdateAnimator(isAlert: true, isDetecting: true);
    }

    // 플레이어를 현재 감지하지 못할 때 호출 (시야 밖, 벽 뒤)
    private void OnPlayerLost()
    {
        IsDetectingPlayer = false;
        DetectedPlayer = null;

        // 한 번이라도 감지한 적 있으면 경계(Alert) 애니메이션 유지
        UpdateAnimator(isAlert: HasDetectedPlayer, isDetecting: false);
    }

    // =====================================================================
    // 공개 함수 (MonsterBrain 에서 호출)
    // =====================================================================

    // 감지 기억 초기화 - MonsterBrain 의 Search 종료 시 호출
    // 이 함수를 호출하면 몬스터가 플레이어를 처음 보는 상태로 돌아감
    public void ResetDetection()
    {
        HasDetectedPlayer = false;
        IsDetectingPlayer = false;
        DetectedPlayer = null;
        LastKnownPlayerPosition = Vector3.zero;
        UpdateAnimator(isAlert: false, isDetecting: false);
        Debug.Log($"[{gameObject.name}] 감지 기억 초기화. 순찰 복귀");
    }

    // 피격 시 MonsterBrain 에서 호출
    // 감지 범위 밖에 있어도 플레이어를 강제로 감지한 것으로 처리
    public void ForceDetectPlayer()
    {
        // [개선] 캐시된 Transform 을 먼저 확인 -> FindWithTag 반복 호출 방지
        // cachedPlayerTransform 이 없을 때만 FindWithTag 로 씬 탐색 (최초 1회 or 씬 재로드 시)
        if (cachedPlayerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null)
            {
                Debug.LogWarning($"[{gameObject.name}] ForceDetectPlayer: Player 태그 오브젝트를 찾을 수 없습니다.");
                return;
            }
            cachedPlayerTransform = playerObj.transform;
        }

        // 감지 상태를 강제로 활성화
        IsDetectingPlayer = true;
        HasDetectedPlayer = true;
        DetectedPlayer = cachedPlayerTransform;
        LastKnownPlayerPosition = cachedPlayerTransform.position;

        UpdateAnimator(isAlert: true, isDetecting: true);

        Debug.Log($"[{gameObject.name}] 피격으로 플레이어 위치 강제 감지: {LastKnownPlayerPosition}");
    }

    // =====================================================================
    // 애니메이터 업데이트
    // =====================================================================

    private void UpdateAnimator(bool isAlert, bool isDetecting)
    {
        if (monsterAnimator == null) return;

        // IsAlert: 경계 상태 여부 (한 번 감지 후 계속 true)
        monsterAnimator.SetBool(AnimIsAlert, isAlert);

        // IsDetecting: 지금 이 순간 플레이어가 시야에 있는지
        monsterAnimator.SetBool(AnimIsDetecting, isDetecting);
    }

    // =====================================================================
    // 에디터 기즈모: 씬 뷰에서 감지 범위 시각화
    // =====================================================================

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        Vector3 eye = eyeTransform != null ? eyeTransform.position : transform.position;
        Vector3 forward = eyeTransform != null ? eyeTransform.forward : transform.forward;

        // 원형 범위 기즈모: RadiusOnly 또는 RadiusAndSight 일 때만 표시 (노란색)
        bool showRadius = data.detectionMode == MonsterData.DetectionMode.RadiusOnly
                       || data.detectionMode == MonsterData.DetectionMode.RadiusAndSight;
        if (showRadius)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawSphere(transform.position, data.detectionRadius);
            Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
            DrawCircleGizmo(transform.position, data.detectionRadius);
        }

        // 시야 범위 기즈모: SightOnly 또는 RadiusAndSight 일 때만 표시 (하늘색)
        bool showSight = data.detectionMode == MonsterData.DetectionMode.SightOnly
                      || data.detectionMode == MonsterData.DetectionMode.RadiusAndSight;
        if (showSight)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.1f);
            Gizmos.DrawSphere(eye, data.sightRange);

            // 수평 시야각 경계선 (좌우)
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            float halfH = data.sightAngle * 0.5f;
            Vector3 leftDir = Quaternion.Euler(0f, -halfH, 0f) * forward;
            Vector3 rightDir = Quaternion.Euler(0f, halfH, 0f) * forward;
            Gizmos.DrawRay(eye, leftDir * data.sightRange);
            Gizmos.DrawRay(eye, rightDir * data.sightRange);

            // 수직 시야각 경계선 (위아래)
            float halfV = data.sightVerticalAngle * 0.5f;

            // forward 의 수평 성분을 기준으로 위아래 경계선 계산
            Vector3 flatFwd = new Vector3(forward.x, 0f, forward.z).normalized;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;

            // 위아래 회전축: forward 의 오른쪽 방향 (Cross 로 계산)
            Vector3 rightAxis = Vector3.Cross(Vector3.up, flatFwd).normalized;
            Vector3 upDir = Quaternion.AngleAxis(-halfV, rightAxis) * flatFwd;
            Vector3 downDir = Quaternion.AngleAxis(halfV, rightAxis) * flatFwd;
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.9f);
            Gizmos.DrawRay(eye, upDir * data.sightRange);
            Gizmos.DrawRay(eye, downDir * data.sightRange);

            DrawArcGizmo(eye, forward, data.sightRange, data.sightAngle, new Color(0.3f, 0.8f, 1f, 0.5f));
        }

        // 플레이어 감지 중일 때 감지선 표시 (초록색)
        if (Application.isPlaying && IsDetectingPlayer && DetectedPlayer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(eye, DetectedPlayer.position);
        }

        // 마지막 목격 위치 표시 (주황색 구)
        if (Application.isPlaying && HasDetectedPlayer)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
            Gizmos.DrawSphere(LastKnownPlayerPosition, 0.3f);
        }
    }

    private void DrawCircleGizmo(Vector3 center, float radius)
    {
        int segments = 32;
        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * angleStep * Mathf.Deg2Rad;
            float a2 = (i + 1) * angleStep * Mathf.Deg2Rad;
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius);
            Gizmos.DrawLine(p1, p2);
        }
    }

    private void DrawArcGizmo(Vector3 center, Vector3 forward, float radius, float totalAngle, Color color)
    {
        Gizmos.color = color;
        int segments = 20;
        float halfAngle = totalAngle * 0.5f;
        float angleStep = totalAngle / segments;

        for (int i = 0; i < segments; i++)
        {
            float a1 = (-halfAngle + angleStep * i) * Mathf.Deg2Rad;
            float a2 = (-halfAngle + angleStep * (i + 1)) * Mathf.Deg2Rad;

            Vector3 p1 = center + Quaternion.Euler(0f, a1 * Mathf.Rad2Deg, 0f) * forward * radius;
            Vector3 p2 = center + Quaternion.Euler(0f, a2 * Mathf.Rad2Deg, 0f) * forward * radius;
            Gizmos.DrawLine(p1, p2);
        }
    }
}