using cowsins;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// 몬스터의 모든 행동 상태를 중앙에서 관리하는 두뇌 스크립트
//
// 구조:
//   MonsterBrain  <- 상태 전환 + 명령 발행
//   MonsterPatrol <- 이동/순찰 실행
//   MonsterDetection <- 감지 결과 보고
//
// 상태 흐름:
//   Patrol(순찰)
//     -> 감지 -> Alert(경계/추적)
//       -> 공격 범위 진입 -> 확률 계산
//         -> Combat_Aim(조준 후 공격)
//         -> Combat_Cover(가구 뒤 엄폐 -> 앉아서 사격 반복, 플레이어가 범위 밖으로 나가야 해제)
//         -> Combat_Charge(돌진 공격, 구역 제한 해제)
//       -> 플레이어 이탈 -> Alert(마지막 목격지 수색)
//         -> 수색 완료 -> Alert 대기

[RequireComponent(typeof(MonsterPatrol))]
[RequireComponent(typeof(MonsterDetection))]
public class MonsterBrain : MonoBehaviour
{
    // =====================================================================
    // 인스펙터 설정
    // =====================================================================

    [Header("몬스터 데이터")]
    [Tooltip("MonsterPatrol, MonsterDetection 과 동일한 MonsterData 연결")]
    public MonsterData data;

    [Header("전투 설정")]
    [Tooltip("총구 위치 오브젝트")]
    public Transform firePoint;

    // 총구 이펙트 컴포넌트 (firePoint 오브젝트에 붙어있는 MonsterMuzzleEffect)
    // Awake 에서 자동으로 찾아서 캐싱하므로 인스펙터에서 따로 연결하지 않아도 됨
    private MonsterMuzzleEffect muzzleEffect;

    [Tooltip("총알 프리팹. 비워두면 Raycast 즉시 판정 방식 사용")]
    public GameObject bulletPrefab;

    [Tooltip("공격 판정 레이어 (Player 레이어 포함)")]
    public LayerMask attackableLayer;

    [Header("가구 엄폐 설정")]
    [Tooltip("엄폐 가능한 가구 레이어")]
    public LayerMask furnitureLayer;

    [Tooltip("가구 탐색 반경 (미터). 이 범위 안의 가구를 엄폐물로 사용")]
    public float coverSearchRadius = 6f;

    [Header("엄폐 유지 설정")]
    [Tooltip("엄폐 중 플레이어가 이 거리 밖으로 나가면 엄폐 해제 후 Alert 로 전환 (미터).\n"
           + "attackRange 와 동일하거나 약간 크게 설정하면 자연스러움")]
    public float coverBreakDistance = 22f;

    [Header("애니메이터")]
    [Tooltip("Animator 컴포넌트 연결 (없어도 동작)")]
    public Animator monsterAnimator;

    // =====================================================================
    // 오브젝트 풀 설정
    // =====================================================================

    [Header("총알 오브젝트 풀 설정")]
    [Tooltip("게임 시작 시 미리 만들어둘 총알 개수. 몬스터 수가 많으면 늘려주세요")]
    public int bulletPoolSize = 20;

    // 총알을 재사용하기 위한 풀 (Queue: 먼저 넣은 것을 먼저 꺼내는 자료구조)
    private Queue<GameObject> bulletPool = new Queue<GameObject>();

    // =====================================================================
    // 전체 상태 열거형
    // =====================================================================

    public enum BrainState
    {
        Patrol,          // 구역 안 자유 순찰
        Alert,           // 감지 후 경계 + 추적
        Search,          // 마지막 목격지 도착 후 주변 경계 대기
        Combat_Aim,      // 조준 후 공격
        Combat_Cover,    // 가구 엄폐 후 앉아서 반복 사격 (플레이어가 범위 밖 나갈 때까지 유지)
        Combat_Charge    // 돌진 공격 (구역 제한 해제)
    }

    public BrainState CurrentState { get; private set; } = BrainState.Patrol;

    // =====================================================================
    // 내부 참조
    // =====================================================================

    private MonsterPatrol patrol;
    private MonsterDetection detection;
    private NavMeshAgent navAgent;

    // =====================================================================
    // 전투 내부 변수
    // =====================================================================

    private Transform combatTarget;
    private float aimStartTime;
    private float lastAttackTime = -999f;
    private float chargeStartTime;

    // 돌진이 한 번이라도 발동되면 true
    // MonsterDetection 에서 읽어서 돌진 광역 감지를 활성화하는 데 사용
    private bool isChargeActivated = false;
    public bool IsChargeActivated => isChargeActivated;

