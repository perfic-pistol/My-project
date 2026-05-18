using UnityEngine;

namespace AssistSoftware.EasternEuropeanSoldier.Demo
{
    public class EasternEuropeanSoldierVisualController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [Header("Modern Soldier Equipment")]
        [SerializeField] private GameObject _ammoPads;
        [SerializeField] private SkinnedMeshRenderer[] _ammoPadsLod;
        [SerializeField] private GameObject _faceMask;
        [SerializeField] private SkinnedMeshRenderer[] _faceMaskLod;
        [SerializeField] private GameObject _gloves;
        [SerializeField] private SkinnedMeshRenderer[] _glovesLod;
        [SerializeField] private GameObject _helmet;
        [SerializeField] private SkinnedMeshRenderer[] _helmetLod;
        [SerializeField] private GameObject _helmetVisor;
        [SerializeField] private GameObject _jaket;
        [SerializeField] private SkinnedMeshRenderer[] _jaketLod;
        [SerializeField] private GameObject _kneePad;
        [SerializeField] private SkinnedMeshRenderer[] _kneePadLod;
        [SerializeField] private GameObject _legPouch;
        [SerializeField] private SkinnedMeshRenderer[] _legPouchLod;
        [SerializeField] private GameObject _pantsBoots;
        [SerializeField] private SkinnedMeshRenderer[] _pantsBootsLod;
        [SerializeField] private GameObject _pistolHolder;
        [SerializeField] private SkinnedMeshRenderer[] _pistolHolderLod;
        [SerializeField] private GameObject _vest;
        [SerializeField] private SkinnedMeshRenderer[] _vestLod;

        [Header("Weapons")]
        [SerializeField] private MeshRenderer[] _riffle;
        [SerializeField] private MeshRenderer[] _pistol;


        [Header("Camo")]
        [SerializeField] private Material[] _strapsAndGearMat;
        [SerializeField] private Material[] _vestAndHelmetMat;
        [SerializeField] private Material[] _jaketAndPantsMat;
        [SerializeField] private Material[] _riffleMat;
        [SerializeField] private Material[] _pistonMat;

        public void PlayAnim(string animName)
        {
            _animator?.Play(animName, 0);
        }

        public void SetCamo(int index) 
        {
            for (int i = 0; i < 3; i++)
            {
                _ammoPadsLod[i].material = _strapsAndGearMat[index];
                _faceMaskLod[i].material = _vestAndHelmetMat[index];
                _glovesLod[i].material = _vestAndHelmetMat[index];
                _helmetLod[i].material = _vestAndHelmetMat[index];
                _jaketLod[i].material = _jaketAndPantsMat[index];
                _kneePadLod[i].material = _strapsAndGearMat[index];
                _legPouchLod[i].material = _strapsAndGearMat[index];
                _pantsBootsLod[i].material = _jaketAndPantsMat[index];
                _pistolHolderLod[i].material = _strapsAndGearMat[index];
                _vestLod[i].material = _vestAndHelmetMat[index];
            }

        }

        public void SetWeaponsCamo(int index)
        {
            for (int i = 0; _riffle.Length > i; i++)
            {
                _riffle[i].material = _riffleMat[index];
            }
            for (int i = 0; _pistol.Length > i; i++)
            {
                _pistol[i].material = _pistonMat[index];
            }
        }

        public void ToggleAmmoPads(bool newState)
        {
            _ammoPads.gameObject.SetActive(newState);
        }
        public void ToggleFaceMask(bool newState)
        {
            _faceMask.gameObject.SetActive(newState);
        }
        public void ToggleGloves(bool newState)
        {
            _gloves.gameObject.SetActive(newState);
        }
        public void ToggleHelmet(bool newState)
        {
            _helmet.gameObject.SetActive(newState);
        }
        public void ToggleHelmetVisor(bool newState)
        {
            _helmetVisor.gameObject.SetActive(newState);
        }
        public void ToggleJaket(bool newState)
        {
            _jaket.gameObject.SetActive(newState);
        }
        public void ToggleKneePad(bool newState)
        {
            _kneePad.gameObject.SetActive(newState);
        }
        public void ToggleLegPouch(bool newState)
        {
            _legPouch.gameObject.SetActive(newState);
        }
        public void TogglePantsBoots(bool newState)
        {
            _pantsBoots.gameObject.SetActive(newState);
        }
        public void TogglePistolHolder(bool newState)
        {
            _pistolHolder.gameObject.SetActive(newState);
        }
        public void ToggleVest(bool newState)
        {
            _vest.gameObject.SetActive(newState);
        }
    }
}