using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 斗篷（ID 27，饰品，堆叠 1）：合成 = 3 木头 + 1 金属 + 1 布料（配方 0）。
    ///
    /// 文档依据（DESIGN 第九章）：「+5 DEF，20% 免伤，累计吸收 20 点 DOT 伤害后破损」。
    ///
    /// 三条效果各走一条通道，互不牵动：
    ///   · +5 DEF    → <see cref="IEquippable"/>（派生值重算通路，背包 / 饰品栏都生效）；
    ///   · 20% 免伤  → <see cref="IDamageReduction"/>（Player.TakeDamage 在 DEF 之后、盾之前乘 0.8）；
    ///   · 吸收 DOT  → <see cref="IDotAbsorber"/>（Player.TakeDotDamage 每跳结算通知一次），
    ///                 吸满 20 点后由 <see cref="ShouldRemove"/> 报告「该走了」，
    ///                 Player 把它从背包 / 饰品栏里收走 —— **破损 = 道具消失**，
    ///                 与镐子用完耐久（`Durability &lt;= 0` → `ShouldRemove`）是同一套机制。
    ///
    /// ⚠️ 累计吸收值为什么是**实例字段**：每件斗篷各记各的账。
    ///   static 会让「打坏一件斗篷」把背包里另一件全新的斗篷一起变成破损；
    ///   记在 Player 上则反过来 —— 换一件新斗篷会继承旧斗篷的旧账。
    ///   本项目对**不可堆叠**道具（MaxStack 1）是「一格一件、不合堆」（Player.AddItem），
    ///   所以「一件斗篷 = 一个实例」成立，实例字段就是正确的记账粒度。
    ///   放进饰品栏也不会换实例：EquipAccessory 只是把同一个对象从 Inventory 移到
    ///   Accessories，所以「在背包里攒的账」与「在饰品栏里攒的账」天然是同一份。
    ///
    /// 历史（已解决，保留供追溯）：
    ///   · 获取方式：文档原先写「放射性区域低概率」，用户裁定 6 改为「合成」，文档已对齐；
    ///   · 「免疫 N 次」：文档写 3 次流血、任务书写 20 次 dot，用户裁定**改成按值表达** ——
    ///     格挡那套「按次数完全免疫」早已整体删除（CONVENTIONS 三 / DESIGN 7.1），
    ///     所以这里记的是**累计吸收的点数**，阈值沿用用户给的 20。
    /// </summary>
    public sealed class Cloak(int count)
        : Item(ItemIds.Cloak, "斗篷", 1, false, count), IEquippable, IDamageReduction, IDotAbsorber, IChangeable
    {
        public override ItemType? Type => ItemType.Cloak;

        /// <summary>背包 / 饰品栏时提供的防御加成（文档：+5 DEF）。</summary>
        public const double DefBonus = 5;

        /// <summary>受伤减免比例（文档：20% 免伤）。</summary>
        public const double ReductionRatio = 0.20;

        /// <summary>累计吸收多少点 DOT 伤害后破损（用户裁定：沿用 20）。</summary>
        public const double DotAbsorbThreshold = 20;

        /// <summary>
        /// 本件斗篷往后再吸收多少点才破 —— 这是**道具自己的账本**，与减伤无关，
        /// 所以不参与 <see cref="IEquippable.ApplyPassive"/> 那条「每次重算都从基础值来」的通路
        /// （写在那里会被回合开头 / 换装 / buff 变化反复清零或叠加，见 Player.RecomputeStats）。
        /// </summary>
        private double _absorbedDot;

        /// <summary>累计已吸收的 DOT 点数（供显示；每件实例各自一份）。</summary>
        public double AbsorbedDot => _absorbedDot;

        /// <summary>是否已经吸满、该破损了。</summary>
        public bool IsBroken => _absorbedDot >= DotAbsorbThreshold;

        /// <summary>减伤比例（0.2 = 20%）。由 Player.DamageReductionMultiplier 与其它来源连乘。</summary>
        public double DamageReductionRatio => ReductionRatio;

        /// <summary>
        /// 只加 DEF（累加式，由 Player.RecomputeStats 保证「先还原成基础值再应用」）。
        /// 「20% 免伤」不能写在这里：ApplyPassive 是**重算通路**，一场战斗里会被调用很多次
        /// （回合开头 / buff 变化 / 环境变化…），把减伤当状态记在这里会被反复叠加/清零；
        /// 它走 <see cref="IDamageReduction"/>，每次受伤现读一次。
        /// </summary>
        public void ApplyPassive(Player p) => p.Def += DefBonus;

        /// <summary>
        /// 吸收这一跳 DOT 伤害，最多只吃到「还差多少点破」为止 —— 不会多吸、也不会溢出。
        /// 硬吃一整个血量级别的伤害在这里是不可能的：上限就是阈值 20。
        /// </summary>
        public double AbsorbDot(Player owner, double dotDamage)
        {
            if (IsBroken || dotDamage <= 0) return 0;

            double absorbed = Math.Min(DotAbsorbThreshold - _absorbedDot, dotDamage);
            _absorbedDot += absorbed;

            Console.WriteLine("斗篷吸收了" + absorbed + "点持续伤害（累计" + _absorbedDot + "/" + DotAbsorbThreshold + "）。");
            if (IsBroken)
            {
                Console.WriteLine("斗篷吸满了，已经破损！");
            }
            return absorbed;
        }

        /// <summary>
        /// 破损即移除。走 <see cref="IChangeable"/> 的既有判定（与镐子耐久归零同源），
        /// 由 Player 在 DOT 结算后统一收走（背包与饰品栏都扫，见 Player.RemoveBrokenEquippedItems）。
        /// </summary>
        public bool ShouldRemove() => IsBroken;

        /// <summary>破损后什么都不留下（不是「变成」别的道具）。</summary>
        public Item Become() => null;

        public string DescribePassive() =>
            "背包 +" + DefBonus + " DEF，受伤 -" + ReductionRatio * 100 + "%，DOT 吸收 " +
            _absorbedDot + "/" + DotAbsorbThreshold;
    }
}