    private enum ChargePhase { Chasing, Shooting }
    private ChargePhase chargePhase = ChargePhase.Chasing;

    private float chargeAimStartTime;
    private float chargeResumeTime;

    // NavMesh 경로 거리 캐시 (pathCheckInterval 마다 갱신)
    private float cachedPathDistance = float.MaxValue;
    private float pathCheckTimer = 0f;

    // NavMeshPath 미리 할당 (매번 new 하면 GC 부하)
    private NavMeshPath navPath;

    // 경로 코너 배열 미리 할당 (GetCornersNonAlloc 재사용)
    private readonly Vector3[] cornerBuffer = new Vector3[64];

    [Header("경로 계산 최적화")]
    [Tooltip("NavMesh 경로 거리를 몇 초마다 재계산할지 설정")]
    [Range(0.05f, 0.5f)]
    public float pathCheckInterval = 0.15f;

    [Header("돌진 공격 세부 설정")]
    [Tooltip("돌진 중 멈추고 조준 사격을 시작하는 거리 (미터)")]
    public float chargeShootDistance = 8f;

    [Tooltip("돌진 중 조준 사격 후 다시 추격 재개까지 대기 시간 (초)")]
    public float chargeResumeDelay = 1.2f;

    [Tooltip("돌진 중 조준 시간 (초)")]
    public float chargeAimDuration = 0.6f;

    private Vector3 coverPosition;
    private bool isAtCover = false;

    private float searchWaitTimer = 0f;

    [Header("수색 설정")]
    [Tooltip("마지막 목격지 도착 후 순찰로 복귀하기까지 경계하는 시간 (초)")]
    public float searchWaitDuration = 5f;

    // =====================================================================
    // 애니메이터 파라미터 해시 캐싱
    // 문자열 대신 해시값을 쓰면 매 프레임 문자열 비교가 없어져서 성능이 좋아짐
    // =====================================================================

    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int AnimIsAiming = Animator.StringToHash("IsAiming");
    private static readonly int AnimIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimIsAlert = Animator.StringToHash("IsAlert");

    // 엄폐 전용 파라미터 (Animator 창에서 이름 정확히 일치해야 함)
    // IsCrouching  : Bool  - 엄폐 위치에 앉아있는 상태인지
    // CrouchAttack : Trigger - 앉은 채로 사격할 때 트리거
    private static readonly int AnimIsCrouching = Animator.StringToHash("IsCrouching");
    private static readonly int AnimCrouchAttack = Animator.StringToHash("CrouchAttack");

