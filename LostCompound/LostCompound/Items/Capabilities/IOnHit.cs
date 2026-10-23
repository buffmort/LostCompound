using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items.Capabilities
{
    /// <summary>
    /// 命中时触发的武器特效（如磷化剑挂流血）。
    ///
    /// 与 <see cref="IEquippable"/>（背包/饰品栏里被动生效）是同一思路的第二个口子：
    /// 道具自己声明「我会在什么时候做什么」，而不是让 Player 逐个认识具体道具类。
    ///
    /// 为什么要有它：原先 Player.Attack 里写的是
    ///   <c>if (target.IsAlive &amp;&amp; HasPhosphorusSword()) { target.AddBuff(new BleedBuff(...)); ... }</c>
    /// —— 通用攻击流程里塞了一个具体武器的名字、数值和文案。加第二把带特效的武器
    /// （中毒、燃烧、吸血……）就得再往 Attack 里加一段 if。
    /// 现在 Attack 只遍历身上的 IOnHit 各触发一次，对武器种类一无所知。
    /// </summary>
    public interface IOnHit
    {
        /// <summary>
        /// 本道具的攻击命中效果，由 <see cref="Player.Attack"/> 在「命中且目标还活着」时调用。
        /// 具体的加成数值与播报文案由道具自己负责（它们属于这件武器的语义）。
        /// </summary>
        void OnHit(Player attacker, Monster target);
    }
}
