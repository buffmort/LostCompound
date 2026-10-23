using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.Tools.Players;

namespace LostCompound.Items.Equipment
{
    /// <summary>
    /// 斗篷（ID 27，饰品，堆叠 1）：合成 = 3 木头 + 1 金属 + 1 布料（配方 0）。
    ///
    /// 文档依据（DESIGN 第九章）：「+5 DEF，20% 免伤，受伤 50 次后破损消失」。
    ///
    /// 三条效果各走一条通道，互不牵动：
    ///   · +5 DEF     → <see cref="IEquippable"/>（派生值重算通路，背包 / 饰品栏都生效）；
    ///   · 20% 免伤   → <see cref="IDamageReduction"/>（Player.TakeDamage 在 DEF 之后、盾之前乘 0.8）；
    ///   · 受伤 50 次破损 → <see cref="IOnDamaged"/>（Player 每**真的扣到血**就通知一次），
    ///                 记满 50 次后由 <see cref="ShouldRemove"/> 报告「该走了」，
    ///                 Player 把它从背包 / 饰品栏里收走 —— **破损 = 道具消失**，
    ///                 与镐子用完耐久（`Durability &lt;= 0` → `ShouldRemove`）是同一套机制。
    ///
    /// 计数口径（用户裁定「受伤 50 次后消失」的落地，**记次数、不记点数**）：
    ///   1 次 = 「这一跳伤害真的打进了身体（Hp 被扣）」，打 1 点与打 10 点都只记 1 次；
    ///   **被盾完全挡下、血一点没掉的那一跳不记** —— 那一跳没有伤到身体，
    ///   斗篷的耐久不该因为盾厚而被消耗（判定落在 Player.TakeDamage 的两条分支上，见那里的注释）。
    ///
    /// ⚠️ 计数为什么是**实例字段**：每件斗篷各记各的账。
    ///   static 会让「打坏一件斗篷」把背包里另一件全新的斗篷一起变成破损；
    ///   记在 Player 上则反过来 —— 换一件新斗篷会继承旧斗篷的旧账。
    ///   本项目对**不可堆叠**道具（MaxStack 1）是「一格一件、不合堆」（Player.AddItem），
    ///   所以「一件斗篷 = 一个实例」成立，实例字段就是正确的记账粒度。
    ///   放进饰品栏也不会换实例：EquipAccessory 只是把同一个对象从 Inventory 移到
    ///   Accessories，所以「在背包里攒的账」与「在饰品栏里攒的账」天然是同一份。
    ///
    /// 历史（已解决，保留供追溯）：
    ///   · 获取方式：文档原先写「放射性区域低概率」，用户裁定 6 改为「合成」，文档已对齐；
    ///   · 「免疫 N 次」：文档写 3 次流血、任务书写 20 次 dot，用户裁定**改成按值表达** → 实现成
    ///     「累计吸收 20 点 DOT 伤害后破损」（走 IDotAbsorber，见 Items\Capabilities\IDotAbsorber.cs）；
    ///   · **本次（用户新裁定）**：去掉「吸收 DOT」这一条，改成**按次数**的「受伤 50 次后消失」。
    ///     于是斗篷不再实现 <see cref="IDotAbsorber"/> —— 那条通道目前**没有任何实现者**
    ///     （接口与 Player.TakeDotDamage 的去留待用户裁定，先保留不动）。
    ///
    /// 为什么新机制不能复用 IDotAbsorber：那条通道的口径是「DOT 这一跳打来多少点原始伤害」，
    ///   它是**在 DEF / 盾之前**吃掉伤害的；而「受伤次数」的口径是**结算完之后血掉了没有**，
    ///   两者连发生的位置都不同（一个改数值、一个收事件），硬揉在一起会让两个口径互相污染。
    /// </summary>
    public sealed class Cloak(int count)
        : Item(ItemIds.Cloak, "斗篷", 1, false, count), IEquippable, IDamageReduction, IOnDamaged, IChangeable
    {
        public override ItemType? Type => ItemType.Cloak;

        /// <summary>背包 / 饰品栏时提供的防御加成（文档：+5 DEF）。</summary>
        public const double DefBonus = 5;

        /// <summary>受伤减免比例（文档：20% 免伤）。</summary>
        public const double ReductionRatio = 0.20;

        /// <summary>
        /// 挨过多少次伤害后破损（用户裁定：50 次）。
        /// ⚠️ 这是**次数**不是点数额：一次打 1 点与一次打 10 点都只记 1 次
        /// （旧机制记的是「累计吸收的点数」，口径完全不同，勿照旧理解）。
        /// </summary>
        public const int MaxDamageCount = 50;

        /// <summary>
        /// 本件斗篷已记下的受伤次数 —— 这是**道具自己的账本**，与减伤无关，
        /// 所以不参与 <see cref="IEquippable.ApplyPassive"/> 那条「每次重算都从基础值来」的通路
        /// （写在那里会被回合开头 / 换装 / buff 变化反复清零或叠加，见 Player.RecomputeStats）。
        /// </summary>
        private int _damageTakenCount;

        /// <summary>已记录在案的受伤次数（供显示；每件实例各自一份）。</summary>
        public int DamageTakenCount => _damageTakenCount;

        /// <summary>是否已经记满、该破损了。</summary>
        public bool IsBroken => _damageTakenCount >= MaxDamageCount;

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
        /// 记一次「受伤」。只有伤害真的打进身体（`Hp` 被扣）时才会被调用
        /// （见 Player.TakeDamage 的扣血分支 → Player.NotifyDamaged）——
        /// 被盾完全挡下的那一跳**不会**到这里，所以「盾厚 = 斗篷耐久被白吃」不会发生。
        /// </summary>
        public void OnDamaged(Player owner)
        {
            // 已经记满但还没被收走的空窗期（同一跳里多个通知者）不再重复记账
            if (IsBroken) return;

            _damageTakenCount++;
            Console.WriteLine("斗篷破损进度（" + _damageTakenCount + "/" + MaxDamageCount + "）。");
            if (IsBroken)
            {
                Console.WriteLine("斗篷已经破损，消失了！");
            }
        }

        /// <summary>
        /// 破损即移除。走 <see cref="IChangeable"/> 的既有判定（与镐子耐久归零同源），
        /// 由 Player 在受伤通知后统一收走（背包与饰品栏都扫，见 Player.RemoveBrokenEquippedItems）。
        /// </summary>
        public bool ShouldRemove() => IsBroken;

        /// <summary>破损后什么都不留下（不是「变成」别的道具）。</summary>
        public Item Become() => null;

        public string DescribePassive() =>
            "背包 +" + DefBonus + " DEF，受伤 -" + ReductionRatio * 100 + "%，已承伤 " +
            _damageTakenCount + "/" + MaxDamageCount;
    }
}
