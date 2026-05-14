using UnityEngine;

// ScriptableObject: 유니티 에디터에서 데이터 파일처럼 만들 수 있는 클래스
// 몬스터마다 다른 수치를 여기서 관리하면, 스크립트를 수정하지 않고
// 에디터에서 값만 바꿔서 다양한 몬스터를 만들 수 있음
//
// 사용법: 프로젝트 창에서 우클릭 -> Create -> Monster -> MonsterData
[CreateAssetMenu(fileName = "NewMonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("몬스터 이름 (에디터에서 구분용)")]
    public string monsterName = "Monster";

    [Header("체력 설정")]
    [Tooltip("몬스터 최대 체력")]
    public float maxHealth = 100f;

    [Header("이동 설정")]
    [Tooltip("순찰 이동 속도 (초당 미터)")]
    public float patrolSpeed = 2f;

    [Tooltip("전투 중 이동 속도 (초당 미터)")]
    public float chaseSpeed = 4f;

    [Tooltip("다음 순찰 지점에 도착했다고 판단하는 거리 (미터)")]
    public float waypointReachDistance = 0.5f;

    [Tooltip("순찰 지점에 도착 후 대기하는 최소 시간 (초)")]
    public float patrolWaitTimeMin = 1f;

    [Tooltip("순찰 지점에 도착 후 대기하는 최대 시간 (초)")]
    public float patrolWaitTimeMax = 3f;

    [Header("활동 구역 설정")]
    [Tooltip("구역 중심에서 이동 가능한 최대 반경 (미터). 0이면 제한 없음")]
    public float zoneRadius = 10f;

    [Tooltip("다음 순찰 목적지를 구역 안에서 무작위로 선택할 때 시도 횟수")]
    public int randomPointMaxAttempts = 10;

    // 감지 방식을 선택하는 열거형
    // RadiusOnly   = 원형 범위만 사용 (1스테이지)
    // SightOnly    = 시야각만 사용
    // RadiusAndSight = 원형 범위 + 시야각 모두 사용 (2, 3스테이지)
    public enum DetectionMode
    {
        RadiusOnly,      // 원형 범위만 사용
        SightOnly,       // 시야각만 사용
        RadiusAndSight   // 원형 범위 + 시야각 모두 사용
    }

    [Tooltip("감지 방식 선택\n- RadiusOnly: 원형 범위만 (1스테이지)\n- SightOnly: 시야각만\n- RadiusAndSight: 둘 다 사용 (2, 3스테이지)")]
    public DetectionMode detectionMode = DetectionMode.RadiusOnly;

    [Tooltip("원형 범위 감지 반경 (미터). RadiusOnly 또는 RadiusAndSight 일 때 사용됨")]
    public float detectionRadius = 8f;

    [Tooltip("시야 감지 범위 반경 (미터). SightOnly 또는 RadiusAndSight 일 때 사용됨")]
    public float sightRange = 15f;

    [Tooltip("시야각 (도). 예: 90이면 정면 기준 좌우 45도씩 볼 수 있음. SightOnly 또는 RadiusAndSight 일 때 사용됨")]
    [Range(10f, 360f)]
    public float sightAngle = 90f;

    [Tooltip("플레이어를 감지할 수 있는 레이어 (Player 레이어 포함)")]
    public LayerMask playerLayer;

    [Tooltip("시야를 막는 장애물 레이어 (Wall, Furniture 등)")]
    public LayerMask obstacleLayer;

    [Header("전투 설정")]
    [Tooltip("공격이 닿는 최대 거리 (미터)")]
    public float attackRange = 20f;

    [Tooltip("공격 쿨다운 시간 (초)")]
    public float attackCooldown = 2f;

    [Tooltip("조준 완료까지 걸리는 시간 (초)")]
    public float aimDuration = 1.5f;

    [Tooltip("공격력 (IDamageable.TakeDamage에 전달되는 값)")]
    public float attackDamage = 20f;

    [Header("돌진 설정")]
    [Tooltip("돌진 중 이동 속도 (초당 미터). 활동 구역 제한이 해제됨")]
    public float chargeSpeed = 8f;

    [Tooltip("돌진 지속 시간 (초). 이 시간이 지나면 돌진 종료")]
    public float chargeDuration = 3f;

    [Tooltip("돌진 활성화 시 감지 반경 (미터). 벽 관통 감지에 사용됨")]
    public float chargeDetectionRadius = 100f;

    [Header("행동 확률 설정 (합계가 반드시 1.0 이 될 필요는 없음. 우선순위 순서로 판단)")]
    [Tooltip("돌진 공격 발동 확률 (0~1). 예: 0.05 = 5%")]
    [Range(0f, 1f)]
    public float chargeAttackChance = 0.05f;

    [Tooltip("가구가 있을 때 엄폐를 선택할 확률 (0~1). 나머지 확률은 조준 공격")]
    [Range(0f, 1f)]
    public float coverChance = 0.5f;

    [Header("탄퍼짐 설정")]
    [Tooltip("탄퍼짐 각도 (도). 0이면 정확한 조준. 값이 클수록 탄이 넓게 퍼짐")]
    [Range(0f, 15f)]
    public float spreadAngle = 3f;

    [Header("점사 설정")]
    [Tooltip("2발 점사 확률 (0~1)")]
    [Range(0f, 1f)]
    public float burst2Chance = 0.4f;

    [Tooltip("3발 점사 확률 (0~1). burst2Chance + burst3Chance 가 1 미만이면 나머지는 4발")]
    [Range(0f, 1f)]
    public float burst3Chance = 0.35f;

    [Tooltip("점사 발사 간격 (초). 점사 중 각 탄 사이의 딜레이")]
    public float burstFireInterval = 0.1f;
}