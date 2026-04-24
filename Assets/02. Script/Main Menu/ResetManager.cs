using UnityEngine;

public class ResetManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AudioListener.pause = false;

        // 마우스 커서 복구
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
