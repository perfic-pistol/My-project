using cowsins;
using UnityEngine;

// 몬스터가 발사하는 총알 프리팹에 붙이는 스크립트
// MonsterBrain 의 bulletPrefab 에 연결된 프리팹에 이 스크립트를 붙여서 사용
//
// 총알 프리팹 세팅 방법:
//   1. 빈 게임 오브젝트 또는 구(Sphere) 오브젝트 생성
//   2. 이 스크립트 부착
//   3. Rigidbody 추가 (Use Gravity 체크 해제 권장, Is Kinematic 체크)
//   4. Collider 추가 (Is Trigger 체크)
//   5. 프리팹으로 저장 후 MonsterBrain 의 bulletPrefab 에 연결
public class MonsterBullet : MonoBehaviour
{
    [Header("총알 설정")]
    [Tooltip("총알 이동 속도 (초당 미터)")]
    public float speed = 20f;

    [Tooltip("총알이 자동으로 회수되기까지의 시간 (초). 씬에 총알이 무한정 쌓이는 것을 방지")]
    public float lifetime = 3f;

    // MonsterBrain 에서 발사 시 자동으로 설정되는 데미지 값
    // 인스펙터에서 직접 수정하지 않아도 됨
    [HideInInspector]
    public float damage = 20f;

    // 총알이 이미 피격 판정을 냈는지 여부
    // true 이면 이후 충돌을 모두 무시 -> 같은 총알이 여러 번 데미지를 주는 문제 방지
    private bool hasHit = false;

    // [개선] 이 총알을 소유한 MonsterBrain 참조
    // 충돌/수명 종료 시 Destroy 대신 풀로 돌려보내기 위해 필요
    // MonsterBrain.FireOnce() 에서 SetPool() 을 통해 자동으로 설정됨
    private MonsterBrain ownerBrain = null;

    // 수명 타이머 (Start 에서 시작, 시간이 다 되면 풀로 반환)
    private float lifetimeTimer = 0f;

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void OnEnable()
    {
        // 풀에서 꺼내서 활성화될 때마다 상태 초기화
        // Start 대신 OnEnable 을 쓰는 이유:
        //   풀링 방식에서는 오브젝트가 재활성화(SetActive(true))될 때 Start 는 다시 실행되지 않음
        //   OnEnable 은 SetActive(true) 될 때마다 실행되므로 풀링에 적합함
        hasHit = false;
        lifetimeTimer = 0f;
    }

    private void Update()
    {
        // 매 프레임 앞 방향으로 이동
        // Time.deltaTime 을 곱해야 프레임 속도와 무관하게 일정한 속도 유지
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        // 수명 카운트
        // [개선] Invoke/Destroy 대신 타이머로 수명 처리 -> 풀로 반환
        lifetimeTimer += Time.deltaTime;
        if (lifetimeTimer >= lifetime)
        {
            ReturnOrDestroy();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 이미 피격 판정이 난 총알이면 이후 충돌 모두 무시
        if (hasHit) return;

        // 몬스터 자신의 콜라이더와 충돌하면 무시 (자기 자신에게 피해 방지)
        if (other.CompareTag("Monster")) return;

        // 플레이어 판정
        // 충돌한 콜라이더 본인 또는 부모 오브젝트에서 PlayerStats 를 탐색
        // 이유: 플레이어가 몸통/머리 등 콜라이더를 여러 개 가질 때
        //       자식 콜라이더에는 Player 태그가 없고 루트에만 있는 경우가 많음
        //       GetComponentInParent 는 자기 자신 포함 부모 방향으로 올라가며 탐색하므로
        //       어떤 콜라이더에 맞아도 PlayerStats 를 정확히 찾을 수 있음
        PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
        if (playerStats != null)
        {
            // hasHit 을 먼저 true 로 설정
            // 같은 프레임에 다른 콜라이더가 OnTriggerEnter 를 또 발생시켜도 데미지 중복 방지
            hasHit = true;
            playerStats.TakeDamage(damage);
            Debug.Log($"[총알] 플레이어에게 {damage} 데미지!");
            ReturnOrDestroy();
            return;
        }

        // 플레이어 이외의 IDamageable (다른 몬스터, 오브젝트 등)
        // 마찬가지로 부모까지 탐색
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            hasHit = true;
            damageable.TakeDamage(damage);
            Debug.Log($"[총알] {other.name} 에게 {damage} 데미지!");
            ReturnOrDestroy();
            return;
        }

        // IDamageable 이 없는 오브젝트 (벽, 바닥 등) 에 닿으면 총알 회수
        hasHit = true;
        ReturnOrDestroy();
    }

    // =====================================================================
    // [개선] 풀 설정 및 반환 관련 함수
    // =====================================================================

    // MonsterBrain 이 총알을 발사할 때 호출해서 풀 참조를 전달하는 함수
    // ownerBrain 이 설정된 총알은 Destroy 대신 풀로 반환됨
    public void SetPool(MonsterBrain brain)
    {
        ownerBrain = brain;
    }

    // 총알을 풀로 돌려보내거나 (풀 없으면) Destroy 하는 함수
    // 수명 종료, 충돌 후 이 함수를 호출하면 됨
    private void ReturnOrDestroy()
    {
        if (ownerBrain != null)
        {
            // 풀이 있으면 비활성화 후 풀로 반환 (재사용)
            ownerBrain.ReturnBulletToPool(gameObject);
        }
        else
        {
            // 풀이 없으면 기존처럼 Destroy (Raycast 방식이거나 풀 미설정 시)
            Destroy(gameObject);
        }
    }
}