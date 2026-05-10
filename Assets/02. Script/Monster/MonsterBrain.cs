using cowsins;
using UnityEngine;
using UnityEngine.AI;

// 몬스터의 모든 행동 상태를 중앙에서 관리하는 두뇌 스크립트
//
// 구조:
//   MonsterBrain  <- 상태 전환 + 명령 발행
//   MonsterPatrol <- 이동/순찰 실행
//   MonsterDetection <- 감지 결과 보고
//   MonsterAimAttack <- 조준/공격 실행
//
// 상태 흐름:
//   Patrol(순찰)
//     -> 감지 -> Alert(경계/추적)
//       -> 공격 범위 진입 -> 확률 계산
//         -> Combat_Aim(조준 후 공격)
//         -> Combat_Cover(엄폐 후 공격)
//         -> Combat_Charge(돌진 공격, 구역 제한 해제)
//       -> 플레이어 이탈 -> Alert(마지막 목격지 수색)
//         -> 수색 완료 -> Alert 대기 (Idle 복귀 없음)

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
    [Tooltip("총구 위치 오브젝트 (MonsterAimAttack 의 firePoint 와 동일하게 연결)")]
    public Transform firePoint;

    [Tooltip("총알 프리팹. 비워두면 Raycast 즉시 판정 방식 사용")]
    public GameObject bulletPrefab;

    [Tooltip("공격 판정 레이어 (Player 레이어 포함)")]
    public LayerMask attackableLayer;

    [Header("가구 엄폐 설정")]
    [Tooltip("엄폐 가능한 가구 레이어")]
    public LayerMask furnitureLayer;

    [Tooltip("가구 탐색 반경 (미터). 이 범위 안의 가구를 엄폐물로 사용")]
    public float coverSearchRadius = 6f;

    [Header("애니메이터")]
    [Tooltip("Animator 컴포넌트 연결 (없어도 동작)")]
    public Animator monsterAnimator;

    // =====================================================================
    // 전체 상태 열거형
    // =====================================================================

    public enum BrainState
    {
        Patrol,          // 구역 안 자유 순찰
        Alert,           // 감지 후 경계 + 추적
        Search,          // 마지막 목격지 도착 후 주변 경계 대기
        Combat_Aim,      // 조준 후 공격
        Combat_Cover,    // 가구 엄폐 후 공격
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

    // 현재 조준 중인 플레이어
    private Transform combatTarget;

    // 조준 시작 시각
    private float aimStartTime;

    // 마지막 공격 시각
    private float lastAttackTime = -999f;

    // 돌진 시작 시각
    private float chargeStartTime;

    // 돌진이 한 번이라도 발동되면 true 로 설정
    // 이 플래그가 true 인 동안은 구역 복귀, Search, Patrol 전환 완전 차단
    // MonsterDetection 에서 읽어서 돌진 광역 감지를 활성화하는 데 사용
    private bool isChargeActivated = false;
    public bool IsChargeActivated => isChargeActivated;

    // 돌진 공격 내부 페이즈
    // Chasing: 플레이어 추격 중
    // Shooting: 멈추고 조준 사격 중
    private enum ChargePhase { Chasing, Shooting }
    private ChargePhase chargePhase = ChargePhase.Chasing;

    // 돌진 중 조준 시작 시각 (Shooting 페이즈 전환 시 기록)
    private float chargeAimStartTime;

    // 돌진 중 사격 후 다시 추격을 시작하기까지의 짧은 대기 시각
    private float chargeResumeTime;

    // NavMesh 경로 거리 캐시
    // 매 프레임 CalculatePath 를 호출하면 10마리 기준 부하가 생기므로
    // pathCheckInterval 마다 한 번만 계산하고 결과를 재사용
    private float cachedPathDistance = float.MaxValue;
    private float pathCheckTimer = 0f;

    // NavMeshPath 를 필드로 미리 할당해서 GC 부하 방지 (매번 new 금지)
    private NavMeshPath navPath;

    [Header("경로 계산 최적화")]
    [Tooltip("NavMesh 경로 거리를 몇 초마다 재계산할지 설정. 값이 클수록 성능이 좋지만 반응이 느려짐")]
    [Range(0.05f, 0.5f)]
    public float pathCheckInterval = 0.15f;

    [Header("돌진 공격 세부 설정")]
    [Tooltip("돌진 중 멈추고 조준 사격을 시작하는 거리 (미터). 이 거리 안에 들어오면 멈춤")]
    public float chargeShootDistance = 8f;

    [Tooltip("돌진 중 조준 사격 후 다시 추격 재개까지 대기 시간 (초)")]
    public float chargeResumeDelay = 1.2f;

    [Tooltip("돌진 중 조준 시간 (초). aimDuration 보다 짧게 설정하면 더 공격적으로 보임")]
    public float chargeAimDuration = 0.6f;

    // 엄폐 위치
    private Vector3 coverPosition;

    // 엄폐 완료 여부
    private bool isAtCover = false;

    // 수색 대기 타이머 (마지막 목격지 도착 후 일정 시간 경계하다가 순찰 복귀)
    private float searchWaitTimer = 0f;

    [Header("수색 설정")]
    [Tooltip("마지막 목격지 도착 후 순찰로 복귀하기까지 경계하는 시간 (초)")]
    public float searchWaitDuration = 5f;

    // =====================================================================
    // 애니메이터 파라미터 해시 캐싱 (문자열 비교 대신 해시값 사용 -> 성능 최적화)
    // =====================================================================

    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int AnimIsAiming = Animator.StringToHash("IsAiming");
    private static readonly int AnimIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimIsAlert = Animator.StringToHash("IsAlert");

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Awake()
    {
        patrol = GetComponent<MonsterPatrol>();
        detection = GetComponent<MonsterDetection>();
        navAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"[{gameObject.name}] MonsterBrain: MonsterData 가 연결되지 않았습니다!");
            enabled = false;
            return;
        }

        EnterPatrol();
    }

    private void Update()
    {
        // 경로 거리 캐시 갱신 타이머
        // 전투/경계 상태일 때만 계산 (순찰/수색 중에는 불필요)
        if (CurrentState == BrainState.Alert || CurrentState == BrainState.Combat_Charge)
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

        // 감지 기억 초기화 (플레이어를 처음 보는 상태로 리셋)
        // isChargeActivated 는 몬스터가 죽을 때까지 유지 -> 리셋하지 않음
        detection.ResetDetection();

        patrol.ResumePatrol();
        SetAnimatorAlert(false);
        Debug.Log($"[{gameObject.name}] 상태: 순찰");
    }

    private void EnterAlert()
    {
        CurrentState = BrainState.Alert;
        patrol.StopPatrol();
        patrol.SetChaseSpeed();
        SetAnimatorAlert(true);
        Debug.Log($"[{gameObject.name}] 상태: 경계/추적");
    }

    private void EnterCombatAim(Transform target)
    {
        CurrentState = BrainState.Combat_Aim;
        combatTarget = target;
        aimStartTime = Time.time;

        // 조준 중 이동 정지
        patrol.StopPatrol();

        if (monsterAnimator != null)
            monsterAnimator.SetBool(AnimIsAiming, true);

        Debug.Log($"[{gameObject.name}] 상태: 조준 공격");
    }

    private void EnterCombatCover(Transform target, Vector3 coverPos)
    {
        CurrentState = BrainState.Combat_Cover;
        combatTarget = target;
        coverPosition = coverPos;
        isAtCover = false;

        // 엄폐 위치로 이동 시작
        patrol.StopPatrol();
        navAgent.isStopped = false;
        navAgent.SetDestination(coverPosition);

        Debug.Log($"[{gameObject.name}] 상태: 엄폐 후 공격");
    }

    private void EnterCombatCharge(Transform target)
    {
        CurrentState = BrainState.Combat_Charge;
        combatTarget = target;
        chargeStartTime = Time.time;

        // 돌진 발동 플래그 설정 - 이후 구역 복귀/순찰 전환 완전 차단
        isChargeActivated = true;

        // 페이즈 초기화: 항상 추격부터 시작
        chargePhase = ChargePhase.Chasing;

        // 돌진: 구역 제한 해제 + 돌진 속도로 즉시 추격 시작
        patrol.StopPatrol();
        patrol.SetChargeSpeed();
        navAgent.isStopped = false;

        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(AnimIsAiming, false);
            monsterAnimator.SetBool(AnimIsCharging, true);
        }

        Debug.Log($"[{gameObject.name}] 상태: 돌진 공격! (구역 제한 해제, 추격-사격 반복)");
    }

    // =====================================================================
    // 상태별 Update 함수 (매 프레임 실행)
    // =====================================================================

    // 순찰 상태: 감지 여부만 감시
    private void UpdatePatrol()
    {
        if (detection.HasDetectedPlayer)
            EnterAlert();
    }

    // 경계/추적 상태: 플레이어 추적 + 공격 범위 진입 시 전투 분기
    private void UpdateAlert()
    {
        // 플레이어가 현재 보이면 추적
        if (detection.IsDetectingPlayer && detection.DetectedPlayer != null)
        {
            Transform player = detection.DetectedPlayer;

            // 활동 구역 이탈 방지
            // 돌진 공격 중이거나 돌진이 한 번이라도 발동된 경우 구역 제한 완전 무시
            if (patrol.IsOutsideZone() && !isChargeActivated)
            {
                patrol.ReturnToZone();
                return;
            }

            // NavMesh 경로 거리로 공격 범위 판단
            // 직선거리 대신 실제 이동 거리를 사용해서 위층/아래층 오판 방지
            float distToPlayer = GetNavMeshPathDistance(player.position);

            // 공격 범위 안에 들어오면 전투 상태 결정
            if (distToPlayer <= data.attackRange)
            {
                DecideCombatBehavior(player);
                return;
            }

            // 공격 범위 밖이면 계속 추적 (위층이면 계단으로 이동)
            navAgent.isStopped = false;
            navAgent.SetDestination(player.position);
        }
        else
        {
            // 플레이어가 안 보임
            // 돌진 발동 이력이 있으면 Search/Patrol 전환 없이 마지막 위치로 계속 추격
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

    // 수색 대기 상태: 마지막 목격지에 도착, 그 방향을 바라보며 경계
    private void EnterSearch()
    {
        CurrentState = BrainState.Search;
        searchWaitTimer = 0f;

        // 그 자리에서 멈춤
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        Debug.Log($"[{gameObject.name}] 마지막 목격지 도착. {searchWaitDuration}초 경계 후 순찰 복귀");
    }

    private void UpdateSearch()
    {
        // 수색 중 플레이어가 다시 감지되면 즉시 Alert 로 복귀
        if (detection.IsDetectingPlayer && detection.DetectedPlayer != null)
        {
            EnterAlert();
            return;
        }

        // 마지막으로 목격한 방향을 계속 바라봄
        Vector3 lookDir = (detection.LastKnownPlayerPosition - transform.position);
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 3f * Time.deltaTime);
        }

        // 대기 시간 카운트
        searchWaitTimer += Time.deltaTime;
        if (searchWaitTimer >= searchWaitDuration)
        {
            // 대기 시간 종료 -> 구역 안으로 복귀 후 순찰 재개
            Debug.Log($"[{gameObject.name}] 수색 종료. 순찰 복귀");
            EnterPatrol();
        }
    }

    // 공격 방식 결정: 돌진 / 가구 엄폐 / 조준
    private void DecideCombatBehavior(Transform player)
    {
        // 1순위: 돌진 공격 (chargeAttackChance 확률, 인스펙터 조절 가능)
        if (Random.value < data.chargeAttackChance)
        {
            EnterCombatCharge(player);
            return;
        }

        // 2순위: 근처에 가구가 있으면 coverChance 확률로 엄폐, 나머지는 조준 공격
        Vector3 coverPos;
        if (TryFindCoverPosition(player, out coverPos))
        {
            if (Random.value < data.coverChance)
            {
                EnterCombatCover(player, coverPos);
                return;
            }
        }

        // 3순위: 조준 후 점사 공격
        EnterCombatAim(player);
    }

    // 조준 후 공격 상태
    private void UpdateCombatAim()
    {
        // 타겟이 사라지면 경계 상태로
        if (combatTarget == null)
        {
            EnterAlert();
            return;
        }

        // 플레이어 방향으로 부드럽게 회전
        RotateTowardsTarget(combatTarget);

        float elapsed = Time.time - aimStartTime;

        // 조준 완료 전
        if (elapsed < data.aimDuration) return;

        // 조준 완료 -> 공격 가능 여부 확인
        if (Time.time - lastAttackTime < data.attackCooldown) return;

        // 공격 실행
        ExecuteAttack(combatTarget);
        lastAttackTime = Time.time;

        // 공격 후 경계 상태로 돌아가서 다음 행동 재결정
        if (monsterAnimator != null)
            monsterAnimator.SetBool(AnimIsAiming, false);

        EnterAlert();
    }

    // 엄폐 후 공격 상태
    private void UpdateCombatCover()
    {
        if (combatTarget == null)
        {
            EnterAlert();
            return;
        }

        // 엄폐 위치로 이동 중
        if (!isAtCover)
        {
            float distToCover = Vector3.Distance(transform.position, coverPosition);
            if (distToCover <= data.waypointReachDistance)
            {
                // 엄폐 위치 도착
                isAtCover = true;
                navAgent.isStopped = true;
                aimStartTime = Time.time; // 엄폐 후 조준 시작
                Debug.Log($"[{gameObject.name}] 엄폐 완료, 조준 시작");
            }
            return;
        }

        // 엄폐 완료 -> 조준 후 공격
        RotateTowardsTarget(combatTarget);

        float elapsed = Time.time - aimStartTime;
        if (elapsed < data.aimDuration) return;
        if (Time.time - lastAttackTime < data.attackCooldown) return;

        ExecuteAttack(combatTarget);
        lastAttackTime = Time.time;

        // 공격 후 다시 경계 상태로
        EnterAlert();
    }

    // 돌진 공격 상태 (구역 제한 없음)
    // 페이즈 1 Chasing: 플레이어 방향으로 돌진 속도로 추격
    // 페이즈 2 Shooting: chargeShootDistance 이내로 접근하면 멈추고 조준 사격
    // 사격 후 chargeResumeDelay 초 대기 -> 다시 Chasing 으로 반복
    // chargeDuration 초 경과 or 타겟 소실 시 돌진 종료
    private void UpdateCombatCharge()
    {
        // 감지가 끊겨도 마지막으로 알려진 플레이어 위치로 계속 추격
        // detection.DetectedPlayer 가 null 이 되어도 combatTarget 은 유지
        if (detection.DetectedPlayer != null)
            combatTarget = detection.DetectedPlayer;

        // 타겟 자체가 처음부터 없으면 종료 (매우 드문 경우)
        if (combatTarget == null)
        {
            EndCharge();
            return;
        }

        // 돌진 전체 지속 시간 초과 -> 종료
        if (Time.time - chargeStartTime > data.chargeDuration)
        {
            Debug.Log($"[{gameObject.name}] 돌진 시간 종료");
            EndCharge();
            return;
        }

        // 층간 오판 방지를 위해 NavMesh 경로 거리 사용
        float distToTarget = GetNavMeshPathDistance(combatTarget.position);

        switch (chargePhase)
        {
            case ChargePhase.Chasing:
                UpdateChasePhase(distToTarget);
                break;

            case ChargePhase.Shooting:
                UpdateShootPhase(distToTarget);
                break;
        }
    }

    // 추격 페이즈: 플레이어 방향으로 이동, 일정 거리 이내 진입 시 사격 페이즈 전환
    private void UpdateChasePhase(float distToTarget)
    {
        // 구역 제한 없이 플레이어 방향으로 전력질주 (NavMesh 경로로 층간 이동 가능)
        navAgent.isStopped = false;
        navAgent.SetDestination(combatTarget.position);

        // NavMesh 경로 거리로 사격 전환 판단 (직선거리 오판 방지)
        float navDist = GetNavMeshPathDistance(combatTarget.position);
        if (navDist <= chargeShootDistance)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            chargePhase = ChargePhase.Shooting;
            chargeAimStartTime = Time.time;

            if (monsterAnimator != null)
                monsterAnimator.SetBool(AnimIsAiming, true);

            Debug.Log($"[{gameObject.name}] 돌진 중 사격 페이즈");
        }
    }

    // 사격 페이즈: 멈추고 조준 후 공격, 이후 잠시 대기하다가 다시 추격
    private void UpdateShootPhase(float distToTarget)
    {
        // 플레이어 방향으로 회전
        RotateTowardsTarget(combatTarget);

        float elapsed = Time.time - chargeAimStartTime;

        // 조준 완료 전 대기
        if (elapsed < chargeAimDuration) return;

        // 조준 완료 -> 공격 실행 (쿨다운 무시: 돌진 공격은 빠르게)
        if (Time.time - lastAttackTime >= data.attackCooldown * 0.5f)
        {
            ExecuteAttack(combatTarget);
            lastAttackTime = Time.time;

            if (monsterAnimator != null)
                monsterAnimator.SetBool(AnimIsAiming, false);

            // 사격 후 chargeResumeDelay 초 뒤 다시 추격
            chargeResumeTime = Time.time + chargeResumeDelay;

            // 플레이어가 너무 멀리 있으면 다시 추격, 가까우면 한 번 더 사격
            if (distToTarget > chargeShootDistance * 1.5f)
            {
                chargePhase = ChargePhase.Chasing;
                patrol.SetChargeSpeed();
            }
            else
            {
                // 잠깐 대기 후 다시 Chasing 으로 (Invoke 대신 타이머로 처리)
                chargePhase = ChargePhase.Chasing;
                navAgent.isStopped = true; // chargeResumeDelay 동안 대기
            }
        }

        // chargeResumeDelay 대기 후 이동 재개
        if (chargePhase == ChargePhase.Chasing && navAgent.isStopped
            && Time.time >= chargeResumeTime)
        {
            navAgent.isStopped = false;
        }
    }

    private void EndCharge()
    {
        chargePhase = ChargePhase.Chasing; // 다음 돌진을 위해 페이즈 초기화

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

    // 점사 발수를 랜덤으로 결정 후 코루틴으로 순차 발사
    // burst2Chance + burst3Chance + 나머지(4발) 확률 구조
    private void ExecuteAttack(Transform target)
    {
        if (monsterAnimator != null)
            monsterAnimator.SetTrigger(AnimAttack);

        // 점사 발수 결정 (인스펙터의 확률값 기반)
        float roll = Random.value;
        int burstCount;
        if (roll < data.burst2Chance)
            burstCount = 2;
        else if (roll < data.burst2Chance + data.burst3Chance)
            burstCount = 3;
        else
            burstCount = 4;

        Debug.Log($"[{gameObject.name}] {burstCount}발 점사 공격!");
        StartCoroutine(BurstFireCoroutine(target, burstCount));
    }

    // 점사 코루틴: burstCount 발을 burstFireInterval 간격으로 순차 발사
    private System.Collections.IEnumerator BurstFireCoroutine(Transform target, int burstCount)
    {
        for (int i = 0; i < burstCount; i++)
        {
            // 타겟이 사라지면 점사 중단
            if (target == null) yield break;

            FireOnce(target);

            // 마지막 탄이 아니면 간격 대기
            if (i < burstCount - 1)
                yield return new WaitForSeconds(data.burstFireInterval);
        }
    }

    // 탄퍼짐이 적용된 단발 발사
    private void FireOnce(Transform target)
    {
        if (bulletPrefab != null && firePoint != null)
        {
            // 탄퍼짐 적용: firePoint 의 forward 에 랜덤 각도 추가
            Quaternion spreadRot = Quaternion.Euler(
                Random.Range(-data.spreadAngle, data.spreadAngle),  // 상하 퍼짐
                Random.Range(-data.spreadAngle, data.spreadAngle),  // 좌우 퍼짐
                0f
            );
            Quaternion finalRot = firePoint.rotation * spreadRot;

            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, finalRot);
            MonsterBullet bulletScript = bullet.GetComponent<MonsterBullet>();
            if (bulletScript != null)
                bulletScript.damage = data.attackDamage;
        }
        else
        {
            FireRaycast(target);
        }
    }

    private void FireRaycast(Transform target)
    {
        Transform origin = firePoint != null ? firePoint : transform;

        // 콜라이더 중심점 조준
        Vector3 targetCenter = target.position;
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
            targetCenter = targetCollider.bounds.center;

        Vector3 baseDirection = (targetCenter - origin.position).normalized;

        // 탄퍼짐 적용: 기준 방향에서 spreadAngle 범위 내 랜덤 회전
        Vector3 spreadDirection = Quaternion.Euler(
            Random.Range(-data.spreadAngle, data.spreadAngle),
            Random.Range(-data.spreadAngle, data.spreadAngle),
            0f
        ) * baseDirection;

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
    // 유틸리티 함수
    // =====================================================================

    // NavMesh 경로 거리 계산
    // Vector3.Distance 는 직선거리라서 위층/아래층이 가깝게 측정됨
    // NavMesh 경로 거리를 사용하면 실제 이동 거리로 비교해서 층간 오판 방지
    // 경로를 찾지 못하면 float.MaxValue 반환 -> 공격 범위 밖으로 판단해서 이동 시작
    // 캐시된 경로 거리 반환 (Update 에서 pathCheckInterval 마다 갱신)
    private float GetNavMeshPathDistance(Vector3 targetPosition)
    {
        return cachedPathDistance;
    }

    // 실제 NavMesh 경로 거리 계산 (pathCheckInterval 마다 한 번만 호출)
    private float CalcNavMeshPathDistance(Vector3 targetPosition)
    {
        // NavMeshPath 를 매번 new 로 만들면 GC 부하 발생
        // 필드로 미리 할당해서 재사용
        if (navPath == null) navPath = new NavMeshPath();

        bool pathFound = navAgent.CalculatePath(targetPosition, navPath);

        if (!pathFound || navPath.status == NavMeshPathStatus.PathInvalid)
            return float.MaxValue;

        float totalDistance = 0f;
        Vector3[] corners = navPath.corners;
        for (int i = 1; i < corners.Length; i++)
            totalDistance += Vector3.Distance(corners[i - 1], corners[i]);

        return totalDistance;
    }

    // 플레이어 방향으로 부드럽게 회전
    // Transform 을 받아서 콜라이더 중심 기준으로 회전 (측면 조준 방지)
    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 dir = (targetPosition - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.deltaTime);
    }

    // 타겟 Transform 을 받아서 콜라이더 중심을 바라보는 오버로드
    private void RotateTowardsTarget(Transform target)
    {
        // 콜라이더 중심으로 방향 계산 (없으면 position 사용)
        Vector3 targetCenter = target.position;
        Collider col = target.GetComponent<Collider>();
        if (col != null) targetCenter = col.bounds.center;

        RotateTowards(targetCenter);
    }

    // 가구 엄폐 위치 탐색
    // 플레이어 반대 방향의 가구 뒤 위치를 NavMesh 위에서 찾음
    private bool TryFindCoverPosition(Transform player, out Vector3 coverPos)
    {
        coverPos = Vector3.zero;

        // 주변 가구 탐색 (NonAlloc: 배열 재사용으로 GC 방지)
        Collider[] furnitureHits = new Collider[5];
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, coverSearchRadius, furnitureHits, furnitureLayer);

        if (count == 0) return false;

        // 가구들 중 플레이어 반대 방향에 있는 가구를 우선 선택
        Vector3 awayFromPlayer = (transform.position - player.position).normalized;
        float bestDot = -1f;
        Collider bestFurniture = null;

        for (int i = 0; i < count; i++)
        {
            if (furnitureHits[i] == null) continue;

            Vector3 dirToFurniture = (furnitureHits[i].transform.position - transform.position).normalized;
            float dot = Vector3.Dot(awayFromPlayer, dirToFurniture);

            // dot 값이 클수록 플레이어 반대 방향에 있는 가구 (엄폐하기 좋음)
            if (dot > bestDot)
            {
                bestDot = dot;
                bestFurniture = furnitureHits[i];
            }
        }

        if (bestFurniture == null) return false;

        // 가구의 플레이어 반대쪽 지점을 엄폐 위치로 설정
        Vector3 candidatePos = bestFurniture.transform.position + awayFromPlayer * 1.2f;

        // NavMesh 위의 유효한 지점인지 확인
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(candidatePos, out navHit, 2f, NavMesh.AllAreas))
        {
            coverPos = navHit.position;
            return true;
        }

        return false;
    }

    // 애니메이터 Alert 파라미터 설정
    private void SetAnimatorAlert(bool isAlert)
    {
        if (monsterAnimator != null)
            monsterAnimator.SetBool(AnimIsAlert, isAlert);
    }

    // =====================================================================
    // 에디터 기즈모: 현재 상태와 전투 관련 정보 시각화
    // =====================================================================

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        // 공격 범위 (빨간 원)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        Gizmos.DrawSphere(transform.position, data.attackRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        DrawCircleGizmo(transform.position, data.attackRange);

        // 가구 탐색 범위 (보라색 원)
        Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, coverSearchRadius);

        // 현재 상태 표시
        if (!Application.isPlaying) return;

        // 엄폐 목표 위치 (청록색 구)
        if (CurrentState == BrainState.Combat_Cover)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(coverPosition, 0.4f);
            Gizmos.DrawLine(transform.position, coverPosition);
        }

        // 전투 타겟 연결선 (빨간 선)
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
}