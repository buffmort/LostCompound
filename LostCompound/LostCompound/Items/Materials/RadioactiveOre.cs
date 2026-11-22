using LostCompound.MapEnvironment;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Materials
{
    /// <summary>
    /// 放射性矿物（配方 0，ID 11）：材料，堆叠 99。
    /// 设计文档第九章原文：背包时每 5 回合扣 1（困难 2）血，可叠加。
    ///
    /// 获取途径：放射性区域的矿脉（地图上有 3 个以上的房间可以采）。
    ///
    /// ## 为什么本类自己不再计时（2026-11-22 改）
    /// 原先这里挂着 `ITurnCount` + 自己的 `Timer` 字段，每回合由
    /// `Player.HandleChangeableItems` 逐个道具推进。
    /// 现在辐射统一收进 <see cref="Buffs.NonEnvRadioactive"/> 这个 debuff：
    ///   · 状态栏要把「你还剩多少辐射」显示出来，散在道具里就显示不了；
    ///   · 放射金属罐(18) 要能**屏蔽**整类辐射 —— 屏蔽一个 buff 只有一处判断，
    ///     屏蔽「散在一堆道具里的 Timer」就要在 N 个道具里各判一次。
    /// 于是本类只剩两件东西：**声明数值**（间隔 / 每次伤害），以及
    /// 「我被放进背包了，请重建那张账」的通知。
    /// </summary>
    public sealed class RadioactiveOre(int count) : Item(ItemIds.RadioactiveOre, "放射性矿物", 99, true, count)
    {
        /// <summary>
        /// 每多少回合扣一次血。
        /// **这是本道具唯一的数值出口** —— <see cref="Buffs.NonEnvRadioactive"/> 直接读它，
        /// 不要在别处再写一遍 5。
        ///
        /// 困难模式改的是「每次伤害」（1 → 2），**不是**本间隔，见 DESIGN §7.4。
        /// </summary>
        public const int DamageInterval = 5;

        /// <summary>每次扣多少血（普通难度）。困难模式另算，等难度系统做出来再接。</summary>
        public const int DamagePerTick = 1;

        public override ItemType? Type => ItemType.RadioactiveOre;

        /// <summary>
        /// 道具进背包时重建辐射账（见 <see cref="Player.SyncRadiationBuff"/>）。
        ///
        /// 为什么挂在「进背包」而不是「每回合检查一次背包」：
        ///   辐射的存续完全由「身上有没有这个道具」决定，没有其它变量。
        ///   每回合扫两遍道具表纯属浪费，而进/出背包是个确定的事件。
        ///   （退出背包那条线由 <see cref="Player.RemoveConsumedItems"/> 统一处理，
        ///   它会调同一个重建方法 —— 那里管着所有移除路径，不会漏。）
        /// </summary>
        public void OnAcquired(Player p)
        {
            p?.SyncRadiationBuff();
        }

        public void OnBattleEnd(Player p, EnvType envType)
        {
            // 无需处理：辐射账由 NonEnvRadioactive 自己维护，跨战斗保留。
        }
    }
}
