using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.AI;
using System.Collections;
using cowsins;

// 몬스터 체력 관리 스크립트
// IDamageable 인터페이스를 구현해서 플레이어 총알(Bullet)이 자동으로 피격 판정을 낼 수 있게 함
// 체력 수치는 MonsterData 에서 가져오므로 이 스크립트에서 따로 설정하지 않아도 됨
//
// 사용법:
//   1. 몬스터 오브젝트에 이 스크립트 추가
//   2. MonsterBrain 과 동일한 MonsterData 연결
//   3. 원하는 옵션 설정 (UI, 사망 처리 등)
[RequireComponent(typeof(MonsterBrain))]
public class MonsterHealth : MonoBehaviour, IDamageable
{
    // =====================================================================
    // 인스펙터 설정
    // =====================================================================

    [Header("몬스터 데이터")]
    [Tooltip("MonsterBrain 과 동일한 MonsterData 를 연결. 여기서 최대 체력을 가져옴")]
    public MonsterData data;

    [Header("사망 처리 설정")]
    [Tooltip("true: 사망 시 게임 오브젝트 삭제 / false: 오브젝트를 유지하고 컴포넌트만 비활성화")]
    public bool destroyOnDie = true;

    [Tooltip("destroyOnDie 가 true 일 때, 삭제까지 기다리는 시간 (초). 사망 애니메이션 길이에 맞춰 설정")]
    public float destroyDelay = 1f;

    [Tooltip("사망 시 생성할 이펙트 프리팹 (파티클 등). 없으면 비워둠")]
    public GameObject deathEffect;

    [Header("피격 이펙트 설정")]
    [Tooltip("피격 시 생성할 출혈 이펙트 프리팹. 인스펙터에서 연결하면 총알이 맞을 때마다 피격 위치에 이펙트가 생성됨")]
    public GameObject hitBloodEffect;

    [Tooltip("출혈 이펙트가 자동으로 삭제되기까지의 시간 (초). 파티클 재생 시간에 맞춰 설정")]
    public float hitEffectLifetime = 1f;

    [Header("체력 UI 설정")]
    [Tooltip("true: 체력 바 UI 를 표시 / false: UI 없이 사용")]
    public bool showHealthUI = false;

    [Tooltip("showHealthUI 가 true 일 때 연결할 체력 바 Image 컴포넌트 (Fill 방식)")]
    public Image healthBar;

    [Header("애니메이터 설정")]
    [Tooltip("몬스터 Animator 컴포넌트 연결. 없으면 사망 애니메이션 재생 생략")]
    public Animator monsterAnimator;

    [Tooltip("서 있을 때 사망 트리거 파라미터 이름. Animator 창의 파라미터 이름과 정확히 일치해야 함")]
    public string deathAnimationTrigger = "Die";

    [Tooltip("앉아 있을 때 (엄폐 중) 사망 트리거 파라미터 이름. 엄폐 사망 애니메이션이 없으면 deathAnimationTrigger 와 동일하게 설정해도 됨")]
    public string crouchDeathAnimationTrigger = "CrouchDie";

    [Header("사망 이벤트")]
    [Tooltip("몬스터가 스폰될 때 실행할 이벤트")]
    public UnityEvent OnSpawn;

    [Tooltip("몬스터가 피해를 입을 때마다 실행할 이벤트")]
    public UnityEvent OnDamaged;

    [Tooltip("몬스터가 사망할 때 실행할 이벤트")]
    public UnityEvent OnDeath;

    // =====================================================================
    // 내부 변수
    // =====================================================================

    // 현재 체력 (읽기 전용 프로퍼티로 외부에서 읽을 수 있게 공개)
    public float Health { get; private set; }

    // 사망 여부 플래그 (한 번 true 가 되면 이후 피격 판정을 모두 무시)
    private bool isDead = false;
    public bool IsDead => isDead;

    // 체력 바 코루틴 참조 (새 코루틴 시작 전에 기존 코루틴을 중단해서 덮어쓰기 방지)
    private Coroutine healthLerpCoroutine;

