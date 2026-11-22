using LostCompound.MapEnvironment.Environments;
using LostCompound.Tools;
using LostCompound.Tools.Players;
using LostCompound.Items.Capabilities;

namespace LostCompound.Items.Equipment
{
    /// <summary>
    /// 盖革计数器（ID 25，饰品，堆叠 1）。
    ///
    /// 文档依据（DESIGN §7.4）：原本写的是「辐射间隔 +5 秒」，
    /// **Hyberty 2026-11-22 改成 ×1.25**。
    ///
    /// 为什么改成乘法：加法在区间两端给的体感完全不均匀 —— 最毒的房间 5 秒变 10 秒（翻倍），
    /// 最安全的 30 秒只变 35 秒（几乎没感觉）。乘法则是「毒的地方也减一分」，全图一致。
    ///
    /// ## 为什么没有 ApplyPassive 的实际内容
    /// 它改的是**环境计时**，不是玩家的 STR / ATK / DEF。而 <see cref="IEquippable"/>
    /// 那条通道的口径是「重算派生值时累加属性」，把它塞进去等于每次重算都要去碰环境，
    /// 方向是错的：环境才是「这一层多少秒扣血」的权威。
    /// 所以真正读它的是 <see cref="RadioactiveArea"/>：它每次刷屏从 `p.Accessories`
    /// 里找 Id == <see cref="ItemIds.GeigerCounter"/>，找到就把间隔乘一次。
    ///
    /// 这也是为什么它必须实现 <see cref="IEquippable"/> 却什么都不加：
    /// 该接口是「能放进饰品栏」的通行证，不是「必须有属性加成」的义务。
    /// </summary>
    public sealed class GeigerCounter(int count)
        : Item(ItemIds.GeigerCounter, "盖革计数器", 1, false, count), IEquippable
    {
        public override ItemType? Type => ItemType.GeigerCounter;

        /// <summary>不做任何属性加成 —— 效果在环境里（见类注释）。</summary>
        public void ApplyPassive(Player p) { }

        public string DescribePassive() =>
            "放在饰品栏里：放射间隔 ×" + RadioactiveArea.GeigerMultiplier;
    }
}
