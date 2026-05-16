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

    [Header("피격 음성 설정")]
    [Tooltip("피격 시 재생할 신음 오디오 클립 목록. 여러 개 등록하면 매 피격마다 랜덤으로 하나 재생됨")]
    public AudioClip[] hitSoundClips;

    [Tooltip("피격 음성 쿨타임 (초). 이 시간 안에 또 피격당하면 음성이 다시 재생되지 않음\n"
           + "연속 피격 시 신음소리가 겹치는 것을 방지함")]
    [Min(0f)]
    public float hitSoundCooldown = 0.5f;

    [Tooltip("피격 음성 볼륨 (0~1)")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 1f;
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

    // 피격 음성 재생을 위한 AudioSource
    // Awake 에서 자동으로 추가하므로 인스펙터에서 따로 추가하지 않아도 됨
    private AudioSource audioSource;

    // 마지막으로 피격 음성을 재생한 시각
    // 현재 시각 - lastHitSoundTime 이 hitSoundCooldown 보다 작으면 재생하지 않음
    private float lastHitSoundTime = -999f;

    // 컴포넌트 캐싱 (Awake 에서 한 번만 가져와서 저장, 매 프레임 GetComponent 방지)
    private MonsterBrain monsterBrain;
    private NavMeshAgent navAgent;

    // MainCamera 캐싱
    // 출혈 이펙트가 카메라를 바라보도록 방향을 계산할 때 사용
    // Camera.main 은 매 호출마다 씬을 탐색하므로 Start 에서 한 번만 캐싱
    private Transform mainCameraTransform;

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Awake()
    {
        monsterBrain = GetComponent<MonsterBrain>();
        navAgent = GetComponent<NavMeshAgent>();

        // AudioSource 가 없으면 자동으로 추가
        // 피격 음성 재생에 사용. PlayOneShotPitch 로 재생하므로 루프는 꺼둠
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
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

        // MainCamera 캐싱 (피격 이펙트 방향 계산에 사용)
        if (Camera.main != null)
            mainCameraTransform = Camera.main.transform;

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

        // 출혈 이펙트 생성
        // data.showBloodEffect 가 false 이면 이펙트 생성 건너뜀
        if (hitBloodEffect != null && data.showBloodEffect)
        {
            Vector3 spawnPos = transform.position + Vector3.up;
            Collider col = GetComponent<Collider>();
            if (col != null)
                spawnPos = col.bounds.center;

            // 카메라 -> 몬스터 방향을 구한 뒤 180도 반전
            // 반전 이유: 총알이 날아온 방향의 반대쪽(몸 뒤쪽)으로 피가 튀는 것이 자연스러움
            Vector3 hitDirection = Vector3.forward;
            if (mainCameraTransform != null)
                hitDirection = (spawnPos - mainCameraTransform.position).normalized;

            Quaternion bloodRot = Quaternion.LookRotation(-hitDirection);
            GameObject blood = Instantiate(hitBloodEffect, spawnPos, bloodRot);
            Destroy(blood, hitEffectLifetime);
        }

        UpdateHealthBar();
        OnDamaged?.Invoke();

        // 피격 음성 재생
        // data.playHitSound 가 false 이면 재생하지 않음
        // 쿨타임이 지났을 때만 재생해서 연속 피격 시 겹치는 것을 방지
        if (data.playHitSound && hitSoundClips != null && hitSoundClips.Length > 0)
        {
            if (Time.time - lastHitSoundTime >= hitSoundCooldown)
            {
                lastHitSoundTime = Time.time;

                // 목록에서 랜덤으로 하나 선택해서 재생
                // PlayOneShot: 현재 재생 중인 소리를 끊지 않고 동시 재생 가능
                int index = Random.Range(0, hitSoundClips.Length);
                AudioClip clip = hitSoundClips[index];
                if (clip != null)
                    audioSource.PlayOneShot(clip, hitSoundVolume);
            }
        }

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

        // ── 아이템 드랍 ──────────────────────────────────────────────────

        // MonsterData 에서 드랍 여부 확인
        // dropItemOnDeath 가 false 이거나 드랍 프리팹 목록이 비어있으면 드랍 안 함
        if (data.dropItemOnDeath
            && data.dropItemPrefabs != null
            && data.dropItemPrefabs.Length > 0)
        {
            // 드랍 확률 체크 (Random.value: 0~1 사이 랜덤값)
            // 예: dropChance = 0.7 이면 70% 확률로 드랍
            if (Random.value <= data.dropChance)
            {
                DropItems();
            }
        }

        // ── 오브젝트 처리 ────────────────────────────────────────────────

        if (destroyOnDie)
            Destroy(gameObject, destroyDelay);
        else
            enabled = false;
    }

    // 아이템 드랍 실행 함수
    // dropItemPrefabs 목록에서 dropCount 개를 랜덤으로 골라서 몬스터 위치에 생성
    private void DropItems()
    {
        // 드랍 위치: 몬스터 발 위치에서 dropSpawnHeight 만큼 위
        Vector3 dropPosition = transform.position + Vector3.up * data.dropSpawnHeight;

        // dropCount 만큼 반복해서 아이템 드랍
        for (int i = 0; i < data.dropCount; i++)
        {
            // 프리팹 목록에서 랜덤으로 하나 선택
            // Random.Range(0, length): 0 이상 length 미만 정수 반환
            int index = Random.Range(0, data.dropItemPrefabs.Length);
            GameObject prefab = data.dropItemPrefabs[index];

            // 프리팹이 null 이면 건너뜀 (목록에 빈 슬롯이 있을 때 방지)
            if (prefab == null) continue;

            // 여러 개를 드랍할 때 같은 위치에 겹치지 않도록 살짝 랜덤 오프셋 추가
            // Random.insideUnitSphere: 반지름 1 구 안의 랜덤 방향
            // y 를 0 으로 고정하고 xz 평면에서만 퍼지게 해서 공중에 뜨지 않게 함
            Vector3 offset = Vector3.zero;
            if (data.dropCount > 1)
            {
                Vector3 randomOffset = Random.insideUnitSphere * 0.3f;
                randomOffset.y = 0f;
                offset = randomOffset;
            }

            Instantiate(prefab, dropPosition + offset, Quaternion.identity);
            Debug.Log($"[{gameObject.name}] 아이템 드랍: {prefab.name}");
        }
    }
}