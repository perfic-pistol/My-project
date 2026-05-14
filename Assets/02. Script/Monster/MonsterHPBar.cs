using UnityEngine;

public class MonsterHPBar : MonoBehaviour
{
    private Transform _mainCameraTransform;

    void Start()
    {
        if (Camera.main != null)
            _mainCameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (_mainCameraTransform == null) return;

        // 1. 카메라를 똑바로 바라보게 합니다.
        transform.LookAt(_mainCameraTransform);

        // 2. 중요: UI의 앞면(Z축)이 카메라를 향하게 하려면 180도 회전이 필요할 수 있습니다.
        // Canvas의 Forward가 반대라면 아래 코드를 활성화하세요.
        // transform.Rotate(0, 180, 0);
    }
}
