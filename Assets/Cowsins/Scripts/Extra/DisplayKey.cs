#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;
using TMPro;
namespace cowsins
{
    public class DisplayKey : MonoBehaviour
    {
        public static PlayerActions inputActions;
        private TextMeshProUGUI txt;

        private string displayString, currentDeviceGroup;

        private void Awake()
        {
            if (inputActions == null)
            {
                inputActions = new PlayerActions();
                inputActions.Enable();
            }
            txt = GetComponent<TextMeshProUGUI>();

            if (DeviceDetection.Instance != null)
                Repaint(DeviceDetection.Instance.mode);
        }


        private void OnEnable()
        {
            if (DeviceDetection.Instance != null)
                DeviceDetection.Instance.OnInputModeChanged += Repaint;
        }

        private void OnDisable()
        {
            if (DeviceDetection.Instance != null)
                DeviceDetection.Instance.OnInputModeChanged -= Repaint;
        }

        public void Repaint(DeviceDetection.InputMode inputMode)
        {
            currentDeviceGroup = inputMode == DeviceDetection.InputMode.Keyboard ? "Keyboard" : "Gamepad";
            displayString = inputActions.GameControls.Interacting.GetBindingDisplayString(InputBinding.MaskByGroup(currentDeviceGroup));
            txt.SetText(displayString);
        }

    }
}