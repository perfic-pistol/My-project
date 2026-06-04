using UnityEngine;

// 플레이어가 이 스크립트가 붙은 프리팹에 닿으면
// 점수를 1점 올리고 효과음을 재생한 뒤 오브젝트를 제거합니다
// 이 스크립트를 점수를 올리고 싶은 프리팹에 붙여주세요
public class CollectibleItem : MonoBehaviour
{
    [Header("플레이어 설정")]
    [Tooltip("플레이어 오브젝트의 태그를 입력하세요 (기본값: Player)")]
    // 플레이어를 구분하기 위한 태그입니다
    public string playerTag = "Player";

    [Header("효과음 설정")]
    [Tooltip("아이템 획득 시 재생할 효과음을 여기에 넣어주세요")]
    // 인스펙터에서 오디오 클립을 직접 연결합니다
    public AudioClip collectSound;

    [Tooltip("효과음 볼륨을 조절합니다 (0 = 무음, 1 = 최대)")]
    [Range(0f, 1f)]
    // 0 에서 1 사이의 슬라이더로 볼륨을 조절할 수 있습니다
    public float volume = 1f;

    // 다른 콜라이더와 충돌이 시작될 때 자동으로 호출됩니다
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            HandleCollection();
        }
    }

    // 콜라이더의 Is Trigger 가 켜져 있으면 이 함수가 호출됩니다
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            HandleCollection();
        }
    }

    // 실제로 점수를 올리고 효과음을 재생한 뒤 오브젝트를 제거하는 함수입니다
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

        // 효과음이 연결되어 있으면 재생합니다
        // PlayClipAtPoint 는 오브젝트가 삭제된 뒤에도 소리가 끝까지 재생됩니다
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position, volume);
        }

        // 이 프리팹 오브젝트를 씬에서 제거합니다
        Destroy(gameObject);
    }
}