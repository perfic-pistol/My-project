using UnityEngine;

// 보스 몬스터의 모든 수치를 담는 스크립터블 오브젝트
// 사용법: 프로젝트 창에서 우클릭 > Create > Boss Monster > Boss Data
[CreateAssetMenu(fileName = "BossData", menuName = "Boss Monster/Boss Data")]
public class BossData : ScriptableObject
{
    [Header("이동 속도")]
    [Tooltip("순찰할 때의 속도")]
    public float patrolSpeed = 1.5f;

    [Tooltip("소리를 듣고 이동할 때의 속도")]
    public float investigateSpeed = 4f;

    [Tooltip("플레이어를 추적할 때의 속도")]
    public float chaseSpeed = 5f;

    [Header("NavMeshAgent 설정")]
    [Tooltip("방향 전환 속도 (도/초)")]
    public float angularSpeed = 120f;

    [Tooltip("가속도")]
    public float acceleration = 6f;

    [Tooltip("목적지 도착 판정 거리 (미터)")]
    public float stoppingDistance = 1.5f;

    [Tooltip("NavMesh 충돌 반경 (미터)")]
    public float agentRadius = 0.8f;

    [Tooltip("NavMesh 충돌 높이 (미터)")]
    public float agentHeight = 2.0f;

    [Header("순찰 설정")]
    [Tooltip("랜덤 순찰 목적지를 현재 위치 기준 얼마나 멀리서 고를지 (미터)")]
    public float patrolWanderRadius = 20f;

    [Tooltip("이 거리 이하로 목적지에 가까워지면 다음 목적지를 미리 선택 (미터)")]
    public float patrolRepickDistance = 3f;

    [Header("청각 감지 - 감지 거리")]
    [Tooltip("큰 소리를 들을 수 있는 최대 거리 (미터)")]
    public float loudSoundDetectionRange = 40f;

    [Tooltip("중간 소리를 들을 수 있는 최대 거리 (미터)")]
    public float mediumSoundDetectionRange = 20f;

    [Tooltip("작은 소리를 들을 수 있는 최대 거리 (미터)")]
    public float quietSoundDetectionRange = 8f;

    [Tooltip("소리 감지 체크 간격 (초). 너무 짧으면 성능 낭비")]
    public float soundCheckInterval = 0.2f;

    [Header("청각 감지 - 화이트리스트 (지정한 소리만 감지)")]
    [Tooltip("기본적으로 모든 소리를 무시합니다.\n아래 목록에 등록된 클립 또는 오브젝트 이름의 소리만 감지합니다.")]
    public AudioClip[] loudClips = new AudioClip[0];

    [Tooltip("중간 소리 클립 이름 키워드 (장전, 점프 등)")]
    public AudioClip[] mediumClips = new AudioClip[0];

    [Tooltip("작은 소리 클립 이름 키워드 (앉기 등)")]
    public AudioClip[] quietClips = new AudioClip[0];

    [Space]
    [Tooltip("클립 이름이 없는 소리(PlayOneShot)는 클립으로 구분이 불가능합니다.\n" +
             "이 경우 오브젝트 이름으로 감지 단계를 지정하세요.\n\n" +
             "큰 소리 오브젝트 이름 키워드 (총소리가 나는 오브젝트 이름)")]
    public string[] loudSoundObjectKeywords = new string[0];

    [Tooltip("중간 소리 오브젝트 이름 키워드 (달리기 발소리가 나는 오브젝트 이름)")]
    public string[] mediumSoundObjectKeywords = new string[0];

    [Tooltip("작은 소리 오브젝트 이름 키워드 (걷기 발소리가 나는 오브젝트 이름)\n" +
             "클립 없는 PlayOneShot 발소리는 볼륨으로 달리기/걷기를 구분합니다.\n" +
             "볼륨 0.8 초과 → 달리기(중간 소리) / 이하 → 걷기(작은 소리)")]
    public string[] quietSoundObjectKeywords = new string[0];

    [Header("탐색 설정")]
    [Tooltip("소리가 난 위치 주변 배회 반경 (미터)")]
    public float searchRadius = 12f;

    [Tooltip("단서 없이 이 시간이 지나면 순찰로 복귀 (초)")]
    public float searchTimeout = 25f;

    [Header("공격 설정")]
    [Tooltip("근접 공격 판정 거리 (미터)")]
    public float meleeRange = 2.5f;

    [Tooltip("근접 공격 1회당 데미지")]
    public float meleeDamage = 30f;

    [Tooltip("공격 쿨타임 (초)")]
    public float attackCooldown = 2f;

    [Tooltip("공격 애니메이션 시작 후 실제 데미지가 들어가기까지의 딜레이 (초).\n" +
             "공격 모션의 타격 시점에 맞게 조절하세요.")]
    public float attackHitDelay = 0.5f;

    [Tooltip("플레이어 소리를 이 시간 동안 못 들으면 공격 포기 (초)")]
    public float attackLostTimeout = 6f;

    // =====================================================================
    // 헬퍼 함수 - BossHearing 에서 호출
    // =====================================================================

    // 클립 참조로 직접 비교 (드래그로 넣은 클립과 일치 여부 확인)
    public bool IsLoudClip(AudioClip clip) => ContainsClip(clip, loudClips);
    public bool IsMediumClip(AudioClip clip) => ContainsClip(clip, mediumClips);
    public bool IsQuietClip(AudioClip clip) => ContainsClip(clip, quietClips);

    // 오브젝트 이름 키워드 비교 (대소문자 무시, 포함 여부)
    public bool IsLoudSoundObject(string objectName) => ContainsKeyword(objectName, loudSoundObjectKeywords);
    public bool IsMediumSoundObject(string objectName) => ContainsKeyword(objectName, mediumSoundObjectKeywords);
    public bool IsQuietSoundObject(string objectName) => ContainsKeyword(objectName, quietSoundObjectKeywords);

    // 클립 배열에 해당 클립이 포함되어 있는지 확인
    private bool ContainsClip(AudioClip clip, AudioClip[] clips)
    {
        if (clip == null || clips == null || clips.Length == 0) return false;
        foreach (AudioClip c in clips)
        {
            if (c == clip) return true;
        }
        return false;
    }

    // 오브젝트 이름에 키워드 배열 중 하나라도 포함되면 true 반환 (대소문자 무시)
    private bool ContainsKeyword(string target, string[] keywords)
    {
        if (keywords == null || keywords.Length == 0) return false;
        if (string.IsNullOrEmpty(target)) return false;

        string lower = target.ToLower();
        foreach (string keyword in keywords)
        {
            if (!string.IsNullOrEmpty(keyword) && lower.Contains(keyword.ToLower()))
                return true;
        }
        return false;
    }
}