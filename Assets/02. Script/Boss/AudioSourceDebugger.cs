using UnityEngine;

// 주변 AudioSource들의 볼륨과 오브젝트 이름을 실시간으로 콘솔에 출력하는 진단 스크립트
// 사용법:
//   1. 보스 오브젝트에 임시로 붙임
//   2. 플레이 후 총 발사, 장전, 걷기 등을 실행하면서 콘솔 확인
//   3. 오브젝트 이름과 클립 이름을 확인한 뒤 BossData 설정에 반영
//   4. 확인 후 이 스크립트는 제거해도 됨
public class AudioSourceDebugger : MonoBehaviour
{
    [Tooltip("이 반경 안의 AudioSource를 탐지 (미터)")]
    public float scanRadius = 50f;

    [Tooltip("소리 출력 간격 (초). 너무 짧으면 콘솔이 가득 참")]
    public float logInterval = 0.5f;

    private float logTimer = 0f;

    // 씬 전체 AudioSource를 캐싱 (콜라이더 없는 오브젝트도 포함)
    private AudioSource[] cachedSources;
    private float cacheTimer = 0f;

    private void Start()
    {
        cachedSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        Debug.Log($"[AudioDebugger] 씬에서 찾은 AudioSource 총 개수: {cachedSources.Length}");
    }

    private void Update()
    {
        // 3초마다 캐시 갱신
        cacheTimer -= Time.deltaTime;
        if (cacheTimer <= 0f)
        {
            cachedSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            cacheTimer = 3f;
        }

        logTimer -= Time.deltaTime;
        if (logTimer > 0f) return;
        logTimer = logInterval;

        ScanAndLog();
    }

    private void ScanAndLog()
    {
        if (cachedSources == null) return;

        foreach (AudioSource audio in cachedSources)
        {
            if (audio == null) continue;
            if (!audio.isPlaying) continue;

            float dist = Vector3.Distance(transform.position, audio.transform.position);
            if (dist > scanRadius) continue;

            string clipName = audio.clip != null ? audio.clip.name : "clip 없음 (PlayOneShot)";

            Debug.Log($"[AudioDebugger] 오브젝트: {audio.gameObject.name} | " +
                      $"클립: {clipName} | " +
                      $"볼륨: {audio.volume:F3} | " +
                      $"거리: {dist:F1}m");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, scanRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}