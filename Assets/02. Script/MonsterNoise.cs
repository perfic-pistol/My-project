using UnityEngine;
using System.Collections;

// 카메라와 오디오소스 컴포넌트가 반드시 두 개 필요함
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(AudioSource))]
public class MonsterNoise : MonoBehaviour
{
    [Header("몬스터 설정")]
    // 노이즈 효과의 기준이 될 몬스터 오브젝트를 연결하세요
    public Transform monster;

    [Header("거리 설정")]
    // 이 거리 이내로 들어오면 노이즈 효과가 시작됨
    public float startDistance = 20f;
    // 이 거리 이내에서 노이즈 효과가 최대가 됨
    public float maxNoiseDistance = 3f;

    [Header("노이즈 스타일")]
    // 0에 가까울수록 스타일A, 1에 가까울수록 스타일B
    [Range(0f, 1f)]
    public float noiseType = 0f;
    // 노이즈 알갱이 크기. 숫자가 클수록 알갱이가 커짐
    [Range(10f, 300f)]
    public float grainSize = 150f;

    [Header("몬스터 주변 클리어 영역")]
    // 몬스터 주변에 노이즈가 없는 깨끗한 원의 크기
    [Range(0f, 1f)]
    public float monsterClearSize = 0.25f;
    // 클리어 영역 가장자리의 부드러움 정도
    [Range(0.01f, 1f)]
    public float edgeSoftness = 0.2f;

    [Header("사운드 설정")]
    // 처음 한 번만 재생될 사운드 파일을 연결하세요 (인트로 사운드)
    public AudioClip introSound;
    // 인트로 사운드가 끝난 후 반복 재생될 사운드 파일을 연결하세요 (루프 사운드)
    public AudioClip loopSound;

    [Header("볼륨 조절")]
    // 범위 안에 막 들어왔을 때 (startDistance 지점)의 볼륨. 0이면 무음, 1이면 최대
    [Range(0f, 1f)]
    public float minVolume = 0f;
    // 가장 가까이 붙었을 때 (maxNoiseDistance 지점)의 볼륨. 0이면 무음, 1이면 최대
    [Range(0f, 1f)]
    public float maxVolume = 1f;

    [Header("셰이더 설정")]
    // NoiseShader1 파일을 인스펙터에서 직접 드래그해서 연결하세요
    public Shader noiseShaderRef;

    // 카메라 컴포넌트 참조
    Camera cam;

    // 노이즈 화면 효과에 사용될 머티리얼
    Material noiseMat;

    // 인트로 사운드를 재생하는 오디오소스 (처음 한 번만 재생)
    AudioSource introAudioSource;

    // 루프 사운드를 재생하는 오디오소스 (반복 재생)
    AudioSource loopAudioSource;

    // 인트로 사운드가 이미 재생되었는지 여부 (중복 재생 방지)
    bool introPlayed = false;

    // 인트로가 끝난 후 루프로 넘어가는 코루틴이 실행 중인지 여부 (중복 실행 방지)
    bool isWaitingForIntroEnd = false;

    // 벽에 가려져 있는지 여부 캐싱 (매 프레임 레이캐스트 방지용)
    bool isBlocked = false;

    // 레이캐스트 체크 간격 (초 단위, 0.1초마다 한 번 체크)
    float checkInterval = 0.1f;

    // 체크 타이머 누적값
    float checkTimer = 0f;

    void Start()
    {
        // 오디오 리스너 정상 작동 보장
        AudioListener.pause = false;

        // 카메라 컴포넌트 가져오기
        cam = GetComponent<Camera>();

        // 노이즈 머티리얼 초기화
        InitMaterial();

        // 오브젝트에 붙어있는 첫 번째 오디오소스를 인트로용으로 사용
        introAudioSource = GetComponent<AudioSource>();
        introAudioSource.clip = introSound;
        introAudioSource.loop = false;         // 인트로는 반복 없이 한 번만 재생
        introAudioSource.playOnAwake = false;

        // 루프용 오디오소스는 새로 추가해서 사용
        loopAudioSource = gameObject.AddComponent<AudioSource>();
        loopAudioSource.clip = loopSound;
        loopAudioSource.loop = true;           // 루프 사운드는 반복 재생
        loopAudioSource.playOnAwake = false;
    }

