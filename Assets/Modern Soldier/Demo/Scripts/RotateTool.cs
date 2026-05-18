using UnityEngine;

namespace AssistSoftware.EasternEuropeanSoldier.Demo
{
    public class RotateTool : MonoBehaviour
    {
        [SerializeField] private float speed;
        void Update()
        {
            transform.Rotate(Vector3.up * Time.deltaTime * speed);
        }
    }
}