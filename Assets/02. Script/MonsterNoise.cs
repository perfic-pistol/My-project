using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(AudioSource))]
public class MonsterNoise : MonoBehaviour
{
    [Header("Monster Settings")]
    public Transform monster;

    [Header("Distance Settings")]
    public float startDistance = 20f;
    public float maxNoiseDistance = 3f;

    [Header("Noise Style")]
    [Range(0f, 1f)]
    public float noiseType = 0f;
    [Range(10f, 300f)]
    public float grainSize = 150f;

    [Header("Monster Mask")]
    [Range(0f, 1f)]
    public float monsterClearSize = 0.25f;
    [Range(0.01f, 1f)]
    public float edgeSoftness = 0.2f;

    [Header("Collision Settings")]
    [Tooltip("몬스터와 이 거리 이하면 씬 전환")]
    public float collisionDistance = 1.5f;

    [Header("Sound Settings")]
    [Tooltip("노이즈 효과 중 재생될 사운드")]
    public AudioClip noiseSound;

    [Tooltip("죽을 때 숨길 UI 오브젝트들 (Canvas 등)")]
    public GameObject[] uiObjects;
    [Tooltip("플레이어 손 등 카메라 자식 오브젝트들")]
    public GameObject[] playerHandObjects;

    Camera cam;
    Material noiseMat;
    Shader noiseShader;
    AudioSource audioSource;

    // 레이캐스트 결과를 캐싱
    bool isBlocked = false;
    float checkInterval = 0.1f;  // 0.1초마다 체크
    float checkTimer = 0f;

    // 씬 전환 중복 실행 방지
    bool isChangingScene = false;

    // 까만 화면 전환용
    float blackScreenIntensity = 0f;
    bool isBlackScreen = false;

