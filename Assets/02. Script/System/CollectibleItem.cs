using UnityEngine;

// 플레이어가 이 스크립트가 붙은 프리팹에 닿으면
// 점수를 1점 올리고 이 오브젝트를 제거합니다
// 이 스크립트를 점수를 올리고 싶은 프리팹에 붙여주세요
public class CollectibleItem : MonoBehaviour
{
    [Header("플레이어 설정")]
    [Tooltip("플레이어 오브젝트의 태그를 입력하세요 (기본값: Player)")]
    // 플레이어를 구분하기 위한 태그입니다
    // 유니티 인스펙터에서 변경할 수 있습니다
    public string playerTag = "Player";

    // 다른 콜라이더와 충돌이 시작될 때 자동으로 호출됩니다
    // 이 방식은 Trigger 가 아닌 일반 Collider 충돌에 반응합니다
    void OnCollisionEnter(Collision collision)
    {
        // 충돌한 오브젝트가 플레이어인지 확인합니다
        if (collision.gameObject.CompareTag(playerTag))
        {
            HandleCollection();
        }
    }

    // Trigger 방식의 콜라이더를 사용할 경우에도 동작하도록 추가했습니다
    // 콜라이더의 Is Trigger 가 켜져 있으면 이 함수가 호출됩니다
    void OnTriggerEnter(Collider other)
    {
        // 충돌한 오브젝트가 플레이어인지 확인합니다
        if (other.CompareTag(playerTag))
        {
            HandleCollection();
        }
    }

    // 실제로 점수를 올리고 오브젝트를 제거하는 함수입니다
    void HandleCollection()
    {
        // ScoreManager 가 씬에 존재하는지 확인합니다
        if (ScoreManager.Instance == null)
        {
            Debug.LogWarning("ScoreManager 가 씬에 없습니다. ScoreManager 오브젝트를 추가해주세요.");
            return;
        }

        // 점수를 1점 올립니다
        ScoreManager.Instance.AddScore();

        // 이 프리팹 오브젝트를 씬에서 제거합니다
        Destroy(gameObject);
    }
}