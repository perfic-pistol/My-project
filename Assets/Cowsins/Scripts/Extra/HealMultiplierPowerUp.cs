/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    public class HealMultiplierPowerUp : PowerUp
    {
        [Header("CUSTOM"), SerializeField]
        private float healMultiplierAddition;

        public override void Interact(PlayerDependencies player)
        {
            base.Interact(player);
            player.PlayerMultipliers.HealMultiplier += healMultiplierAddition;
            Destroy(this.gameObject);
        }
    }
}