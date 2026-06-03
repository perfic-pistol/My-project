using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 타이머가 끝나면 보스를 스폰하는 스크립트
// 씬에 빈 게임 오브젝트를 만들고 이 스크립트를 붙이세요
public class BossSpawnTimer : MonoBehaviour
{
    [Header("보스 스폰 설정")]
    [Tooltip("스폰할 보스 프리팹을 여기에 연결하세요")]
    public GameObject bossPrefab;

    [Tooltip("보스가 스폰될 위치. 씬에 빈 오브젝트를 만들어 원하는 위치에 놓고 연결하세요")]
    public Transform spawnPoint;

    [Header("타이머 설정")]
    [Tooltip("타이머 시작 시간 (초). 기본값 180초 = 3분")]
    public float timerDuration = 180f;

    [Tooltip("타이머를 씬 시작과 동시에 자동으로 시작할지 여부")]
    public bool autoStart = true;

    [Header("UI 연결")]
    [Tooltip("타이머 텍스트를 표시할 TextMeshPro 컴포넌트. 인스펙터에서 연결하세요")]
    public TextMeshProUGUI timerText;

    [Tooltip("타이머 배경 이미지 (선택). 스폰 직전 색상이 바뀝니다")]
    public Image timerBackground;

    [Tooltip("타이머가 이 시간 이하로 남으면 경고 색상으로 변경 (초)")]
    public float warningTime = 30f;

    [Tooltip("평상시 타이머 텍스트 색상")]
    public Color normalColor = Color.white;

    [Tooltip("경고 시 타이머 텍스트 색상")]
    public Color warningColor = new Color(1f, 0.4f, 0.2f, 1f);

    [Header("스폰 연출")]
    [Tooltip("보스 스폰 시 재생할 사운드 (선택)")]
    public AudioClip spawnSound;

    [Tooltip("스폰 사운드 볼륨 (0~1)")]
    [Range(0f, 1f)]
    public float spawnSoundVolume = 1f;

    // 내부 상태
    private float remainingTime;
    private bool isRunning = false;
    private bool hasSpawned = false;
    private AudioSource audioSource;

    private void Awake()
    {
        // 스폰 사운드 재생을 위한 AudioSource 자동 추가
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        remainingTime = timerDuration;

        if (timerText != null)
            timerText.color = normalColor;

        if (autoStart)
            StartTimer();
    }

    private void Update()
    {
        if (!isRunning || hasSpawned) return;

        remainingTime -= Time.deltaTime;

        // 타이머 UI 갱신
        UpdateTimerUI();

        // 타이머 종료
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            isRunning = false;
            SpawnBoss();
        }
    }

    // 타이머 텍스트와 색상 갱신
    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        // MM:SS 형식으로 표시
        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        // 경고 시간 이하면 색상 변경
        bool isWarning = remainingTime <= warningTime;
        timerText.color = isWarning ? warningColor : normalColor;

        if (timerBackground != null)
            timerBackground.color = isWarning
                ? new Color(warningColor.r, warningColor.g, warningColor.b, 0.2f)
                : new Color(0f, 0f, 0f, 0.2f);
    }

    // 보스 스폰 실행
    private void SpawnBoss()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        // 타이머 텍스트 숨기기 또는 "BOSS!" 표시
        if (timerText != null)
        {
            timerText.text = "BOSS!";
            timerText.color = warningColor;
        }

        // 스폰 위치 결정: spawnPoint 가 없으면 이 오브젝트 위치에서 스폰
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        // 스폰 사운드 재생
        if (spawnSound != null && audioSource != null)
            audioSource.PlayOneShot(spawnSound, spawnSoundVolume);

        // 보스 프리팹 스폰
        if (bossPrefab != null)
        {
            Instantiate(bossPrefab, spawnPosition, spawnRotation);
            Debug.Log("[BossSpawnTimer] 보스 스폰 완료! 위치: " + spawnPosition);
        }
        else
        {
            Debug.LogError("[BossSpawnTimer] bossPrefab 이 연결되지 않았습니다. 인스펙터에서 보스 프리팹을 연결하세요.");
        }
    }

    // ====================================================
    // 외부에서 호출 가능한 제어 함수
    // ====================================================

    // 타이머 시작
    public void StartTimer()
    {
        if (hasSpawned) return;
        isRunning = true;
        Debug.Log("[BossSpawnTimer] 타이머 시작. 남은 시간: " + timerDuration + "초");
    }

    // 타이머 일시정지
    public void PauseTimer()
    {
        isRunning = false;
    }

    // 타이머 재개
    public void ResumeTimer()
    {
        if (!hasSpawned)
            isRunning = true;
    }

    // 타이머 초기화 (처음부터 다시 시작)
    public void ResetTimer()
    {
        remainingTime = timerDuration;
        hasSpawned = false;
        isRunning = false;

        if (timerText != null)
        {
            UpdateTimerUI();
            timerText.color = normalColor;
        }
    }

    // 즉시 보스 스폰 (테스트용)
    public void ForceSpawn()
    {
        remainingTime = 0f;
        isRunning = false;
        SpawnBoss();
    }

    // 현재 남은 시간 반환 (다른 스크립트에서 참조할 때 사용)
    public float GetRemainingTime() => remainingTime;

    // 타이머가 실행 중인지 여부
    public bool IsRunning() => isRunning;

    // 이미 스폰됐는지 여부
    public bool HasSpawned() => hasSpawned;

    // 에디터에서 스폰 위치 확인
    private void OnDrawGizmosSelected()
    {
        if (spawnPoint == null) return;

        // 스폰 위치 - 빨간색 구
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawSphere(spawnPoint.position, 0.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(spawnPoint.position, 1f);

        // 스폰 방향 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(spawnPoint.position, spawnPoint.forward * 2f);
    }
}