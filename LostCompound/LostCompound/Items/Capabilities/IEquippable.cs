using LostCompound.Tools;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Capabilities
{
    /// <summary>
    /// 背包被动道具：放在背包里就生效。
    ///
    /// 设计文档里很多道具是「背包 +7ATK」「背包 +10DEF」这种描述 ——
    /// 它们不需要玩家主动使用，只要待在背包里就该起作用。
    ///
    /// 生效方式：Player 每次重算派生值（<c>Player.RecomputeStats</c>：开战 / 换装 /
    /// 回合开头 / buff 或环境变化）都会把所有被动装备的加成重新加一遍。注意是「先重置再应用」
    /// —— 每次重算都先把 Atk/Def 赋回基础值，所以这里写 `p.Atk += 加成` 不会越叠越多
    /// （CONVENTIONS 2.3）。
    ///
    /// ⚠️ 本接口只管**派生值**（STR / ATK / DEF）。**资源**（护盾）不能写进
    /// <see cref="ApplyPassive"/>：重算在一场战斗里会被调用很多次，写在这里等于每次重算
    /// 都把盾补满。要提供护盾请实现 <see cref="IShieldSource"/>（见 Shield 那件道具）。
    /// </summary>
    public interface IEquippable
    {
        /// <summary>
        /// 把本道具的派生属性加成应用到玩家身上（每次重算派生值时调用一次）。
        /// 只允许 `p.Atk += ...` 这类**累加**写法（由调用方保证先重置再应用），
        /// 不要在这里改资源（Hp / Shield）、也不要改基础值。
        /// </summary>
        void ApplyPassive(Player p);

        /// <summary>给玩家看的一行效果说明，用于列表展示。</summary>
        string DescribePassive();
    }
}
