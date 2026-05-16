using UnityEngine;

// 보스의 청각 감지를 담당하는 컴포넌트
//
// 소리 분류:
//   clip 있음 → 클립 이름 키워드로 분류 (총소리, 앉기, 장전 등)
//   clip 없음 → PlayOneShot 방식 (발소리)
//              Player 오브젝트 + 볼륨 0.5 초과 → 달리기 (중간 소리)
//              Player 오브젝트 + 볼륨 0.5 이하 → 걷기   (작은 소리)
//
// BossBlackboard 가 같은 오브젝트에 있어야 함
[RequireComponent(typeof(BossBlackboard))]
public class BossHearing : MonoBehaviour
{
    [Tooltip("보스 데이터 스크립터블 오브젝트. 인스펙터에서 연결하세요")]
    public BossData MonsterData;

    private BossBlackboard blackboard;
    private Transform playerTransformCache = null;

    private float soundCheckTimer = 0f;
    private float audioCacheTimer = 0f;
    private const float AUDIO_CACHE_INTERVAL = 3f;
    private AudioSource[] cachedAudioSources = null;

    // 달리기와 걷기를 구분하는 볼륨 기준값
    // FootstepsBehaviour 에서 달리기는 footstepVolume 그대로,
    // 걷기는 footstepVolume * 0.4 로 재생하므로 0.5 를 기준으로 구분
    private const float RUNNING_VOLUME_THRESHOLD = 0.8f;

    private void Awake()
    {
        blackboard = GetComponent<BossBlackboard>();
    }

