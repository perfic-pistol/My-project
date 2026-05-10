using cowsins;
using UnityEngine;

// 몬스터가 발사하는 총알 프리팹에 붙이는 스크립트
// MonsterBrain 의 bulletPrefab 에 연결된 프리팹에 이 스크립트를 붙여서 사용
//
// 총알 프리팹 세팅 방법:
//   1. 빈 게임 오브젝트 또는 구(Sphere) 오브젝트 생성
//   2. 이 스크립트 부착
//   3. Rigidbody 추가 (Use Gravity 체크 해제 권장)
//   4. Collider 추가 (Is Trigger 체크)
//   5. 프리팹으로 저장 후 MonsterBrain 의 bulletPrefab 에 연결
public class MonsterBullet : MonoBehaviour
{
    [Header("총알 설정")]
    [Tooltip("총알 이동 속도 (초당 미터)")]
    public float speed = 20f;

    [Tooltip("총알이 자동으로 삭제되기까지의 시간 (초). 씬에 총알이 무한정 쌓이는 것을 방지")]
    public float lifetime = 3f;

    // MonsterBrain 에서 발사 시 자동으로 설정되는 데미지 값
    // 인스펙터에서 직접 수정하지 않아도 됨
    [HideInInspector]
    public float damage = 20f;

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Start()
    {
        // lifetime 초 후 이 게임 오브젝트를 씬에서 자동 삭제
        // 삭제하지 않으면 빗나간 총알이 씬에 계속 쌓여서 메모리 낭비 발생
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // 매 프레임 앞 방향으로 이동
        // Time.deltaTime 을 곱해야 프레임 속도와 무관하게 일정한 속도 유지
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 몬스터 자신의 콜라이더와 충돌하면 무시 (자기 자신에게 피해 방지)
        if (other.CompareTag("Monster")) return;

        // 충돌한 오브젝트에 IDamageable 인터페이스가 있으면 데미지 적용
        // TryGetComponent: GetComponent 보다 효율적 (없을 때 null 반환, GC 없음)
        if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            damageable.TakeDamage(damage);
            Debug.Log($"[총알] {other.name} 에게 {damage} 데미지!");
        }

        // 몬스터가 아닌 오브젝트에 닿으면 총알 삭제 (벽, 플레이어 등)
        Destroy(gameObject);
    }
}