    void Start()
    {
        AudioListener.pause = false;

        cam = GetComponent<Camera>();
        InitMaterial();

        // AudioSource 설정
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = noiseSound;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    void InitMaterial()
    {
        noiseShader = Shader.Find("Custom/NoiseShader1");

        // 셰이더를 못 찾으면 중단
        if (noiseShader == null)
        {
            Debug.LogError("NoiseShader를 찾을 수 없습니다. 셰이더 파일 이름을 확인하세요.");
            return;
        }

        // 셰이더가 정상인지 확인
        if (!noiseShader.isSupported)
        {
            Debug.LogError("NoiseShader가 현재 플랫폼에서 지원되지 않습니다.");
            return;
        }

        noiseMat = new Material(noiseShader);
        noiseMat.hideFlags = HideFlags.HideAndDontSave;
    }

    void Update()
    {
        if (monster == null || isChangingScene) return;

        // 충돌 거리 체크
        float dist = Vector3.Distance(transform.position, monster.position);

        // 몬스터 접촉 시 씬 전환 시작
        if (dist <= collisionDistance)
        {
            StartCoroutine(BlackScreenAndChangeScene());
            return;
        }

        // 노이즈 범위 안에 있고 벽에 안 막혔으면 사운드 재생
        // 볼륨은 항상 1f로 고정
        if (!isBlocked)
        {
            float intensity = 1f - Mathf.InverseLerp(maxNoiseDistance, startDistance, dist);
            intensity = Mathf.Clamp01(intensity);

            if (intensity > 0.01f)
            {
                if (!audioSource.isPlaying && noiseSound != null)
                    audioSource.Play();

                audioSource.volume = 1f; // 볼륨 항상 최대 고정
            }
            else
            {
                if (audioSource.isPlaying)
                    audioSource.Stop();
            }
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
        }

        // 0.1초마다 벽 체크
        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        Vector3 direction = monster.position - transform.position;
        bool hasWall = Physics.Raycast(
            transform.position,
            direction.normalized,
            out RaycastHit hit,
            dist
        );
        isBlocked = hasWall && hit.transform != monster;
    }

    IEnumerator BlackScreenAndChangeScene()
    {
        isChangingScene = true;

        // 모든 사운드 정지
        AudioListener.pause = true;

        // UI 숨기기
        foreach (GameObject ui in uiObjects)
            if (ui != null) ui.SetActive(false);

        // 플레이어 손 숨기기
        foreach (GameObject hand in playerHandObjects)
            if (hand != null) hand.SetActive(false);

        // 까만 화면 노이즈 시작
        isBlackScreen = true;

        // 3초 대기
        yield return new WaitForSeconds(3f);

        // 씬 전환
        string currentScene = SceneManager.GetActiveScene().name;
        string nextScene = "";

        if (currentScene == "Map1")
            nextScene = "Map2";
        else if (currentScene == "Map2")
            nextScene = "Map3";
        else if (currentScene == "Map3")
            nextScene = "Opening";
        else
        {
            Debug.LogWarning("현재 씬이 Map1, Map2, Map3가 아닙니다: " + currentScene);
            isChangingScene = false;
            isBlackScreen = false;
            AudioListener.pause = false; // 실패 시 사운드 복구

            // 실패 시 다시 보이게 복구
            foreach (GameObject ui in uiObjects)
                if (ui != null) ui.SetActive(true);
            foreach (GameObject hand in playerHandObjects)
                if (hand != null) hand.SetActive(true);

            yield break;
        }

        SceneManager.LoadScene(nextScene);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // 머티리얼이 없거나 깨진 경우 그냥 통과
        if (noiseMat == null || !noiseMat.shader.isSupported)
        {
            Graphics.Blit(src, dest);
            return;
        }

        if (monster == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        float dist = Vector3.Distance(transform.position, monster.position);

        // 플레이어 → 몬스터 방향으로 레이캐스트
        Vector3 direction = monster.position - transform.position;
        bool hasWallBetween = Physics.Raycast(
            transform.position,
            direction.normalized,
            out RaycastHit hit,
            dist
        );

        // 까만 화면 상태면 검은 텍스처로 덮기
        if (isBlackScreen)
        {
            RenderTexture black = RenderTexture.GetTemporary(src.width, src.height);
            GL.Clear(true, true, Color.black);
            noiseMat.SetFloat("_Intensity", 1f);
            noiseMat.SetFloat("_NoiseType", noiseType);
            noiseMat.SetFloat("_GrainSize", grainSize);
            noiseMat.SetFloat("_MonsterU", -9999f);
            noiseMat.SetFloat("_MonsterV", -9999f);
            noiseMat.SetFloat("_ClearSize", 0f);
            noiseMat.SetFloat("_EdgeSoftness", 0.01f);
            Graphics.Blit(black, dest, noiseMat);
            RenderTexture.ReleaseTemporary(black);
            return;
        }

        // 벽에 막혔는지 확인 (몬스터 본인에게 맞은 건 제외)
        bool blocked = hasWallBetween && hit.transform != monster;

        // 막혀있으면 강도 0으로
        float intensity = 0f;
        if (!blocked)
        {
            intensity = 1f - Mathf.InverseLerp(maxNoiseDistance, startDistance, dist);
            intensity = Mathf.Clamp01(intensity);
        }

        Vector3 screenPos = cam.WorldToViewportPoint(monster.position);
        if (screenPos.z < 0)
            screenPos = new Vector3(-9999, -9999, 0);

        noiseMat.SetFloat("_Intensity", intensity);
        noiseMat.SetFloat("_NoiseType", noiseType);
        noiseMat.SetFloat("_GrainSize", grainSize);
        noiseMat.SetFloat("_MonsterU", screenPos.x);
        noiseMat.SetFloat("_MonsterV", screenPos.y);
        noiseMat.SetFloat("_ClearSize", monsterClearSize);
        noiseMat.SetFloat("_EdgeSoftness", edgeSoftness);

        Graphics.Blit(src, dest, noiseMat);
    }

    // 오브젝트 파괴될 때 머티리얼 메모리 정리
    void OnDestroy()
    {
        if (noiseMat != null)
            DestroyImmediate(noiseMat);
    }
}