using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AssistSoftware.EasternEuropeanSoldier.Demo
{
    public class EasternEuropeanSoldierDemoUI : MonoBehaviour
    {
        [SerializeField] private Dropdown _animationDropdown;
        [SerializeField] private EasternEuropeanSoldierVisualController _soldierController;
        [SerializeField] private Toggle _ammoPadsToggle, _faceMaskToggle, _glovesToggle, _helmetToggle, _helmetVisorToggle, _jaketToggle, _kneePadToggle, _legPouchToggle, _pantsBootsToggle, _pistolHolderToggle, _vestToggle;
        [SerializeField] private Button _camoA, _camoB, _camoC, _camoD, _camoE;
        [SerializeField] private Button _weaponCamoA, _weaponCamoB, _weaponCamoC, _weaponCamoD, _weaponCamoE;

        private Dictionary<int, System.Action> selectionIndexToAction;

        private void Awake()
        {
            selectionIndexToAction = new Dictionary<int, System.Action>();
            selectionIndexToAction.Add(0, () => _soldierController.PlayAnim("Demo1"));
            selectionIndexToAction.Add(1, () => _soldierController.PlayAnim("Demo2"));
        }

        private void Start()
        {
            _animationDropdown.onValueChanged.AddListener(ctx => { selectionIndexToAction[ctx]?.Invoke(); });

            _ammoPadsToggle.onValueChanged.AddListener(AmmoPadsToggleAction);
            _faceMaskToggle.onValueChanged.AddListener(FaceMaskToggleAction);
            _glovesToggle.onValueChanged.AddListener(GlovesToggleAction);
            _helmetToggle.onValueChanged.AddListener(HelmetToggleAction);
            _helmetVisorToggle.onValueChanged.AddListener(HelmetVisorToggleAction);
            _jaketToggle.onValueChanged.AddListener(JaketToggleAction);
            _kneePadToggle.onValueChanged.AddListener(KneePadsToggleAction);
            _legPouchToggle.onValueChanged.AddListener(LegPouchToggleAction);
            _pantsBootsToggle.onValueChanged.AddListener(PantsBootsToggleAction);
            _pistolHolderToggle.onValueChanged.AddListener(PistolHolderToggleAction);
            _vestToggle.onValueChanged.AddListener(VestToggleAction);

            _camoA.onClick.AddListener(() => _soldierController.SetCamo(0));
            _camoB.onClick.AddListener(() => _soldierController.SetCamo(1));
            _camoC.onClick.AddListener(() => _soldierController.SetCamo(2));
            _camoD.onClick.AddListener(() => _soldierController.SetCamo(3));
            _camoE.onClick.AddListener(() => _soldierController.SetCamo(4));

            _weaponCamoA.onClick.AddListener(() => _soldierController.SetWeaponsCamo(0));
            _weaponCamoB.onClick.AddListener(() => _soldierController.SetWeaponsCamo(1));
            _weaponCamoC.onClick.AddListener(() => _soldierController.SetWeaponsCamo(2));
            _weaponCamoD.onClick.AddListener(() => _soldierController.SetWeaponsCamo(3));
            _weaponCamoE.onClick.AddListener(() => _soldierController.SetWeaponsCamo(4));

        }
        private void AmmoPadsToggleAction(bool value)
        {
            _soldierController.ToggleAmmoPads(value);
        }
        private void FaceMaskToggleAction(bool value)
        {
            _soldierController.ToggleFaceMask(value);
        }
        private void GlovesToggleAction(bool value)
        {
            _soldierController.ToggleGloves(value);
        }
        private void HelmetToggleAction(bool value)
        {
            _soldierController.ToggleHelmet(value);
        }
        private void HelmetVisorToggleAction(bool value)
        {
            _soldierController.ToggleHelmetVisor(value);
        }
        private void JaketToggleAction(bool value)
        {
            _soldierController.ToggleJaket(value);
        }
        private void KneePadsToggleAction(bool value)
        {
            _soldierController.ToggleKneePad(value);
        }
        private void LegPouchToggleAction(bool value)
        {
            _soldierController.ToggleLegPouch(value);
        }
        private void PantsBootsToggleAction(bool value)
        {
            _soldierController.TogglePantsBoots(value);
        }
        private void PistolHolderToggleAction(bool value)
        {
            _soldierController.TogglePistolHolder(value);
        }
        private void VestToggleAction(bool value)
        {
            _soldierController.ToggleVest(value);
        }
    }
}