using UnityEngine;
using UnityEngine.AI; // NavMeshAgent 사용을 위한 네임스페이스

// 몬스터의 활동 구역 중심을 씬에서 관리하는 헬퍼 스크립트
// 사용 방법:
//   1. 빈 게임 오브젝트를 만들고 이 스크립트를 붙임
//   2. 해당 오브젝트를 몬스터가 돌아다닐 구역 중앙에 배치
//   3. MonsterPatrol의 zoneCenter 필드에 이 오브젝트를 연결
//
// 이렇게 하면 구역 중심을 씬에서 직접 드래그해서 배치할 수 있어서 편리함
public class MonsterZone : MonoBehaviour
{
    [Header("구역 시각화 설정 (에디터 전용)")]
    [Tooltip("씬 뷰에서 구역 원의 색상")]
    public Color zoneColor = new Color(0f, 0.8f, 1f, 0.15f);

    [Tooltip("씬 뷰에서 구역 테두리 색상")]
    public Color zoneBorderColor = new Color(0f, 0.8f, 1f, 0.8f);

    [Tooltip("씬 뷰에 구역 반경 표시. MonsterData의 zoneRadius와 맞춰주세요")]
    public float previewRadius = 10f;

    [Header("이 구역을 사용하는 몬스터 목록 (선택 사항, 에디터 확인용)")]
    [Tooltip("어떤 몬스터가 이 구역을 쓰는지 에디터에서 확인하기 위한 참조 목록")]
    public MonsterPatrol[] assignedMonsters;

    [Header("시작 위치 설정")]
    [Tooltip("게임 시작 시 assignedMonsters 목록의 몬스터들을 구역 정중앙으로 이동시킴")]
    public bool snapToZoneCenterOnStart = true;

    [Tooltip("구역 중심 위치의 y값(높이)을 자동으로 NavMesh 위에 맞춤. 꺼두면 MonsterZone 오브젝트의 y값 그대로 사용")]
    public bool autoSnapToNavMesh = true;

    // =====================================================================
    // 유니티 생명주기
    // =====================================================================

    private void Awake()
    {
        // snapToZoneCenterOnStart 가 꺼져 있으면 아무것도 하지 않음
        if (!snapToZoneCenterOnStart) return;

        // assignedMonsters 목록이 비어있으면 경고 후 종료
        if (assignedMonsters == null || assignedMonsters.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] assignedMonsters 목록이 비어있습니다. 인스펙터에서 몬스터를 연결해주세요.");
            return;
        }

        // 구역 중심의 실제 y값(높이) 결정
        // autoSnapToNavMesh 가 켜져 있으면 NavMesh 위의 가장 가까운 지점으로 높이 보정
        Vector3 spawnPosition = GetNavMeshCenter();

        // 목록에 있는 모든 몬스터를 구역 중앙으로 이동
        foreach (MonsterPatrol monster in assignedMonsters)
        {
            if (monster == null)
            {
                Debug.LogWarning($"[{gameObject.name}] assignedMonsters 목록에 비어있는 슬롯이 있습니다.");
                continue;
            }

            // NavMeshAgent 가 활성화된 상태에서 transform.position 을 직접 바꾸면
            // NavMesh 위치와 어긋나서 오류가 발생할 수 있음
            // Warp() 를 써야 NavMeshAgent 내부 위치도 함께 갱신됨
            NavMeshAgent agent = monster.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.Warp(spawnPosition);
            }
            else
            {
                // NavMeshAgent 가 없는 경우 일반 위치 이동
                monster.transform.position = spawnPosition;
            }

            Debug.Log($"[{gameObject.name}] {monster.gameObject.name} 을(를) 구역 중앙 {spawnPosition} 으로 이동시켰습니다.");
        }
    }

    // 구역 중심의 NavMesh 위 실제 위치를 계산해서 반환
    private Vector3 GetNavMeshCenter()
    {
        Vector3 center = transform.position;

        if (!autoSnapToNavMesh) return center;

        // NavMesh.SamplePosition: 해당 위치에서 가장 가까운 NavMesh 위의 점을 찾음
        // 탐색 반경은 5f (너무 좁으면 못 찾을 수 있으니 여유있게 설정)
        NavMeshHit hit;
        if (NavMesh.SamplePosition(center, out hit, 5f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // NavMesh 위의 점을 못 찾으면 원래 위치 그대로 사용
        Debug.LogWarning($"[{gameObject.name}] 구역 중심 근처에서 NavMesh 를 찾지 못했습니다. NavMesh Bake 가 되어 있는지 확인해주세요.");
        return center;
    }

    // =====================================================================
    // 에디터 기즈모: 씬 뷰에서 구역 위치와 크기를 시각적으로 확인
    // =====================================================================

    private void OnDrawGizmos()
    {
        // 구역 채우기 (반투명)
        Gizmos.color = zoneColor;
        Gizmos.DrawSphere(transform.position, previewRadius);

        // 구역 테두리
        Gizmos.color = zoneBorderColor;
        DrawCircleGizmo(transform.position, previewRadius);

        // 구역 중심 십자 표시
        Gizmos.color = zoneBorderColor;
        float crossSize = 0.5f;
        Gizmos.DrawLine(transform.position - Vector3.right * crossSize,
                        transform.position + Vector3.right * crossSize);
        Gizmos.DrawLine(transform.position - Vector3.forward * crossSize,
                        transform.position + Vector3.forward * crossSize);
    }

    private void DrawCircleGizmo(Vector3 center, float radius)
    {
        int segments = 32;
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0f, Mathf.Sin(angle1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0f, Mathf.Sin(angle2) * radius);

            Gizmos.DrawLine(p1, p2);
        }
    }

#if UNITY_EDITOR
    // 씬 뷰에서 구역 이름 표시 (에디터에서만 동작)
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = zoneBorderColor;
        string snapStatus = snapToZoneCenterOnStart ? "중앙 스냅 ON" : "중앙 스냅 OFF";
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.5f,
            $"[구역] {gameObject.name}\n반경: {previewRadius}m\n몬스터 수: {(assignedMonsters != null ? assignedMonsters.Length : 0)}\n{snapStatus}");
    }
#endif
}