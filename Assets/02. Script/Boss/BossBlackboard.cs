using UnityEngine;

// 보스의 현재 상태와 위치 정보를 모든 컴포넌트가 공유하기 위한 클래스
// 이 스크립트를 보스 오브젝트에 붙이면 청각, 순찰, 공격 스크립트가 모두 이 데이터를 참조함
public class BossBlackboard : MonoBehaviour
{
    // 보스의 행동 상태 열거형
    public enum BossState
    {
        Patrol,      // 순찰 - NavMesh 위를 랜덤하게 돌아다님
        Investigate, // 조사 - 큰 소리가 들린 곳으로 이동
        Search,      // 탐색 - 조사 위치 주변 배회하며 작은 소리 포착 시도
        Attack       // 공격 - 플레이어 추적 및 공격
    }

    // ====================================================
    // 현재 행동 상태
    // ====================================================

    // 현재 상태를 변경할 때 다른 컴포넌트에 알릴 이벤트
    // 사용법: blackboard.OnStateChanged += 내함수;
    public System.Action<BossState> OnStateChanged;

    private BossState currentState = BossState.Patrol;

    // 현재 상태를 읽거나 변경하는 프로퍼티
    // 상태를 바꾸면 자동으로 OnStateChanged 이벤트가 발생함
    public BossState CurrentState
    {
        get => currentState;
        set
        {
            if (currentState == value) return; // 같은 상태로 바꾸면 무시
            currentState = value;
            OnStateChanged?.Invoke(currentState);
            Debug.Log("[BossBlackboard] 상태 변경: " + currentState);
        }
    }

    // ====================================================
    // 공유 위치 정보
    // ====================================================

    [Header("디버그 (읽기 전용 - 실시간 상태 확인용)")]

    [Tooltip("마지막으로 들은 큰 소리의 위치")]
    [SerializeField] private Vector3 lastLoudSoundPosition;

    [Tooltip("큰 소리 위치 정보가 있는지 여부")]
    [SerializeField] private bool hasLoudSoundPosition = false;

    [Tooltip("마지막으로 파악한 플레이어 위치")]
    [SerializeField] private Vector3 lastKnownPlayerPosition;

    [Tooltip("현재 추적 중인 플레이어의 트랜스폼")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("현재 행동 상태 이름 (읽기 전용)")]
    [SerializeField] private string debugStateName;

    // 프로퍼티로 외부에서 안전하게 접근
    public Vector3 LastLoudSoundPosition
    {
        get => lastLoudSoundPosition;
        set { lastLoudSoundPosition = value; hasLoudSoundPosition = true; }
    }

    public bool HasLoudSoundPosition => hasLoudSoundPosition;

    public Vector3 LastKnownPlayerPosition
    {
        get => lastKnownPlayerPosition;
        set => lastKnownPlayerPosition = value;
    }

    public Transform PlayerTransform
    {
        get => playerTransform;
        set => playerTransform = value;
    }

    // ====================================================
    // 타이머 (여러 컴포넌트가 공유)
    // ====================================================

    [Tooltip("탐색 타임아웃 타이머 (초 단위로 감소)")]
    [SerializeField] private float searchTimer = 0f;

    [Tooltip("공격 중 플레이어를 놓쳤을 때의 타이머 (초 단위로 감소)")]
    [SerializeField] private float attackLostTimer = 0f;

    public float SearchTimer
    {
        get => searchTimer;
        set => searchTimer = value;
    }

    public float AttackLostTimer
    {
        get => attackLostTimer;
        set => attackLostTimer = value;
    }

    // ====================================================
    // 초기화
    // ====================================================

    private void Update()
    {
        // 인스펙터에서 현재 상태 이름을 실시간으로 확인하기 위한 갱신
        debugStateName = currentState.ToString();
    }

    // 큰 소리 위치 정보를 초기화할 때 사용
    public void ClearLoudSoundPosition()
    {
        hasLoudSoundPosition = false;
        lastLoudSoundPosition = Vector3.zero;
    }
}