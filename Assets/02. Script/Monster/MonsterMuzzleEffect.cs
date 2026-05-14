using UnityEngine;

// 몬스터 총구 이펙트 스크립트
// 총알을 발사할 때마다 총구 불빛(머즐 플래시)과 총알 궤도 이펙트를 생성함
//
// 사용법:
//   1. firePoint 오브젝트에 이 스크립트 부착
//   2. 인스펙터에서 이펙트 프리팹 연결
//   3. MonsterBrain 의 FireOnce 함수 안에서 muzzleEffect.PlayFireEffect(bulletDirection) 호출
//
// 궤도 이펙트 프리팹 권장 설정:
//   - 앞으로 길게 늘어나는 파티클 또는 트레일 렌더러
//   - 프리팹 정면(+Z)이 진행 방향이 되도록 설정
public class MonsterMuzzleEffect : MonoBehaviour
{
    // =====================================================================
    // 인스펙터 설정
    // =====================================================================

    [Header("총구 불빛 설정")]
    [Tooltip("총구에서 발사 시 생성할 머즐 플래시 이펙트 프리팹")]
    public GameObject muzzleFlashPrefab;

    [Tooltip("머즐 플래시가 자동 삭제되기까지의 시간 (초). 파티클 재생 시간에 맞춰 설정")]
    public float muzzleFlashLifetime = 0.05f;

    [Header("총알 궤도 설정")]
    [Tooltip("총알이 날아가는 방향으로 생성할 궤도 이펙트 프리팹 (트레일 파티클 등)")]
    public GameObject bulletTrailPrefab;

    [Tooltip("총알 궤도 이펙트가 자동 삭제되기까지의 시간 (초)")]
    public float bulletTrailLifetime = 0.3f;

    [Tooltip("총알 궤도 이펙트가 날아가는 속도 (초당 미터). 실제 총알 speed 와 비슷하게 설정하면 자연스러움")]
    public float trailSpeed = 20f;

    [Tooltip("총알 궤도 이펙트가 날아가는 최대 거리 (미터). 이 거리를 넘으면 이펙트가 삭제됨")]
    public float trailMaxDistance = 30f;

    // =====================================================================
    // 공개 함수 (MonsterBrain 에서 발사할 때마다 호출)
    // =====================================================================

    // 총알 1발 발사 시 호출하는 함수
    // fireDirection: 총알이 날아가는 방향 벡터 (MonsterBrain 의 finalRot * Vector3.forward)
    public void PlayFireEffect(Vector3 fireDirection)
    {
        // 총구 불빛 생성
        SpawnMuzzleFlash();

        // 총알 궤도 이펙트 생성
        SpawnBulletTrail(fireDirection);
    }

    // =====================================================================
    // 내부 처리 함수
    // =====================================================================

    // 총구 위치에 머즐 플래시 이펙트 생성
    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null) return;

        // 총구 위치와 방향으로 생성 (transform = firePoint 오브젝트)
        GameObject flash = Instantiate(
            muzzleFlashPrefab,
            transform.position,
            transform.rotation
        );

        // 총구에 붙어서 같이 움직이도록 자식으로 설정
        flash.transform.SetParent(transform);

        // muzzleFlashLifetime 초 후 자동 삭제
        Destroy(flash, muzzleFlashLifetime);
    }

    // 총알 궤도 이펙트를 총구에서 생성하고 앞으로 이동시킴
    private void SpawnBulletTrail(Vector3 fireDirection)
    {
        if (bulletTrailPrefab == null) return;

        // 방향 벡터가 0이면 생성하지 않음 (비정상 호출 방지)
        if (fireDirection.sqrMagnitude < 0.001f) return;

        // 총구 위치에서 총알 날아가는 방향으로 회전해서 생성
        Quaternion trailRot = Quaternion.LookRotation(fireDirection.normalized);
        GameObject trail = Instantiate(bulletTrailPrefab, transform.position, trailRot);

        // 궤도 이펙트를 앞으로 이동시키는 컴포넌트 추가
        // 이 컴포넌트가 trailSpeed 로 이동하고 trailMaxDistance 에 도달하면 삭제
        BulletTrailMover mover = trail.AddComponent<BulletTrailMover>();
        mover.Initialize(fireDirection.normalized, trailSpeed, trailMaxDistance, bulletTrailLifetime);
    }
}

// =========================================================================
// 총알 궤도 이펙트 이동 처리 컴포넌트
// MonsterMuzzleEffect 가 자동으로 추가하므로 따로 프리팹에 붙일 필요 없음
// =========================================================================
public class BulletTrailMover : MonoBehaviour
{
    // 이동 방향
    private Vector3 direction;

    // 이동 속도 (초당 미터)
    private float speed;

    // 최대 이동 거리 (미터). 이 거리를 넘으면 자동 삭제
    private float maxDistance;

    // 수명 제한 (초). 거리와 시간 중 먼저 도달하는 조건으로 삭제
    private float lifetime;

    // 현재까지 이동한 거리
    private float traveledDistance = 0f;

    // 수명 타이머
    private float timer = 0f;

    // MonsterMuzzleEffect 에서 생성 후 바로 호출해서 값을 설정하는 초기화 함수
    public void Initialize(Vector3 dir, float spd, float maxDist, float life)
    {
        direction = dir;
        speed = spd;
        maxDistance = maxDist;
        lifetime = life;
    }

    private void Update()
    {
        // 매 프레임 방향으로 이동
        float moveAmount = speed * Time.deltaTime;
        transform.Translate(Vector3.forward * moveAmount, Space.Self);
        traveledDistance += moveAmount;

        // 수명 카운트
        timer += Time.deltaTime;

        // 최대 거리 초과 또는 수명 종료 시 삭제
        if (traveledDistance >= maxDistance || timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}