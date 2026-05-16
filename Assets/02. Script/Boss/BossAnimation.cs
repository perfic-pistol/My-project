using UnityEngine;
using UnityEngine.AI;

// 보스 몬스터의 애니메이션을 담당하는 컴포넌트
// Animator 와 NavMeshAgent 가 같은 오브젝트에 있어야 함
//
// 애니메이터 파라미터 설정 방법:
//   Animator 창에서 아래 이름으로 파라미터를 추가하세요
//   - IsWalking (Bool) : 이동 중일 때 true
//   - Attack (Trigger) : 공격 시 한 번 발동
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BossBlackboard))]
public class BossAnimation : MonoBehaviour
{
    // 애니메이터 파라미터 이름 상수
    // 오타 방지를 위해 문자열 대신 상수로 관리
    private static readonly int PARAM_IS_WALKING = Animator.StringToHash("IsWalking");
    private static readonly int PARAM_ATTACK = Animator.StringToHash("Attack");

    private Animator animator;
    private NavMeshAgent agent;
    private BossBlackboard blackboard;

    // 이동 중으로 판단하는 최소 속도
    // 이 값 이상으로 움직이면 걷기 애니메이션 재생
    private const float MOVE_THRESHOLD = 0.1f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        blackboard = GetComponent<BossBlackboard>();
    }

    private void Start()
    {
        // 상태 변경 이벤트 구독
        // 공격 상태로 전환될 때 Attack 트리거 발동
        blackboard.OnStateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (blackboard != null)
            blackboard.OnStateChanged -= OnStateChanged;
    }

    private void Update()
    {
        // NavMeshAgent 실제 이동 속도로 걷기 애니메이션 전환
        // agent.velocity 는 실제로 움직이는 속도이므로
        // 목적지에 도달해서 멈출 때 자동으로 걷기가 꺼짐
        bool isMoving = agent.velocity.magnitude > MOVE_THRESHOLD;
        animator.SetBool(PARAM_IS_WALKING, isMoving);
    }

    // 상태가 바뀔 때 호출됨
    private void OnStateChanged(BossBlackboard.BossState newState)
    {
        if (newState == BossBlackboard.BossState.Attack)
        {
            // Attack 트리거는 BossAttack 에서 실제 공격이 실행될 때 발동
            // 여기서는 상태 전환 시 트리거를 초기화만 함
            animator.ResetTrigger(PARAM_ATTACK);
        }
    }

    // BossAttack 에서 공격이 실행될 때 호출하는 함수
    // 사용법: GetComponent<BossAnimation>().PlayAttackAnimation();
    public void PlayAttackAnimation()
    {
        animator.SetTrigger(PARAM_ATTACK);
    }
}