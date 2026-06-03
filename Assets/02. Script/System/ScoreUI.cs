using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 캔버스에 점수를 표시하는 스크립트입니다
// Canvas 안의 Text 오브젝트에 이 스크립트를 붙여주세요
public class ScoreUI : MonoBehaviour
{
    [Header("UI 텍스트 설정")]
    [Tooltip("점수를 표시할 TextMeshPro 텍스트를 연결해주세요")]
    // 인스펙터에서 점수를 보여줄 텍스트 오브젝트를 연결해야 합니다
    public TMP_Text scoreText;

    [Tooltip("점수 앞에 표시할 문구입니다 (예: 점수: )")]
    // 점수 텍스트 앞에 붙일 레이블입니다. 인스펙터에서 변경 가능합니다
    public string scoreLabel = "점수: ";

    // 게임이 시작될 때 실행됩니다
    void Start()
    {
        // scoreText 가 연결되어 있지 않으면 경고를 표시합니다
        if (scoreText == null)
        {
            Debug.LogError("ScoreUI: scoreText 가 연결되지 않았습니다. 인스펙터에서 TMP Text 를 연결해주세요.");
            return;
        }

        // ScoreManager 가 씬에 있는지 확인합니다
        if (ScoreManager.Instance == null)
        {
            Debug.LogError("ScoreUI: ScoreManager 가 씬에 없습니다. ScoreManager 오브젝트를 추가해주세요.");
            return;
        }

        // 점수가 바뀔 때마다 UpdateScoreText 함수가 자동으로 호출되도록 등록합니다
        ScoreManager.Instance.OnScoreChanged += UpdateScoreText;

        // 시작 시 현재 점수(0)를 바로 화면에 표시합니다
        UpdateScoreText(ScoreManager.Instance.CurrentScore);
    }

    // 점수가 바뀔 때마다 텍스트를 새로 업데이트하는 함수입니다
    // ScoreManager 에서 자동으로 호출됩니다
    void UpdateScoreText(int newScore)
    {
        if (scoreText == null) return;

        // 레이블과 점수를 합쳐서 텍스트에 표시합니다 (예: "점수: 3")
        scoreText.text = scoreLabel + newScore;
    }

    // 이 오브젝트가 씬에서 제거될 때 이벤트 등록을 해제합니다
    // 이렇게 해야 메모리 누수를 방지할 수 있습니다
    void OnDestroy()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged -= UpdateScoreText;
        }
    }
}