    private void Start()
    {
        if (MonsterData == null)
        {
            Debug.LogError("[BossHearing] BossData 가 연결되지 않았습니다.");
            enabled = false;
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransformCache = playerObj.transform;

        RefreshAudioSourceCache();
    }

    private void Update()
    {
        audioCacheTimer -= Time.deltaTime;
        if (audioCacheTimer <= 0f)
        {
            RefreshAudioSourceCache();
            audioCacheTimer = AUDIO_CACHE_INTERVAL;
        }

        soundCheckTimer -= Time.deltaTime;
        if (soundCheckTimer > 0f) return;
        soundCheckTimer = MonsterData.soundCheckInterval;

        CheckSurroundingSounds();
    }

    private void RefreshAudioSourceCache()
    {
        cachedAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
    }

    private bool IsSoundPlaying(AudioSource audio)
    {
        if (audio.isPlaying) return true;
        if (audio.clip != null && audio.time > 0f && audio.time < audio.clip.length)
            return true;
        return false;
    }

    private enum SoundLevel { None, Quiet, Medium, Loud }

    private void CheckSurroundingSounds()
    {
        if (cachedAudioSources == null) return;

        BossBlackboard.BossState state = blackboard.CurrentState;

        if (state == BossBlackboard.BossState.Attack && blackboard.PlayerTransform != null)
        {
            TryRefreshAttackTimer();
            return;
        }

        Vector3 loudPos = Vector3.zero; float loudDist = float.MaxValue; bool foundLoud = false;
        Vector3 mediumPos = Vector3.zero; float mediumDist = float.MaxValue; bool foundMedium = false;
        Vector3 quietPos = Vector3.zero; float quietDist = float.MaxValue; bool foundQuiet = false;

        foreach (AudioSource audio in cachedAudioSources)
        {
            if (audio == null) continue;
            if (audio.gameObject == gameObject) continue;
            if (!IsSoundPlaying(audio)) continue;

            float dist = Vector3.Distance(transform.position, audio.transform.position);

            SoundLevel level = ClassifySound(audio, dist);
            if (level == SoundLevel.None) continue;

            switch (level)
            {
                case SoundLevel.Loud:
                    if (dist < loudDist) { loudDist = dist; loudPos = audio.transform.position; foundLoud = true; }
                    break;
                case SoundLevel.Medium:
                    if (dist < mediumDist) { mediumDist = dist; mediumPos = audio.transform.position; foundMedium = true; }
                    break;
                case SoundLevel.Quiet:
                    if (dist < quietDist) { quietDist = dist; quietPos = audio.transform.position; foundQuiet = true; }
                    break;
            }
        }

        if (foundLoud) HandleLoudSound(loudPos);
        else if (foundMedium) HandleMediumSound(mediumPos, state);
        else if (foundQuiet) HandleQuietSound(quietPos, state);
    }

    // 소리 단계 분류 함수
    private SoundLevel ClassifySound(AudioSource audio, float dist)
    {
        // clip 이 있으면 클립 이름 키워드로 분류 (총소리, 앉기, 장전 등)
        if (audio.clip != null)
        {
            string clipName = audio.clip.name;
            if (MonsterData.IsLoudClip(clipName) && dist <= MonsterData.loudSoundDetectionRange) return SoundLevel.Loud;
            if (MonsterData.IsMediumClip(clipName) && dist <= MonsterData.mediumSoundDetectionRange) return SoundLevel.Medium;
            if (MonsterData.IsQuietClip(clipName) && dist <= MonsterData.quietSoundDetectionRange) return SoundLevel.Quiet;
            return SoundLevel.None;
        }

        // clip 이 없으면 PlayOneShot 방식 (발소리)
        // Player 오브젝트에서 나오는 소리를 볼륨으로 달리기/걷기 구분
        string objectName = audio.gameObject.name;
        if (MonsterData.IsQuietSoundObject(objectName))
        {
            float vol = audio.volume;
            if (vol > RUNNING_VOLUME_THRESHOLD && dist <= MonsterData.mediumSoundDetectionRange) return SoundLevel.Medium; // 달리기
            if (vol <= RUNNING_VOLUME_THRESHOLD && dist <= MonsterData.quietSoundDetectionRange) return SoundLevel.Quiet;  // 걷기
        }

        return SoundLevel.None;
    }

    // 공격 중 플레이어 소리가 들리면 추적 타이머 리셋
    private void TryRefreshAttackTimer()
    {
        if (cachedAudioSources == null) return;

        foreach (AudioSource audio in cachedAudioSources)
        {
            if (audio == null || !IsSoundPlaying(audio)) continue;

            float dist = Vector3.Distance(transform.position, audio.transform.position);
            if (dist > MonsterData.loudSoundDetectionRange) continue;

            // clip 있는 소리: 클립 이름 키워드 확인
            if (audio.clip != null)
            {
                string clipName = audio.clip.name;
                if (MonsterData.IsLoudClip(clipName) || MonsterData.IsMediumClip(clipName) || MonsterData.IsQuietClip(clipName))
                {
                    blackboard.AttackLostTimer = MonsterData.attackLostTimeout;
                    return;
                }
                continue;
            }

            // clip 없는 소리 (발소리): Player 오브젝트 확인
            if (MonsterData.IsQuietSoundObject(audio.gameObject.name))
            {
                blackboard.AttackLostTimer = MonsterData.attackLostTimeout;
                return;
            }
        }
    }

    private void HandleLoudSound(Vector3 soundPos)
    {
        blackboard.LastLoudSoundPosition = soundPos;
        blackboard.LastKnownPlayerPosition = soundPos;

        BossBlackboard.BossState state = blackboard.CurrentState;
        if (state == BossBlackboard.BossState.Patrol || state == BossBlackboard.BossState.Search)
        {
            Debug.Log("[BossHearing] 큰 소리(총소리) 감지! 조사 이동: " + soundPos);
            blackboard.CurrentState = BossBlackboard.BossState.Investigate;
        }
        else if (state == BossBlackboard.BossState.Attack)
        {
            blackboard.AttackLostTimer = MonsterData.attackLostTimeout;
        }
    }

    private void HandleMediumSound(Vector3 soundPos, BossBlackboard.BossState state)
    {
        blackboard.LastKnownPlayerPosition = soundPos;
        if (state == BossBlackboard.BossState.Patrol)
        {
            Debug.Log("[BossHearing] 중간 소리(달리기/장전) 감지! 탐색 전환: " + soundPos);
            blackboard.LastLoudSoundPosition = soundPos;
            blackboard.CurrentState = BossBlackboard.BossState.Search;
        }
        else if (state == BossBlackboard.BossState.Search)
        {
            blackboard.LastLoudSoundPosition = soundPos;
            blackboard.SearchTimer = MonsterData.searchTimeout;
        }
        else if (state == BossBlackboard.BossState.Attack)
        {
            blackboard.AttackLostTimer = MonsterData.attackLostTimeout;
        }
    }

    private void HandleQuietSound(Vector3 soundPos, BossBlackboard.BossState state)
    {
        blackboard.LastKnownPlayerPosition = soundPos;
        if (state == BossBlackboard.BossState.Search)
        {
            Debug.Log("[BossHearing] 작은 소리(걷기/앉기) 감지! 공격 전환: " + soundPos);
            TryAssignPlayerTransform(soundPos);
            blackboard.CurrentState = BossBlackboard.BossState.Attack;
        }
        else if (state == BossBlackboard.BossState.Patrol || state == BossBlackboard.BossState.Investigate)
        {
            Debug.Log("[BossHearing] 순찰 중 작은 소리(걷기/앉기) 감지! 탐색 전환: " + soundPos);
            blackboard.LastLoudSoundPosition = soundPos;
            blackboard.CurrentState = BossBlackboard.BossState.Search;
        }
    }

    private void TryAssignPlayerTransform(Vector3 nearPosition)
    {
        if (playerTransformCache != null) { blackboard.PlayerTransform = playerTransformCache; return; }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        if (Vector3.Distance(playerObj.transform.position, nearPosition) < 10f)
        {
            playerTransformCache = playerObj.transform;
            blackboard.PlayerTransform = playerTransformCache;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (MonsterData == null) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.1f);
        Gizmos.DrawSphere(transform.position, MonsterData.loudSoundDetectionRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, MonsterData.loudSoundDetectionRange);

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, MonsterData.mediumSoundDetectionRange);
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, MonsterData.mediumSoundDetectionRange);

        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, MonsterData.quietSoundDetectionRange);
        Gizmos.color = new Color(1f, 1f, 0f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, MonsterData.quietSoundDetectionRange);
    }
}