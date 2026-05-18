using cowsins;
using UnityEngine;

// 보스의 근접 공격을 담당하는 컴포넌트
// 공격 상태일 때 플레이어가 meleeRange 안에 들어오면 PlayerStats.TakeDamage 를 호출함
// 보스는 무적이므로 피격 처리 없음
// BossBlackboard 가 같은 오브젝트에 있어야 함
[RequireComponent(typeof(BossBlackboard))]
public class BossAttack : MonoBehaviour
{
    [Tooltip("보스 데이터 스크립터블 오브젝트. 인스펙터에서 연결하세요")]
    public BossData MonsterData;

    // 공유 상태 데이터
    private BossBlackboard blackboard;

    // 애니메이션 및 사운드 컴포넌트 (없어도 동작함)
    private BossAnimation bossAnimation;
    private BossSound bossSound;

    // 공격 쿨타임 타이머
    private float attackCooldownTimer = 0f;

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Awake()
    {
        blackboard = GetComponent<BossBlackboard>();
        bossAnimation = GetComponent<BossAnimation>();
        bossSound = GetComponent<BossSound>();
    }

    private void Start()
    {
        if (MonsterData == null)
        {
            Debug.LogError("[BossAttack] BossData 가 연결되지 않았습니다. 인스펙터에서 MonsterData 항목을 설정하세요.");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        // 공격 쿨타임 감소
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        // 공격 상태일 때만 실행
        if (blackboard.CurrentState != BossBlackboard.BossState.Attack) return;

        TryMeleeAttack();
    }

    // =====================================================================
    // 공격 로직
    // =====================================================================

    // 플레이어가 근접 범위 안에 있으면 공격 실행
    private void TryMeleeAttack()
    {
        Transform player = blackboard.PlayerTransform;

        // 블랙보드에 플레이어 정보가 없으면 태그로 다시 탐색
        // FindGameObjectWithTag 는 비용이 있으므로 플레이어가 없을 때만 실행
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                blackboard.PlayerTransform = playerObj.transform;
                player = blackboard.PlayerTransform;
            }
            else return;
        }

        // 공격 쿨타임 중이면 대기
        if (attackCooldownTimer > 0f) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // 근접 범위 안에 있을 때만 공격
        if (distToPlayer <= MonsterData.meleeRange)
        {
            PerformMeleeAttack(player);
        }
    }

    // 실제 근접 공격 실행
    // MonsterBullet 의 OnTriggerEnter 와 동일하게 PlayerStats.TakeDamage 호출
    // GetComponentInParent 를 사용하는 이유:
    //   플레이어가 몸통, 머리 등 여러 콜라이더를 가질 때
    //   루트 오브젝트에만 PlayerStats 가 붙어있는 경우가 많기 때문
    private void PerformMeleeAttack(Transform player)
    {
        attackCooldownTimer = MonsterData.attackCooldown;

        // 공격 애니메이션 재생
        bossAnimation?.PlayAttackAnimation();

        // 공격 사운드 재생
        bossSound?.PlayAttackSound();

        // BossData 의 attackHitDelay 만큼 딜레이 후 실제 데미지 적용
        // 공격 모션의 타격 시점에 맞게 인스펙터에서 조절하세요
        StartCoroutine(ApplyDamageAfterDelay(player, MonsterData.attackHitDelay));
    }

    // attackHitDelay 초 후에 데미지를 적용하는 코루틴
    private System.Collections.IEnumerator ApplyDamageAfterDelay(Transform player, float delay)
    {
        yield return new WaitForSeconds(delay);

        // 딜레이 도중 플레이어가 사라졌으면 취소
        if (player == null) yield break;

        // 딜레이 도중 범위를 벗어났으면 취소
        // 공격 모션 중에 플레이어가 뒤로 빠졌을 때 데미지가 들어가지 않도록 방지
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > MonsterData.meleeRange * 1.5f) yield break;

        PlayerStats playerStats = player.GetComponentInParent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.TakeDamage(MonsterData.meleeDamage);
            Debug.Log($"[BossAttack] 근접 공격 적중! 플레이어에게 {MonsterData.meleeDamage} 데미지!");
        }
    }

    // =====================================================================
    // 에디터 씬 뷰 시각화
    // =====================================================================

    private void OnDrawGizmosSelected()
    {
        if (MonsterData == null) return;

        // 근접 공격 판정 범위 - 주황색
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, MonsterData.meleeRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, MonsterData.meleeRange);
    }
}