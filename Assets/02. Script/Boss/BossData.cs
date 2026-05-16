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
    [Tooltip("큰 소리를 들을 수 있는 최대 거리. 총소리 등 (미터)")]
    public float loudSoundDetectionRange = 40f;

    [Tooltip("중간 소리를 들을 수 있는 최대 거리. 점프, 장전, 달리기 등 (미터)")]
    public float mediumSoundDetectionRange = 20f;

    [Tooltip("작은 소리를 들을 수 있는 최대 거리. 걷기 발소리 등 (미터)")]
    public float quietSoundDetectionRange = 8f;

    [Tooltip("소리 감지 체크 간격 (초). 너무 짧으면 성능 낭비")]
    public float soundCheckInterval = 0.2f;

    [Header("청각 감지 - 오브젝트 분류")]
    [Tooltip("큰 소리 오브젝트 이름 키워드.\n" +
             "이 오브젝트에서 나는 소리는 loudSoundDetectionRange 안에서 큰 소리로 감지됩니다.\n" +
             "총소리 오브젝트: GeneralManagers")]
    public string[] loudSoundObjectKeywords = new string[] { "GeneralManagers" };

    [Tooltip("중간 소리 오브젝트 이름 키워드.\n" +
             "이 오브젝트에서 나는 소리는 mediumSoundDetectionRange 안에서 중간 소리로 감지됩니다.\n" +
             "점프, 장전, 달리기 소리 오브젝트: Player, GeneralManagers 등\n" +
             "큰 소리 오브젝트도 여기 포함하면 거리에 따라 자동으로 구분됩니다.")]
    public string[] mediumSoundObjectKeywords = new string[] { "Player", "GeneralManagers" };

    [Tooltip("작은 소리 오브젝트 이름 키워드.\n" +
             "이 오브젝트에서 나는 소리는 quietSoundDetectionRange 안에서 작은 소리로 감지됩니다.\n" +
             "걷기 발소리 오브젝트: Player")]
    public string[] quietSoundObjectKeywords = new string[] { "Player" };

    [Header("청각 감지 - 클립 이름 키워드")]
    [Tooltip("큰 소리로 감지할 클립 이름 키워드 (총소리 등)\n" +
             "AudioSourceDebugger 로그의 클립 이름을 확인한 뒤 입력하세요\n" +
             "대소문자 구분 없이 포함 여부로 판단합니다")]
    public string[] loudClipKeywords = new string[] { "gunshot", "shoot", "fire" };

    [Tooltip("중간 소리로 감지할 클립 이름 키워드 (달리기, 장전 등)")]
    public string[] mediumClipKeywords = new string[] { "reload", "run", "sprint" };

    [Tooltip("작은 소리로 감지할 클립 이름 키워드 (걷기, 앉기 등)")]
    public string[] quietClipKeywords = new string[] { "walk", "footstep", "crouch" };

    [Header("청각 감지 - 무시할 클립 이름")]
    [Tooltip("재생 중이어도 보스가 반응하면 안 되는 오디오 클립 이름 키워드.\n" +
             "clip 이름이 있는 소리에만 적용됩니다. PlayOneShot 소리는 clip이 없어서 적용 안 됨.\n" +
             "예시: ui, menu, click 등 UI 관련 소리")]
    public string[] ignoredClipKeywords = new string[] { "ui", "menu", "click" };

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

    [Tooltip("플레이어 소리를 이 시간 동안 못 들으면 공격 포기 (초)")]
    public float attackLostTimeout = 6f;

    // =====================================================================
    // 헬퍼 함수 - BossHearing 에서 호출
    // =====================================================================

    public bool IsLoudSoundObject(string objectName) => ContainsKeyword(objectName, loudSoundObjectKeywords);
    public bool IsMediumSoundObject(string objectName) => ContainsKeyword(objectName, mediumSoundObjectKeywords);
    public bool IsQuietSoundObject(string objectName) => ContainsKeyword(objectName, quietSoundObjectKeywords);
    public bool IsIgnoredClip(string clipName) => ContainsKeyword(clipName, ignoredClipKeywords);

    // 클립 이름 기반 분류 함수
    // AudioSourceDebugger 로 클립 이름을 확인한 뒤 아래 배열에 키워드를 입력하세요
    public bool IsLoudClip(string clipName) => ContainsKeyword(clipName, loudClipKeywords);
    public bool IsMediumClip(string clipName) => ContainsKeyword(clipName, mediumClipKeywords);
    public bool IsQuietClip(string clipName) => ContainsKeyword(clipName, quietClipKeywords);

    // 이름에 키워드 배열 중 하나라도 포함되면 true 반환 (대소문자 무시)
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