    // IsMoving 강제 차단 플래그
    // 조준/사격/돌진 사격 페이즈처럼 몬스터가 멈춰야 하는 상황에서
    // MonsterPatrol 의 UpdateAnimator 가 NavMeshAgent velocity 잔상으로
    // IsMoving = true 를 올리는 것을 막기 위해 Update 에서 매 프레임 덮어씀
    // true 이면 IsMoving 을 강제로 false 로 유지, false 이면 MonsterPatrol 이 제어
    private bool suppressMovingAnim = false;

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Awake()
    {
        patrol = GetComponent<MonsterPatrol>();
        detection = GetComponent<MonsterDetection>();
        navAgent = GetComponent<NavMeshAgent>();

        // firePoint 오브젝트에 MonsterMuzzleEffect 가 붙어있으면 자동으로 캐싱
        // firePoint 가 없거나 MonsterMuzzleEffect 가 없으면 null 로 유지 (이펙트 없이 동작)
        if (firePoint != null)
            muzzleEffect = firePoint.GetComponent<MonsterMuzzleEffect>();
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"[{gameObject.name}] MonsterBrain: MonsterData 가 연결되지 않았습니다!");
            enabled = false;
            return;
        }

        if (bulletPrefab != null)
            InitBulletPool();

        EnterPatrol();
    }

    private void Update()
    {
        // 경로 거리 캐시 갱신
        // Alert, Combat_Cover, Combat_Charge 상태에서만 계산 (다른 상태에선 불필요)
        bool needsPathCheck = CurrentState == BrainState.Alert
                           || CurrentState == BrainState.Combat_Cover
                           || CurrentState == BrainState.Combat_Charge;

        if (needsPathCheck)
        {
            pathCheckTimer += Time.deltaTime;
            if (pathCheckTimer >= pathCheckInterval)
            {
                pathCheckTimer = 0f;
                Vector3 targetPos = (combatTarget != null)
                    ? combatTarget.position
                    : detection.LastKnownPlayerPosition;
                cachedPathDistance = CalcNavMeshPathDistance(targetPos);
            }
        }

        // IsMoving 강제 차단
        // suppressMovingAnim 이 true 인 동안 매 프레임 IsMoving = false 를 덮어씀
        // MonsterPatrol.UpdateAnimator 가 velocity 잔상으로 IsMoving 을 올리는 것을 막음
        // -> 조준/사격 중 걷기 애니메이션, 돌진 정지 사격 중 달리기 애니메이션 방지
        if (suppressMovingAnim && monsterAnimator != null)
            monsterAnimator.SetBool(AnimIsMoving, false);

        switch (CurrentState)
        {
            case BrainState.Patrol: UpdatePatrol(); break;
            case BrainState.Alert: UpdateAlert(); break;
            case BrainState.Search: UpdateSearch(); break;
            case BrainState.Combat_Aim: UpdateCombatAim(); break;
            case BrainState.Combat_Cover: UpdateCombatCover(); break;
            case BrainState.Combat_Charge: UpdateCombatCharge(); break;
        }
    }

    // =====================================================================
    // 상태별 Enter 함수 (상태 진입 시 한 번만 실행)
    // =====================================================================

    private void EnterPatrol()
    {
        CurrentState = BrainState.Patrol;
        detection.ResetDetection();
        patrol.ResumePatrol();

        // 순찰 복귀 = 교전 완전 종료
        // IsAiming, IsCrouching, IsAlert 를 모두 해제해서 순찰 Idle 로 전환
        // suppressMovingAnim 도 해제해서 순찰 이동 애니메이션이 정상 작동하게 함
        suppressMovingAnim = false;
        SetAnimatorCrouch(false);
        SetAnimatorAlert(false);
        if (monsterAnimator != null)
            monsterAnimator.SetBool(AnimIsAiming, false);

        Debug.Log($"[{gameObject.name}] 상태: 순찰");
    }

    private void EnterAlert()
    {
        CurrentState = BrainState.Alert;
        patrol.StopPatrol();
        patrol.SetChaseSpeed();

        // 엄폐 중이었다면 일어서기
        SetAnimatorCrouch(false);
        SetAnimatorAlert(true);

        // IsAiming 이 true 상태로 Alert 에 진입하는 경우 (사격 직후 쿨타임 대기)
        // -> suppressMovingAnim 을 유지해서 쿨타임 중 조준 자세가 깨지지 않게 함
        // IsAiming 이 false 인 경우 (추격, 수색 등) -> suppressMovingAnim 해제해서 이동 가능하게 함
        bool currentlyAiming = monsterAnimator != null && monsterAnimator.GetBool(AnimIsAiming);
        suppressMovingAnim = currentlyAiming;

        // IsAiming 은 여기서 끄지 않음 (의도된 동작)
        // 쿨타임 중 UpdateAlert 에서 navAgent.isStopped = true 로 멈추고
        // 쿨타임이 끝나면 DecideCombatBehavior -> EnterCombatAim 으로 다시 조준 시작
        // IsAiming 이 false 가 되는 시점:
        //   - EnterPatrol: 순찰 복귀
        //   - EnterSearch: 플레이어 놓침
        //   - SetAnimatorCrouch(false): 엄폐 해제
        //   - EnterCombatCharge: 돌진 시작

        Debug.Log($"[{gameObject.name}] 상태: 경계/추적");
    }

    private void EnterCombatAim(Transform target)
    {
        CurrentState = BrainState.Combat_Aim;
        combatTarget = target;
        aimStartTime = Time.time;

        patrol.StopPatrol();
        SetAnimatorCrouch(false);

        // 조준 시작: 멈춘 상태에서 조준하므로 IsMoving 강제 차단
        // NavMeshAgent 가 멈춰도 velocity 잔상이 남아서 IsMoving 이 잠깐 true 가 될 수 있음
        suppressMovingAnim = true;

        if (monsterAnimator != null)
            monsterAnimator.SetBool(AnimIsAiming, true);

        Debug.Log($"[{gameObject.name}] 상태: 조준 공격");
    }

    // 엄폐 상태 진입
    // 가구 뒤 엄폐 위치로 이동 시작
    // 도착 후에는 플레이어가 coverBreakDistance 밖으로 나갈 때까지 앉아서 반복 사격
    private void EnterCombatCover(Transform target, Vector3 coverPos)
    {
        CurrentState = BrainState.Combat_Cover;
        combatTarget = target;
        coverPosition = coverPos;
        isAtCover = false;

        patrol.StopPatrol();
        navAgent.isStopped = false;
        navAgent.SetDestination(coverPosition);

        // 엄폐 위치로 이동 중에는 달리기 애니메이션이 나와야 하므로 차단 해제
        // 엄폐 위치 도착 후 앉을 때 다시 suppressMovingAnim = true 로 설정
        suppressMovingAnim = false;

        // 이동 중에는 서 있는 상태
        SetAnimatorCrouch(false);

        Debug.Log($"[{gameObject.name}] 상태: 엄폐 이동 시작");
    }

    private void EnterCombatCharge(Transform target)
    {
        CurrentState = BrainState.Combat_Charge;
        combatTarget = target;
        chargeStartTime = Time.time;
        isChargeActivated = true;
        chargePhase = ChargePhase.Chasing;

        patrol.StopPatrol();
        patrol.SetChargeSpeed();
        navAgent.isStopped = false;

        // 엄폐 중이었다면 일어서기
        SetAnimatorCrouch(false);

        // 돌진 달리기 시작: IsMoving 차단 해제 (Charge_Run 이 나와야 함)
        // 멈추고 조준하는 Shooting 페이즈에서 UpdateChasePhase 가 다시 true 로 설정
        suppressMovingAnim = false;

        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(AnimIsAiming, false);
            monsterAnimator.SetBool(AnimIsCharging, true);
        }

        Debug.Log($"[{gameObject.name}] 상태: 돌진 공격!");
    }

    // =====================================================================
    // 상태별 Update 함수 (매 프레임 실행)
    // =====================================================================

    private void UpdatePatrol()
    {
        if (detection.HasDetectedPlayer)
            EnterAlert();
    }

    private void UpdateAlert()
    {
        if (detection.IsDetectingPlayer && detection.DetectedPlayer != null)
        {
            Transform player = detection.DetectedPlayer;

            if (patrol.IsOutsideZone() && !isChargeActivated)
            {
                patrol.ReturnToZone();
                return;
            }

            float distToPlayer = GetNavMeshPathDistance(player.position);

            if (distToPlayer <= data.attackRange)
            {
                // 공격 쿨타임이 아직 남아있으면 DecideCombatBehavior 를 호출하지 않음
                // 쿨타임 중에는 IsAiming = true 를 유지해서 조준 애니메이션이 계속 재생되게 함
                // 쿨타임이 끝난 후에만 다음 공격 방식을 결정
                if (Time.time - lastAttackTime < data.attackCooldown)
                {
                    // 제자리에서 플레이어 방향으로 계속 바라봄 (이동 없음)
                    navAgent.isStopped = true;
                    navAgent.velocity = Vector3.zero;
                    if (combatTarget != null)
                        RotateTowardsTarget(combatTarget);
                    return;
                }

                DecideCombatBehavior(player);
                return;
            }

            // 공격 범위 밖 -> 추격 이동
            navAgent.isStopped = false;
            navAgent.SetDestination(player.position);
        }
        else
        {
            if (isChargeActivated)
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(detection.LastKnownPlayerPosition);
            }
            else if (CurrentState != BrainState.Combat_Charge)
            {
                EnterSearch();
            }
        }
    }

    private void EnterSearch()
    {
        CurrentState = BrainState.Search;
        searchWaitTimer = 0f;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        // 수색 = 교전 종료 -> IsAiming 해제해서 Alert_Idle 애니메이션으로 전환
        // velocity 를 0 으로 직접 초기화했으므로 suppressMovingAnim 없이도
        // MonsterPatrol 이 IsMoving = false 를 정상 처리함
        suppressMovingAnim = false;
        if (monsterAnimator != null)
            monsterAnimator.SetBool(AnimIsAiming, false);

        Debug.Log($"[{gameObject.name}] 마지막 목격지 도착. {searchWaitDuration}초 경계 후 순찰 복귀");
    }

    private void UpdateSearch()
    {
        if (detection.IsDetectingPlayer && detection.DetectedPlayer != null)
        {
            EnterAlert();
            return;
        }

        Vector3 lookDir = (detection.LastKnownPlayerPosition - transform.position);
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 3f * Time.deltaTime);
        }

        searchWaitTimer += Time.deltaTime;
        if (searchWaitTimer >= searchWaitDuration)
        {
            Debug.Log($"[{gameObject.name}] 수색 종료. 순찰 복귀");
            EnterPatrol();
        }
    }

    private void DecideCombatBehavior(Transform player)
    {
        if (Random.value < data.chargeAttackChance)
        {
            EnterCombatCharge(player);
            return;
        }

        Vector3 coverPos;
        if (TryFindCoverPosition(player, out coverPos))
        {
            if (Random.value < data.coverChance)
            {
                EnterCombatCover(player, coverPos);
                return;
            }
        }

        EnterCombatAim(player);
    }

    // 조준 후 공격 (서서 사격)
    private void UpdateCombatAim()
    {
        if (combatTarget == null) { EnterAlert(); return; }

        RotateTowardsTarget(combatTarget);

        float elapsed = Time.time - aimStartTime;
        if (elapsed < data.aimDuration) return;
        if (Time.time - lastAttackTime < data.attackCooldown) return;

        ExecuteAttack(combatTarget);
        lastAttackTime = Time.time;

        // IsAiming 은 여기서 끄지 않음
        // EnterAlert 가 호출되어도 IsAiming = true 가 유지되므로
        // Animator 는 Alert_Idle 대신 Aim 상태를 계속 재생함
        // 쿨타임이 끝나면 UpdateAlert -> DecideCombatBehavior -> EnterCombatAim 에서
        // 다시 이 Update 로 진입하여 자연스럽게 다음 사격으로 이어짐
        // IsAiming 은 EnterPatrol(순찰 복귀) 또는 EnterSearch(수색) 시에만 꺼짐

        EnterAlert();
    }

    // =====================================================================
    // 엄폐 상태 (Combat_Cover)
    //
    // 동작 흐름:
    //   1단계: 엄폐 위치로 이동
    //   2단계: 도착하면 IsCrouching = true (앉기 애니메이션 전환)
    //   3단계: 플레이어 방향으로 회전 -> 조준 -> CrouchAttack 트리거 -> 사격
    //   4단계: 쿨다운 후 3단계 반복 (다른 전투 상태로 절대 자동 전환 안 됨)
    //   5단계: 플레이어가 coverBreakDistance 밖으로 나가면 일어서서 Alert 로만 전환
    // =====================================================================
    private void UpdateCombatCover()
    {
        // 타겟 오브젝트가 씬에서 삭제된 경우 (ex. 플레이어 사망)
        if (combatTarget == null)
        {
            ExitCoverToAlert();
            return;
        }

        // ── 1단계: 엄폐 위치로 이동 중 ───────────────────────────────────
        if (!isAtCover)
        {
            float distToCover = Vector3.Distance(transform.position, coverPosition);
            if (distToCover <= data.waypointReachDistance)
            {
                // 엄폐 위치 도착 -> 멈추고 앉기
                isAtCover = true;
                navAgent.isStopped = true;
                aimStartTime = Time.time;

                // 앉은 상태에서는 이동 애니메이션이 나오면 안 되므로 차단
                suppressMovingAnim = true;

                // IsCrouching 을 true 로 바꾸면 Animator 가 앉기 애니메이션으로 전환
                SetAnimatorCrouch(true);
                Debug.Log($"[{gameObject.name}] 엄폐 도착. 앉아서 사격 시작");
            }
            // 아직 이동 중이면 다음 프레임에 다시 확인
            return;
        }

        // ── 5단계: 엄폐 해제 조건 확인 ───────────────────────────────────
        // 플레이어가 coverBreakDistance 밖으로 멀어지면 엄폐 해제 후 Alert 로 전환
        // 이 조건 이외에는 절대 엄폐 상태를 스스로 해제하지 않음
        float distToPlayer = GetNavMeshPathDistance(combatTarget.position);
        if (distToPlayer > coverBreakDistance)
        {
            Debug.Log($"[{gameObject.name}] 플레이어 이탈 감지. 엄폐 해제 -> Alert");
            ExitCoverToAlert();
            return;
        }

        // ── 2~4단계: 앉은 채로 반복 사격 ─────────────────────────────────

        // 플레이어 방향으로 부드럽게 회전 (앉은 채로)
        RotateTowardsTarget(combatTarget);

        // 조준 시간 대기
        float elapsed = Time.time - aimStartTime;
        if (elapsed < data.aimDuration) return;

        // 공격 쿨다운 대기
        if (Time.time - lastAttackTime < data.attackCooldown) return;

        // 앉아서 공격 (CrouchAttack 트리거 -> 앉아서 쏘는 애니메이션)
        ExecuteCrouchAttack(combatTarget);
        lastAttackTime = Time.time;

        // 조준 타이머 리셋 -> 엄폐 상태 유지하며 다음 사격 준비
        // EnterAlert 를 호출하지 않으므로 엄폐 상태가 계속 유지됨
        aimStartTime = Time.time;
    }

    // 엄폐 해제 후 Alert 전환 헬퍼
    // 앉기 애니메이션 해제 + Alert 진입을 한 곳에서 처리
    private void ExitCoverToAlert()
    {
        SetAnimatorCrouch(false);
        EnterAlert();
    }

    // =====================================================================
    // 돌진 공격 상태
    // =====================================================================

    private void UpdateCombatCharge()
    {
        if (detection.DetectedPlayer != null)
            combatTarget = detection.DetectedPlayer;

        if (combatTarget == null) { EndCharge(); return; }

        if (Time.time - chargeStartTime > data.chargeDuration)
        {
            Debug.Log($"[{gameObject.name}] 돌진 시간 종료");
            EndCharge();
            return;
        }

        float distToTarget = GetNavMeshPathDistance(combatTarget.position);

        switch (chargePhase)
        {
            case ChargePhase.Chasing: UpdateChasePhase(distToTarget); break;
            case ChargePhase.Shooting: UpdateShootPhase(distToTarget); break;
        }
    }

    private void UpdateChasePhase(float distToTarget)
    {
        navAgent.isStopped = false;
        navAgent.SetDestination(combatTarget.position);

        // 달리는 중에는 IsMoving 차단 해제 (Charge_Run 애니메이션이 나와야 함)
        suppressMovingAnim = false;

        float navDist = GetNavMeshPathDistance(combatTarget.position);
        if (navDist <= chargeShootDistance)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            chargePhase = ChargePhase.Shooting;
            chargeAimStartTime = Time.time;

            // 멈추고 조준 사격 시작: IsMoving 차단 켜기
            // velocity 잔상으로 달리기 애니메이션이 나오는 것을 막음
            suppressMovingAnim = true;

            if (monsterAnimator != null)
                monsterAnimator.SetBool(AnimIsAiming, true);

            Debug.Log($"[{gameObject.name}] 돌진 중 사격 페이즈");
        }
    }

    private void UpdateShootPhase(float distToTarget)
    {
        RotateTowardsTarget(combatTarget);

        float elapsed = Time.time - chargeAimStartTime;
        if (elapsed < chargeAimDuration) return;

        if (Time.time - lastAttackTime >= data.attackCooldown * 0.5f)
        {
            ExecuteAttack(combatTarget);
            lastAttackTime = Time.time;
            chargeResumeTime = Time.time + chargeResumeDelay;

            if (monsterAnimator != null)
                monsterAnimator.SetBool(AnimIsAiming, false);

            // 사격 후 다시 추격 페이즈로 복귀: IsMoving 차단 해제
            // 이후 달리기 애니메이션이 정상적으로 나와야 함
            suppressMovingAnim = false;

            chargePhase = ChargePhase.Chasing;
            patrol.SetChargeSpeed();

            if (distToTarget <= chargeShootDistance * 1.5f)
                navAgent.isStopped = true;
        }

        if (chargePhase == ChargePhase.Chasing && navAgent.isStopped
            && Time.time >= chargeResumeTime)
        {
            navAgent.isStopped = false;
        }
    }

    private void EndCharge()
    {
        chargePhase = ChargePhase.Chasing;
        suppressMovingAnim = false;

        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(AnimIsCharging, false);
            monsterAnimator.SetBool(AnimIsAiming, false);
        }

        EnterAlert();
    }

    // =====================================================================
    // 공격 실행
    // =====================================================================

    // 서서 사격 (Combat_Aim, Combat_Charge 에서 사용)
    // Animator 의 Attack 트리거를 발동
    private void ExecuteAttack(Transform target)
    {
        if (monsterAnimator != null)
            monsterAnimator.SetTrigger(AnimAttack);

        float roll = Random.value;
        int burstCount;
        if (roll < data.burst2Chance)
            burstCount = 2;
        else if (roll < data.burst2Chance + data.burst3Chance)
            burstCount = 3;
        else
            burstCount = 4;

        Debug.Log($"[{gameObject.name}] {burstCount}발 점사!");
        StartCoroutine(BurstFireCoroutine(target, burstCount));
    }

    // 앉아서 사격 (Combat_Cover 에서 사용)
    // Animator 의 CrouchAttack 트리거를 발동 -> 앉아서 쏘는 애니메이션 재생
    private void ExecuteCrouchAttack(Transform target)
    {
        if (monsterAnimator != null)
            monsterAnimator.SetTrigger(AnimCrouchAttack);

        float roll = Random.value;
        int burstCount;
        if (roll < data.burst2Chance)
            burstCount = 2;
        else if (roll < data.burst2Chance + data.burst3Chance)
            burstCount = 3;
        else
            burstCount = 4;

        Debug.Log($"[{gameObject.name}] 엄폐 사격 {burstCount}발!");
        StartCoroutine(BurstFireCoroutine(target, burstCount));
    }

    private IEnumerator BurstFireCoroutine(Transform target, int burstCount)
    {
        for (int i = 0; i < burstCount; i++)
        {
            if (target == null) yield break;
            FireOnce(target);
            if (i < burstCount - 1)
                yield return new WaitForSeconds(data.burstFireInterval);
        }
    }

    private void FireOnce(Transform target)
    {
        if (bulletPrefab != null && firePoint != null)
        {
            Quaternion spreadRot = Quaternion.Euler(
                Random.Range(-data.spreadAngle, data.spreadAngle),
                Random.Range(-data.spreadAngle, data.spreadAngle),
                0f
            );
            Quaternion finalRot = firePoint.rotation * spreadRot;

            // 총알 발사 방향 벡터 (이펙트에 전달)
            Vector3 fireDirection = finalRot * Vector3.forward;

            // 총구 이펙트 재생 (머즐 플래시 + 궤도 이펙트)
            // muzzleEffect 가 null 이면 이펙트 없이 그냥 넘어감
            if (muzzleEffect != null)
                muzzleEffect.PlayFireEffect(fireDirection);

            GameObject bullet = GetBulletFromPool(firePoint.position, finalRot);
            MonsterBullet bulletScript = bullet.GetComponent<MonsterBullet>();
            if (bulletScript != null)
            {
                bulletScript.damage = data.attackDamage;
                bulletScript.SetPool(this);
            }
        }
        else
        {
            FireRaycast(target);
        }
    }

    private void FireRaycast(Transform target)
    {
        Transform origin = firePoint != null ? firePoint : transform;

        Vector3 targetCenter = target.position;
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
            targetCenter = targetCollider.bounds.center;

        Vector3 baseDirection = (targetCenter - origin.position).normalized;
        Vector3 spreadDirection = Quaternion.Euler(
            Random.Range(-data.spreadAngle, data.spreadAngle),
            Random.Range(-data.spreadAngle, data.spreadAngle),
            0f
        ) * baseDirection;

        // Raycast 방식에서도 총구 이펙트 재생
        if (muzzleEffect != null)
            muzzleEffect.PlayFireEffect(spreadDirection);

        float distance = data.attackRange;
        RaycastHit hit;
        if (Physics.Raycast(origin.position, spreadDirection, out hit, distance, attackableLayer))
        {
            if (hit.collider.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                damageable.TakeDamage(data.attackDamage);
                Debug.Log($"[{gameObject.name}] {data.attackDamage} 데미지!");
            }
        }

        Debug.DrawRay(origin.position, spreadDirection * distance, Color.red, 0.3f);
    }

    // =====================================================================
    // 오브젝트 풀 관련 함수
    // =====================================================================

    private void InitBulletPool()
    {
        for (int i = 0; i < bulletPoolSize; i++)
        {
            GameObject bullet = Instantiate(bulletPrefab);
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }
    }

    private GameObject GetBulletFromPool(Vector3 pos, Quaternion rot)
    {
        GameObject bullet;
        if (bulletPool.Count > 0)
        {
            bullet = bulletPool.Dequeue();
            bullet.transform.SetPositionAndRotation(pos, rot);
            bullet.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 총알 풀이 비었습니다. bulletPoolSize 를 늘려주세요.");
            bullet = Instantiate(bulletPrefab, pos, rot);
        }
        return bullet;
    }

    public void ReturnBulletToPool(GameObject bullet)
    {
        bullet.SetActive(false);
        bulletPool.Enqueue(bullet);
    }

    // =====================================================================
    // 유틸리티 함수
    // =====================================================================

    private float GetNavMeshPathDistance(Vector3 targetPosition)
    {
        return cachedPathDistance;
    }

    private float CalcNavMeshPathDistance(Vector3 targetPosition)
    {
        if (navPath == null) navPath = new NavMeshPath();

        bool pathFound = navAgent.CalculatePath(targetPosition, navPath);
        if (!pathFound || navPath.status == NavMeshPathStatus.PathInvalid)
            return float.MaxValue;

        int cornerCount = navPath.GetCornersNonAlloc(cornerBuffer);
        float totalDistance = 0f;
        for (int i = 1; i < cornerCount; i++)
            totalDistance += Vector3.Distance(cornerBuffer[i - 1], cornerBuffer[i]);

        return totalDistance;
    }

    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 dir = (targetPosition - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.deltaTime);
    }

    private void RotateTowardsTarget(Transform target)
    {
        Vector3 targetCenter = target.position;
        Collider col = target.GetComponent<Collider>();
        if (col != null) targetCenter = col.bounds.center;
        RotateTowards(targetCenter);
    }

    private readonly Collider[] furnitureCoverBuffer = new Collider[5];

    private bool TryFindCoverPosition(Transform player, out Vector3 coverPos)
    {
        coverPos = Vector3.zero;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, coverSearchRadius, furnitureCoverBuffer, furnitureLayer);

        if (count == 0) return false;

        Vector3 awayFromPlayer = (transform.position - player.position).normalized;
        float bestDot = -1f;
        Collider bestFurniture = null;

        for (int i = 0; i < count; i++)
        {
            if (furnitureCoverBuffer[i] == null) continue;
            Vector3 dirToFurniture = (furnitureCoverBuffer[i].transform.position - transform.position).normalized;
            float dot = Vector3.Dot(awayFromPlayer, dirToFurniture);
            if (dot > bestDot) { bestDot = dot; bestFurniture = furnitureCoverBuffer[i]; }
        }

        if (bestFurniture == null) return false;

        Vector3 candidatePos = bestFurniture.transform.position + awayFromPlayer * 1.2f;
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(candidatePos, out navHit, 2f, NavMesh.AllAreas))
        {
            coverPos = navHit.position;
            return true;
        }

        return false;
    }

    // 앉기/일어서기 관련 파라미터를 한 곳에서 처리하는 헬퍼
    // IsCrouching Bool 과 IsAiming Bool 을 동시에 제어
    private void SetAnimatorCrouch(bool isCrouching)
    {
        if (monsterAnimator == null) return;
        monsterAnimator.SetBool(AnimIsCrouching, isCrouching);

        // 엄폐 해제 시 IsAiming 도 함께 끄기 (조준 애니메이션이 남아있지 않게)
        if (!isCrouching)
            monsterAnimator.SetBool(AnimIsAiming, false);
    }

    private void SetAnimatorAlert(bool isAlert)
    {
        if (monsterAnimator != null)
            monsterAnimator.SetBool(AnimIsAlert, isAlert);
    }

    // =====================================================================
    // 에디터 기즈모
    // =====================================================================

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        // 공격 범위 (빨간 원)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        Gizmos.DrawSphere(transform.position, data.attackRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        DrawCircleGizmo(transform.position, data.attackRange);

        // 엄폐 해제 거리 (주황 원) - attackRange 보다 약간 크게 설정하는 것을 권장
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.12f);
        Gizmos.DrawSphere(transform.position, coverBreakDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        DrawCircleGizmo(transform.position, coverBreakDistance);

        // 가구 탐색 범위 (보라색 원)
        Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, coverSearchRadius);

        if (!Application.isPlaying) return;

        if (CurrentState == BrainState.Combat_Cover)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(coverPosition, 0.4f);
            Gizmos.DrawLine(transform.position, coverPosition);
        }

        if (combatTarget != null &&
            (CurrentState == BrainState.Combat_Aim ||
             CurrentState == BrainState.Combat_Cover ||
             CurrentState == BrainState.Combat_Charge))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, combatTarget.position);
        }
    }

    private void DrawCircleGizmo(Vector3 center, float radius)
    {
        int segments = 32;
        float step = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * step * Mathf.Deg2Rad;
            float a2 = (i + 1) * step * Mathf.Deg2Rad;
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius);
            Gizmos.DrawLine(p1, p2);
        }
    }

    // =====================================================================
    // 피격 알림 (MonsterHealth 에서 호출)
    // =====================================================================

    public void OnHit()
    {
        // 엄폐 중 피격은 엄폐를 유지 (피격당해도 자리를 지킴)
        // Patrol 또는 Search 중일 때만 강제로 Alert 로 전환
        if (CurrentState != BrainState.Patrol && CurrentState != BrainState.Search) return;

        detection.ForceDetectPlayer();
        EnterAlert();
        Debug.Log($"[{gameObject.name}] 피격 감지! 강제 Alert 전환");
    }
}