    // 컴포넌트 캐싱 (Awake 에서 한 번만 가져와서 저장, 매 프레임 GetComponent 방지)
    private MonsterBrain monsterBrain;
    private NavMeshAgent navAgent;

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Awake()
    {
        monsterBrain = GetComponent<MonsterBrain>();
        navAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"[{gameObject.name}] MonsterHealth: MonsterData 가 연결되지 않았습니다!");
            enabled = false;
            return;
        }

        Health = data.maxHealth;

        if (showHealthUI && healthBar != null)
            healthBar.fillAmount = 1f;

        OnSpawn?.Invoke();
    }

    // =====================================================================
    // IDamageable 인터페이스 구현
    // =====================================================================

    // Bullet 스크립트가 IDamageable 을 찾아서 이 메서드를 호출함
    public void TakeDamage(float attackDamage)
    {
        Damage(attackDamage, false);
    }

    public void Damage(float attackDamage, bool isHeadshot)
    {
        if (isDead) return;

        float damage = Mathf.Abs(attackDamage);
        Health -= damage;
        Health = Mathf.Max(Health, 0f);

        Debug.Log($"[{gameObject.name}] {damage} 피해! 남은 체력: {Health}/{data.maxHealth}");

        // 출혈 이펙트 생성 (hitBloodEffect 가 연결되어 있을 때만)
        // 몬스터 위치에 생성하고 hitEffectLifetime 초 후 자동 삭제
        if (hitBloodEffect != null)
        {
            GameObject blood = Instantiate(hitBloodEffect, transform.position, Quaternion.identity);
            Destroy(blood, hitEffectLifetime);
        }

        UpdateHealthBar();
        OnDamaged?.Invoke();

        if (Health <= 0f)
        {
            Die();
            return;
        }

        if (monsterBrain != null && monsterBrain.enabled)
            monsterBrain.OnHit();
    }

    // =====================================================================
    // 체력 UI
    // =====================================================================

    private void UpdateHealthBar()
    {
        if (!showHealthUI || healthBar == null) return;

        if (healthLerpCoroutine != null)
            StopCoroutine(healthLerpCoroutine);

        float targetFill = Health / data.maxHealth;
        healthLerpCoroutine = StartCoroutine(LerpHealthBar(healthBar.fillAmount, targetFill, 0.2f));
    }

    private IEnumerator LerpHealthBar(float startValue, float targetValue, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (healthBar == null) yield break;
            elapsed += Time.deltaTime;
            healthBar.fillAmount = Mathf.Lerp(startValue, targetValue, elapsed / duration);
            yield return null;
        }

        if (healthBar != null)
            healthBar.fillAmount = targetValue;
    }

    // =====================================================================
    // 사망 처리
    // =====================================================================

    private void Die()
    {
        isDead = true;
        Debug.Log($"[{gameObject.name}] 사망!");

        // ── 움직임 완전 정지 ─────────────────────────────────────────────

        if (monsterBrain != null)
            monsterBrain.enabled = false;

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        // ── 사망 애니메이션 재생 ─────────────────────────────────────────

        if (monsterAnimator != null)
        {
            // 다른 파라미터를 모두 초기화해서 사망 트리거가 묻히지 않게 함
            monsterAnimator.ResetTrigger("Attack");
            monsterAnimator.ResetTrigger("CrouchAttack");
            monsterAnimator.SetBool("IsMoving", false);
            monsterAnimator.SetBool("IsAiming", false);
            monsterAnimator.SetBool("IsCharging", false);
            monsterAnimator.SetBool("IsAlert", false);

            // 현재 앉아있는 상태인지 확인해서 다른 사망 트리거 발동
            // IsCrouching 파라미터가 true 이면 엄폐(앉기) 상태로 판단
            bool isCrouching = monsterAnimator.GetBool("IsCrouching");
            monsterAnimator.SetBool("IsCrouching", false);

            if (isCrouching && !string.IsNullOrEmpty(crouchDeathAnimationTrigger))
            {
                // 앉아있다가 사망 -> 앉아서 쓰러지는 애니메이션
                monsterAnimator.SetTrigger(crouchDeathAnimationTrigger);
                Debug.Log($"[{gameObject.name}] 엄폐 중 사망 애니메이션: {crouchDeathAnimationTrigger}");
            }
            else if (!string.IsNullOrEmpty(deathAnimationTrigger))
            {
                // 서 있다가 사망 -> 일반 사망 애니메이션
                monsterAnimator.SetTrigger(deathAnimationTrigger);
                Debug.Log($"[{gameObject.name}] 일반 사망 애니메이션: {deathAnimationTrigger}");
            }
        }

        OnDeath?.Invoke();

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        // ── 오브젝트 처리 ────────────────────────────────────────────────

        if (destroyOnDie)
            Destroy(gameObject, destroyDelay);
        else
            enabled = false;
    }
}