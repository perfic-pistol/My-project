using UnityEngine;

// 보스 몬스터의 사운드를 담당하는 컴포넌트
// 평상시에는 중얼거리는 소리를 랜덤 간격으로 재생
// 공격 시에는 공격 사운드를 재생
// BossBlackboard 가 같은 오브젝트에 있어야 함
[RequireComponent(typeof(BossBlackboard))]
public class BossSound : MonoBehaviour
{
    [Header("중얼거림 사운드")]
    [Tooltip("평상시 중얼거리는 소리 클립 목록. 여러 개 등록하면 랜덤으로 재생됩니다")]
    public AudioClip[] mumbleSounds;

    [Tooltip("중얼거림 사운드 재생 최소 간격 (초)")]
    public float mumbleIntervalMin = 3f;

    [Tooltip("중얼거림 사운드 재생 최대 간격 (초)")]
    public float mumbleIntervalMax = 7f;

    [Tooltip("중얼거림 사운드 볼륨")]
    [Range(0f, 1f)]
    public float mumbleVolume = 0.7f;

    [Header("공격 사운드")]
    [Tooltip("공격 시 재생되는 사운드 클립 목록. 여러 개 등록하면 랜덤으로 재생됩니다")]
    public AudioClip[] attackSounds;

    [Tooltip("공격 사운드 볼륨")]
    [Range(0f, 1f)]
    public float attackVolume = 1f;

    private BossBlackboard blackboard;

    // 중얼거림용 AudioSource - 중얼거림 전용으로 분리하여 공격 사운드와 겹쳐도 끊기지 않음
    private AudioSource mumbleSource;

    // 공격 사운드용 AudioSource
    private AudioSource attackSource;

    // 다음 중얼거림까지 남은 시간
    private float mumbleTimer = 0f;

    private void Awake()
    {
        blackboard = GetComponent<BossBlackboard>();

        // AudioSource 두 개 생성 (중얼거림 / 공격 전용)
        mumbleSource = gameObject.AddComponent<AudioSource>();
        mumbleSource.spatialBlend = 1f; // 3D 사운드
        mumbleSource.playOnAwake = false;
        mumbleSource.volume = mumbleVolume;

        attackSource = gameObject.AddComponent<AudioSource>();
        attackSource.spatialBlend = 1f;
        attackSource.playOnAwake = false;
        attackSource.volume = attackVolume;
    }

    private void Start()
    {
        // 시작 시 첫 중얼거림 타이머 설정
        ResetMumbleTimer();
    }

    private void Update()
    {
        // 공격 상태일 때는 중얼거림 재생 안 함
        if (blackboard.CurrentState == BossBlackboard.BossState.Attack) return;

        // 중얼거림 타이머 감소
        mumbleTimer -= Time.deltaTime;
        if (mumbleTimer <= 0f)
        {
            PlayMumble();
            ResetMumbleTimer();
        }
    }

    // 중얼거림 사운드 재생
    private void PlayMumble()
    {
        if (mumbleSounds == null || mumbleSounds.Length == 0) return;

        // 이미 중얼거리는 중이면 재생 안 함
        if (mumbleSource.isPlaying) return;

        AudioClip clip = mumbleSounds[Random.Range(0, mumbleSounds.Length)];
        if (clip == null) return;

        mumbleSource.clip = clip;
        mumbleSource.volume = mumbleVolume;
        mumbleSource.Play();
    }

    // 다음 중얼거림 타이머를 랜덤 간격으로 리셋
    private void ResetMumbleTimer()
    {
        mumbleTimer = Random.Range(mumbleIntervalMin, mumbleIntervalMax);
    }

    // 공격 사운드 재생
    // BossAttack 에서 공격이 실행될 때 호출하는 함수
    // 사용법: GetComponent<BossSound>().PlayAttackSound();
    public void PlayAttackSound()
    {
        if (attackSounds == null || attackSounds.Length == 0) return;

        AudioClip clip = attackSounds[Random.Range(0, attackSounds.Length)];
        if (clip == null) return;

        // 공격 사운드는 PlayOneShot 으로 재생
        // 공격이 빠르게 연속으로 일어나도 소리가 끊기지 않음
        attackSource.PlayOneShot(clip, attackVolume);
    }
}