    // 노이즈 셰이더 머티리얼 초기화 함수
    void InitMaterial()
    {
        // 인스펙터에서 연결된 셰이더를 우선 사용하고, 없으면 이름으로 찾음
        Shader noiseShader = noiseShaderRef != null
            ? noiseShaderRef
            : Shader.Find("Custom/NoiseShader1");

        // 셰이더를 찾지 못한 경우 오류 출력 후 중단
        if (noiseShader == null)
        {
            Debug.LogError("NoiseShader를 찾을 수 없습니다. 셰이더 파일 이름을 확인하세요.");
            return;
        }

        // 현재 플랫폼에서 셰이더를 지원하지 않는 경우 오류 출력 후 중단
        if (!noiseShader.isSupported)
        {
            Debug.LogError("NoiseShader가 현재 플랫폼에서 지원되지 않습니다.");
            return;
        }

        // 머티리얼 생성 및 씬에 저장되지 않도록 설정
        noiseMat = new Material(noiseShader);
        noiseMat.hideFlags = HideFlags.HideAndDontSave;
    }

    void Update()
    {
        // 몬스터가 없으면 아무것도 하지 않음
        if (monster == null) return;

        // 몬스터와의 현재 거리 계산
        float dist = Vector3.Distance(transform.position, monster.position);

        // 거리에 따라 노이즈 강도 계산 (0~1 사이 값)
        float intensity = 1f - Mathf.InverseLerp(maxNoiseDistance, startDistance, dist);
        intensity = Mathf.Clamp01(intensity);

        // 벽에 막히지 않았고 강도가 있을 때만 사운드 처리
        if (!isBlocked && intensity > 0.01f)
        {
            // 거리에 따라 볼륨 계산 (minVolume ~ maxVolume 사이에서 부드럽게 변함)
            float currentVolume = Mathf.Lerp(minVolume, maxVolume, intensity);

            // 현재 재생 중인 사운드의 볼륨을 실시간으로 업데이트
            introAudioSource.volume = currentVolume;
            loopAudioSource.volume = currentVolume;

            // 인트로 사운드가 아직 한 번도 재생되지 않았을 때만 재생 시작
            if (!introPlayed)
            {
                introPlayed = true;

                // 인트로 사운드 파일이 연결되어 있으면 재생
                if (introSound != null)
                {
                    introAudioSource.Play();

                    // 인트로가 끝나면 루프 사운드로 넘어가는 코루틴 시작
                    // 이미 실행 중이 아닐 때만 시작해서 중복 방지
                    if (!isWaitingForIntroEnd)
                    {
                        isWaitingForIntroEnd = true;
                        StartCoroutine(PlayLoopAfterIntro());
                    }
                }
                else
                {
                    // 인트로 사운드가 없으면 곧바로 루프 사운드만 재생
                    StartLoopSound();
                }
            }
        }
        else
        {
            // 벽에 막혔거나 범위 밖이면 모든 사운드 정지하고 상태 초기화
            StopAllSounds();
        }

        // 타이머 누적
        checkTimer += Time.deltaTime;

        // 아직 체크 간격이 안 됐으면 리턴
        if (checkTimer < checkInterval) return;

        // 타이머 초기화 후 레이캐스트로 벽 체크
        checkTimer = 0f;

        Vector3 direction = monster.position - transform.position;
        bool hasWall = Physics.Raycast(
            transform.position,
            direction.normalized,
            out RaycastHit hit,
            dist
        );

        // 레이캐스트에 뭔가 맞았고 그게 몬스터가 아니라면 벽으로 판단
        isBlocked = hasWall && hit.transform != monster;
    }

