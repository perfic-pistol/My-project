using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    public class MainMenuButtons : MonoBehaviour
    {
        [Header("Scene Settings")]
#if UNITY_EDITOR
        [SerializeField] private SceneAsset gameSceneAsset;
#endif
        [HideInInspector]
        [SerializeField] private string gameSceneName;

        
        public void StartGame()
        {
            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError("Game scene is not assigned in MainMenuButtons.");
                return;
            }
            SceneManager.LoadScene(gameSceneName);
        }

        
        public void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gameSceneAsset != null)
                gameSceneName = gameSceneAsset.name;
        }
#endif
    }
}