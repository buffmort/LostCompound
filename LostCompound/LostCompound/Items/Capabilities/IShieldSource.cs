using LostCompound.Tools;

namespace LostCompound.Items.Capabilities
{
    /// <summary>
    /// 护盾提供者：把「护盾」这件**资源**的加成单独成一条通道，与
    /// <see cref="IEquippable"/>（派生属性加成：STR / ATK / DEF）分开。
    ///
    /// 为什么必须分开（这是一个真 bug 的根因）：
    ///   Player 每回合开头都要把护盾清空再加回装备给的盾（用户明确要的「护盾每回合重置」），
    ///   而改造前这件事是靠在 ApplyEquipmentPassives 里顺带重置 Atk/Def 做的。
    ///   于是「消耗 1 块金属永久 +1 DEF」（Items\Metal.cs 的 `p.Def += 1`）会在**下一回合开头**
    ///   被那句还原冲掉 —— 不报错、不崩溃，只是静默失效。
    ///   根因是两类东西被塞进了同一条通道：
    ///     派生值（ATK/DEF）—— 由来源决定，可以随时从基础值重算；
    ///     资源（护盾）  —— 会被打掉，只能在一个明确的时机重置。
    ///   现在派生值走 IEquippable.ApplyPassive（由 Player.RecomputeStats 调用），
    ///   护盾走本接口的 ApplyShield（只由 Player.ResetShieldForTurn 调用），
    ///   两条通道互不牵动。
    ///
    /// 为什么是独立接口而不是给 IEquippable 加一个方法：与 <see cref="IOnHit"/> 同一思路 ——
    ///   只有真正提供护盾的道具才实现它，剑和盔甲不必写一个空的「我不给盾」。
    /// </summary>
    public interface IShieldSource
    {
        /// <summary>
        /// 把本道具的护盾加成发给玩家（每回合开头 / 换装 / 开战时各调用一次，
        /// 调用点见 <c>Player.ResetShieldForTurn</c>）。
        /// </summary>
        void ApplyShield(Player p);
    }
}
