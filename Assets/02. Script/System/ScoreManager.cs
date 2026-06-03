using UnityEngine;
using UnityEngine.Events;
using cowsins;

// 점수를 관리하는 스크립트입니다
// 씬 전체에서 하나만 존재하는 싱글톤 방식으로 동작합니다
public class ScoreManager : MonoBehaviour
{
    // 다른 스크립트에서 ScoreManager.Instance 로 접근할 수 있게 해주는 변수입니다
    public static ScoreManager Instance { get; private set; }

    [Header("점수 설정")]
    [Tooltip("점수의 최대값을 설정하세요")]
    // 인스펙터에서 최대 점수를 직접 설정할 수 있습니다
    public int maxScore = 10;

    [Header("문 잠금해제 설정")]
    [Tooltip("최대 점수 달성 시 잠금해제할 문들을 여기에 연결하세요")]
    // 인스펙터에서 잠금해제할 문 오브젝트들을 연결합니다. 여러 개 등록 가능합니다
    public DoorInteractable[] doorsToUnlock;

    // 현재 점수를 저장하는 변수입니다 (외부에서 읽기만 가능)
    public int CurrentScore { get; private set; }

    // 점수가 바뀔 때 자동으로 호출되는 이벤트입니다 (UI 업데이트에 사용됩니다)
    public UnityAction<int> OnScoreChanged;

    // 점수가 최대값에 도달했을 때 호출되는 이벤트입니다
    public UnityAction OnMaxScoreReached;

    // 게임 오브젝트가 처음 생성될 때 딱 한 번 실행됩니다
    void Awake()
    {
        // 이미 ScoreManager 가 존재하면 중복 생성을 막습니다
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 이 오브젝트를 ScoreManager 의 유일한 인스턴스로 지정합니다
        Instance = this;
    }

    // 게임이 시작될 때 실행됩니다
    void Start()
    {
        // 점수를 항상 0 으로 초기화합니다
        CurrentScore = 0;

        // 시작 시점에도 UI 가 0 을 표시하도록 이벤트를 한 번 실행합니다
        OnScoreChanged?.Invoke(CurrentScore);
    }

    // 점수를 1점 올리는 함수입니다
    // CollectibleItem 스크립트에서 충돌 시 이 함수를 호출합니다
    public void AddScore()
    {
        // 이미 최대 점수에 도달했다면 더 이상 올리지 않습니다
        if (CurrentScore >= maxScore)
        {
            return;
        }

        // 점수를 1 증가시킵니다
        CurrentScore++;

        // 점수가 바뀌었으므로 UI 업데이트 이벤트를 실행합니다
        OnScoreChanged?.Invoke(CurrentScore);

        // 점수가 최대값에 도달했는지 확인합니다
        if (CurrentScore >= maxScore)
        {
            // 최대 점수 도달 이벤트를 실행합니다
            OnMaxScoreReached?.Invoke();

            // 연결된 문들을 모두 잠금해제합니다
            UnlockDoors();
        }
    }

    // 인스펙터에 등록된 모든 문의 잠금을 해제하는 함수입니다
    void UnlockDoors()
    {
        // 등록된 문이 하나도 없으면 실행하지 않습니다
        if (doorsToUnlock == null || doorsToUnlock.Length == 0)
        {
            return;
        }

        // 등록된 문을 하나씩 순서대로 잠금해제합니다
        for (int i = 0; i < doorsToUnlock.Length; i++)
        {
            // 혹시 빈 슬롯이 있더라도 오류 없이 건너뜁니다
            if (doorsToUnlock[i] == null) continue;

            doorsToUnlock[i].UnLock();
        }
    }
}