    // 인트로 사운드가 끝날 때까지 기다렸다가 루프 사운드를 재생하는 코루틴
    IEnumerator PlayLoopAfterIntro()
    {
        // 인트로 사운드의 길이만큼 기다림
        // introSound가 없으면 0초 대기 후 바로 진행 (안전 처리)
        float waitTime = (introSound != null) ? introSound.length : 0f;
        yield return new WaitForSeconds(waitTime);

        // 코루틴 실행 플래그 해제
        isWaitingForIntroEnd = false;

        // 인트로가 끝난 뒤 아직 범위 안에 있고 벽에 막히지 않은 경우에만 루프 재생
        if (introPlayed && !isBlocked)
        {
            StartLoopSound();
        }
    }

    // 루프 사운드를 시작하는 함수 (중복 재생 방지 포함)
    void StartLoopSound()
    {
        if (loopSound == null) return;

        // 이미 재생 중이 아닐 때만 시작
        if (!loopAudioSource.isPlaying)
        {
            loopAudioSource.Play();
        }
    }

    // 모든 사운드를 정지하고 재생 상태를 초기화하는 함수
    void StopAllSounds()
    {
        // 인트로 사운드 정지
        if (introAudioSource.isPlaying)
            introAudioSource.Stop();

        // 루프 사운드 정지
        if (loopAudioSource.isPlaying)
            loopAudioSource.Stop();

        // 재생 기록 초기화 (다시 범위에 들어오면 처음부터 재생되도록)
        introPlayed = false;
        isWaitingForIntroEnd = false;

        // 실행 중인 코루틴 전부 정지 (인트로 대기 코루틴 포함)
        StopAllCoroutines();
    }

    // 카메라 렌더링이 끝난 후 화면에 노이즈 효과를 덧씌우는 함수
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // 머티리얼이 없거나 셰이더가 지원 안 되면 효과 없이 그대로 출력
        if (noiseMat == null || !noiseMat.shader.isSupported)
        {
            Graphics.Blit(src, dest);
            return;
        }

        // 몬스터가 없으면 효과 없이 그대로 출력
        if (monster == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        // 몬스터와의 거리 계산
        float dist = Vector3.Distance(transform.position, monster.position);

        // 렌더링용 레이캐스트로 벽 체크
        Vector3 direction = monster.position - transform.position;
        bool hasWallBetween = Physics.Raycast(
            transform.position,
            direction.normalized,
            out RaycastHit hit,
            dist
        );

        // 벽에 막혀 있는지 판단 (몬스터 자체에 맞은 건 제외)
        bool blocked = hasWallBetween && hit.transform != monster;

        // 막혀 있으면 강도 0, 아니면 거리에 따라 계산
        float intensity = 0f;
        if (!blocked)
        {
            intensity = 1f - Mathf.InverseLerp(maxNoiseDistance, startDistance, dist);
            intensity = Mathf.Clamp01(intensity);
        }

        // 몬스터의 화면 좌표 계산 (0~1 범위의 뷰포트 좌표)
        Vector3 screenPos = cam.WorldToViewportPoint(monster.position);

        // 몬스터가 카메라 뒤에 있으면 화면 밖으로 보냄
        if (screenPos.z < 0)
            screenPos = new Vector3(-9999, -9999, 0);

        // 셰이더 파라미터 전달
        noiseMat.SetFloat("_Intensity", intensity);
        noiseMat.SetFloat("_NoiseType", noiseType);
        noiseMat.SetFloat("_GrainSize", grainSize);
        noiseMat.SetFloat("_MonsterU", screenPos.x);
        noiseMat.SetFloat("_MonsterV", screenPos.y);
        noiseMat.SetFloat("_ClearSize", monsterClearSize);
        noiseMat.SetFloat("_EdgeSoftness", edgeSoftness);

        // 노이즈 효과 적용해서 화면에 출력
        Graphics.Blit(src, dest, noiseMat);
    }

    // 이 오브젝트가 파괴될 때 머티리얼 메모리 정리
    void OnDestroy()
    {
        // 실행 중인 코루틴 정리
        StopAllCoroutines();

        if (noiseMat != null)
            DestroyImmediate(noiseMat);
    }
}