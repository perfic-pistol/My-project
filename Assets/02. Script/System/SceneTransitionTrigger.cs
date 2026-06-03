using UnityEngine;
using UnityEngine.SceneManagement;

// 플레이어가 이 오브젝트에 닿으면 다음 씬으로 전환하는 스크립트입니다
// Quad 오브젝트에 이 스크립트를 붙여주세요
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("플레이어 설정")]
    [Tooltip("플레이어 오브젝트의 태그를 입력하세요 (기본값: Player)")]
    // 플레이어를 구분하기 위한 태그입니다
    public string playerTag = "Player";

    [Header("씬 전환 설정")]
    [Tooltip("전환할 다음 씬의 이름을 정확히 입력하세요 (대소문자 구분)")]
    // 이동할 씬 이름을 인스펙터에서 직접 입력합니다
    // Map1 에 있는 Quad 라면 Map2, Map2 에 있는 Quad 라면 Map3 을 입력하세요
    public string nextSceneName;

    [Tooltip("씬 전환 중 중복 실행을 막습니다. 건드리지 않아도 됩니다")]
    // 씬 전환이 이미 진행 중일 때 다시 실행되는 것을 방지하는 변수입니다
    private bool isTransitioning = false;

    // 게임이 시작될 때 설정값을 검사합니다
    void Start()
    {
        // 씬 이름이 비어있으면 경고를 표시합니다
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("SceneTransitionTrigger: 인스펙터에서 Next Scene Name 을 입력해주세요.");
        }

        // 이 오브젝트에 콜라이더가 없으면 경고를 표시합니다
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("SceneTransitionTrigger: 이 오브젝트에 Collider 가 없습니다. Mesh Collider 등을 추가해주세요.");
        }
        else if (!col.isTrigger)
        {
            // Trigger 가 꺼져 있으면 자동으로 켜줍니다
            col.isTrigger = true;
            Debug.Log("SceneTransitionTrigger: Collider 의 Is Trigger 를 자동으로 활성화했습니다.");
        }
    }

    // 플레이어가 이 오브젝트의 Trigger 영역에 들어오면 실행됩니다
    void OnTriggerEnter(Collider other)
    {
        // 충돌한 오브젝트가 플레이어인지 확인합니다
        if (!other.CompareTag(playerTag)) return;

        // 이미 씬 전환이 진행 중이라면 중복 실행을 막습니다
        if (isTransitioning) return;

        // 씬 이름이 비어있으면 전환하지 않습니다
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("SceneTransitionTrigger: Next Scene Name 이 비어있어 씬 전환을 할 수 없습니다.");
            return;
        }

        isTransitioning = true;
        LoadNextScene();
    }

    // 실제로 씬을 불러오는 함수입니다
    void LoadNextScene()
    {
        // 빌드 세팅에 해당 씬이 등록되어 있는지 확인합니다
        // 씬이 없으면 오류 없이 경고만 출력합니다
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("SceneTransitionTrigger: \"" + nextSceneName + "\" 씬을 찾을 수 없습니다. " +
                           "File > Build Settings 에서 해당 씬이 추가되어 있는지 확인해주세요.");
            // 전환 실패 시 다시 시도할 수 있도록 플래그를 초기화합니다
            isTransitioning = false;
        }
    }
}