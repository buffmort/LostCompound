using LostCompound.MapEnvironment;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Equipment
{
    /// <summary>
    /// 放射金属罐（配方 1，ID 18）：饰品/容器，堆叠 10。
    /// 合成材料：2 放射矿 + 金属罐。
    /// 设计文档第九章原文：继承 11 重写（10 回合掉 2），饰品栏时屏蔽非环境辐射伤害。
    ///
    /// 「继承 11」= 和放射性矿物一样是「背包时定期扣血」，只是参数不同（10 回合 / 2 血）。
    /// 所以两者的计时代码在 <see cref="Buffs.NonEnvRadioactive"/> 里，本类只声明自己的数值。
    /// </summary>
    /// <remarks>
    /// 为什么不实现 IEquippable（尽管文档说「饰品栏时屏蔽」）：
    ///   本类的功能性被动是「屏蔽**其它**辐射」，与 <see cref="IEquippable.ApplyPassive"/>
    ///   那种「给自己加 ATK/DEF」的静态加成不是一类；
    ///   而且 <see cref="Buffs.NonEnvRadioactive"/> 判断屏蔽时是直接在
    ///   `p.Accessories` 里找本类的实例 —— 也就是说它可以被放进饰品栏，只是不走被动加成那条路。
    ///   CONVENTIONS 2.4 的顾虑（出现在可装备列表里并打印假的加成描述）在这里不成立，
    ///   因为「放射金属罐」本来就该出现在可装备列表里。
    /// </remarks>
    public sealed class RadioactiveMetalCan(int count)
        : Item(ItemIds.RadioactiveMetalCan, "放射金属罐", 10, true, count), IEquippable
    {
        /// <summary>每多少回合扣一次血（覆盖放射性矿物的 5 回合）。</summary>
        public const int DamageInterval = 10;

        /// <summary>每次发作扣几点血（覆盖放射性矿物的 1 点）。</summary>
        public const int DamagePerTick = 2;

        public override ItemType? Type => ItemType.RadioactiveMetalCan;

        /// <summary>
        /// 戴在饰品栏时**屏蔽全部非环境辐射**（含它自己那一份）——
        /// 设计文档：「18 继承 11，饰品栏时屏蔽非环境辐射伤害」。
        ///
        /// 实现方式是 <see cref="Buffs.NonEnvRadioactive"/> 每次 Sync 时问一句
        /// 「饰品栏里有没有本类实例」（见该类的 Shielded 判断），
        /// 所以真正的屏蔽逻辑在那里，**不在这里** —— 本类只负责「我在饰品栏里」这个事实。
        /// 把判断写在发起方（buff）而不是道具里，好处是「谁施加、谁算伤害」这条线不被切断。
        /// </summary>
        public void ApplyPassive(Player p)
        {
            // 被动数值：无（屏蔽在 buff 侧）。
        }

        public string DescribePassive() => "放在饰品栏里：屏蔽非环境辐射";

        /// <summary>
        /// 道具进背包时重建辐射账。与 <see cref="Materials.RadioactiveOre.OnAcquired"/> 同一理由。
        /// </summary>
        public void OnAcquired(Player p)
        {
            p?.SyncRadiationBuff();
        }

        public void OnBattleEnd(Player p, EnvType envType)
        {
            // 无需处理：辐射账由 NonEnvRadioactive 自己维护。
        }
    }
}
