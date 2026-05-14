/// <summary>
/// This script belongs to cowsins?as a part of the cowsins?FPS Engine. All rights reserved. 
/// </summary>
namespace cowsins
{
    /// <summary>
    /// Used for Player and enemies, which can be hit
    /// </summary>
    public interface IDamageable
    {
        void Damage(float damage, bool isHeadshot);

        // 몬스터 공격용으로 추가 - Damage 를 내부적으로 호출하도록 구현
        void TakeDamage(float attackDamage);
    }
}
