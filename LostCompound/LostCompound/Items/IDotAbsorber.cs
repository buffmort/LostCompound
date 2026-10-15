using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 持有者对「周期伤害（DOT）」的额外处理。玩家被 buff 结算一次就通知一次，
    /// 用于「吸收若干点后损坏」这类效果（斗篷 27）。
    ///
    /// 为什么需要单独一条通道：周期性伤害（流血）由 <see cref="LostCompound.Buffs.BleedBuff"/>
    /// 在回合开场直接结算，装备既不参与 buff 列表、也拿不到那次结算，无法记账；
    /// 而「被 DOT 打了一次」这个信息只有结算点自己知道，必须由它显式地传出来。
    ///
    /// ⚠️ 与 <see cref="IDamageReduction"/> 的区别（两条通道按顺序衔接，不要混）：
    ///   IDamageReduction 是「所有伤害打 N 折」（在 TakeDamage 内部，DEF 之后、盾之前）；
    ///   本接口是「这一跳 DOT 先被我吃掉多少点」，被吸收的部分**根本不进 TakeDamage**
    ///   （不过 DEF、不动盾），剩下的残量才走正常受伤管线。
    ///
    /// 调用方（<see cref="LostCompound.Tools.Player.TakeDotDamage"/>）保证：
    ///   · 只在**明确的 DOT 结算**时调用（普通攻击、环境判负等都不走这条）；
    ///   · 背包 + 饰品栏里所有实现本接口的道具**各调一次**，按顺序把**尚未被吸收的残量**
    ///     传进来 —— 所以多个吸收者不会重复吃掉同一份伤害；
    ///   · 返回值会被夹进 [0, dotDamage]，道具不必自己防越界。
    /// </summary>
    public interface IDotAbsorber
    {
        /// <summary>
        /// 吸收本跳周期伤害。返回**实际吸收掉的点数**（0 = 不吸收，剩下的照常打进身体）。
        /// 记账（累计吸收了多少、要不要损坏）由实现方自己负责 —— 调用方只关心「少了多少伤害」。
        /// </summary>
        /// <param name="owner">被结算的玩家（与 <see cref="IOnHit.OnHit"/> 同一风格，把上下文显式传进来）。</param>
        /// <param name="dotDamage">本跳**尚未被别的吸收者吃掉**的伤害量。</param>
        double AbsorbDot(Player owner, double dotDamage);
    }
}
