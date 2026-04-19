using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    public class PlayerMultipliers : MonoBehaviour, IPlayerMultipliers
    {
        [ReadOnly] public float damageMultiplier = 1;
        [ReadOnly] public float healMultiplier = 1;
        [ReadOnly] public float playerWeightMultiplier = 1;

        public float DamageMultiplier { get { return damageMultiplier; }  set { damageMultiplier = value; } }
        public float HealMultiplier { get { return healMultiplier; } set { healMultiplier = value; } }
        public float WeightMultiplier { get { return playerWeightMultiplier; } set { playerWeightMultiplier = value; } }

        private void Awake()
        {
            damageMultiplier = 1;
            healMultiplier = 1;
            playerWeightMultiplier = 1;
        }
    }
}

#if UNITY_EDITOR
namespace cowsins
{
    [CustomEditor(typeof(PlayerMultipliers))]
    public class PlayerMultipliersEditor : Editor
    {
        private bool showDebugInfo;
        override public void OnInspectorGUI()
        {
            serializedObject.Update();

            if (showDebugInfo)
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.HelpBox("The game is not running. Some values may not be applied until play mode.", MessageType.Warning);
                }
                DrawDefaultInspector();
            }
            if (GUILayout.Button(showDebugInfo ? "Hide Debug Information" : "Show Debug Information")) showDebugInfo = !showDebugInfo;
        }
    }
}
#endif
