using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Items;
using LostCompound.Monsters;
using LostCompound.Maps;
using LostCompound.UI;
using LostCompound.Buffs;
using LostCompound.Items.Capabilities;
using LostCompound.Items.Materials;
using LostCompound.Items.Consumables;
using LostCompound.Items.Containers;
using LostCompound.Items.Equipment;
using LostCompound.Rooms.Types;
using LostCompound.Tools.Debug;
using LostCompound.Buffs.Types;

namespace LostCompound.Tools.Players
{
    public partial class Player
    {

        // ==================== 基础值（永久成长的载体） ====================
        // 本项目把「会变的数值」分成两类，存放与改写方式完全不同（CONVENTIONS 2.3）：
        //   派生值 Str / Atk / Def —— 由来源决定，永远等于「基础值 + 环境 + buff + 装备」，
        //                             任何时刻都能从基础值重算（RecomputeStats），
        //                             所以**直接改它们没有意义**（下次重算就没了）；
        //   资源   Hp / Shield     —— 会被消耗（掉血 / 吃盾），**绝不能进重算通路**
        //                             （重算等于回满，是静默的无限治疗）。
        // 于是每个派生属性配一个 BaseXxx：想永久变强就改基础值（GrowBaseXxx），
        // 想临时变强就提供修正量（环境 StrModifier / Buff.StrBonus 等）。
        // 基础值一律 private set，出口只有下面那几个 Grow 方法 ——「直接改派生值」这种写法
        // 编译得过、运行不报错，只会静默失效（金属的 +1 DEF 原先就是这么被冲掉的）。

        /// <summary>
        /// 基础 HP（设计文档 7.3：200）。它是**资源的上限**而不是派生值 ——
        /// 不参与 <see cref="RecomputeStats"/>（那会把已经掉掉的血回满）。
        /// 永久加生命值走 <see cref="GrowBaseHp"/>。
        /// </summary>
        public double BaseHp { get; private set; } = 200;

        /// <summary>
        /// 基础 STR（设计文档 7.3：3）。这是角色的**固有属性**，是「永久成长」的载体
        /// —— 如通关奖励、永久强化道具，将来都走 <see cref="GrowBaseStr"/> 改它。
        ///
        /// 为什么不是 const：const 是编译期常量，谁也改不了，
        /// 那样「永久 +STR 的道具」就无处安放。
        /// 为什么不是 static：每个 Player 实例各自一份。static 会让
        /// 多场对局（或调试玩家 + 正式玩家）共用同一个值而互相干扰。
        /// </summary>
        public double BaseStr { get; private set; } = 3.0;

        /// <summary>基础 ATK（设计文档 7.3：20；攻击力公式 2 + STR × √ATK 里的 ATK）。</summary>
        public double BaseAtk { get; private set; } = 20;

        /// <summary>基础 DEF（设计文档 7.3：5；伤害减免 RealDmg = Basic - 0.5 × DEF 里的 DEF）。</summary>
        public double BaseDef { get; private set; } = 5;

        // 派生值：由 RecomputeStats 一处写入。
        // Str 连 setter 都不对外开放；Atk / Def 的 setter 仍是 public —— 因为装备的被动加成
        // 走 IEquippable.ApplyPassive(Player p)（道具自己 p.Atk += 加成），收窄成 internal
        // 在同一程序集里也挡不住误用，而改那个接口签名属于「改 public API」，
        // 按 CONVENTIONS 1.2 要先问。真正的纪律写在 RecomputeStats 的注释里。
        public double Atk { get; set; }
        public double Def { get; set; }

        /// <summary>
        /// 当前有效 STR = 基础 STR + 环境修正 + buff 修正（由 <see cref="RecomputeStats"/> 写入）。
        ///
        /// 只读（private set）：外部想改 STR 必须二选一 ——
        ///   临时效果 → 走环境 StrModifier / buff StrBonus；
        ///   永久成长 → 走 GrowBaseStr。
        /// 直接把 Str 设成某个值没有意义：下一次 RecomputeStats 就会把它冲掉
        /// （debug 的 127 曾因此只能生效一回合）。
        ///
        /// 伤害计算读的是这个值，不是 BaseStr。
        /// </summary>
        public double Str { get; private set; }

        /// <summary>
        /// 当前血量（**资源**：会被扣，不参与重算 —— 见本区块开头的说明）。
        /// </summary>
        public double Hp { get; set; }

        /// <summary>
        /// 构造函数：给出资源与派生值的初值。
        ///
        /// 为什么不能写成 `public double Hp { get; set; } = BaseHp;`：
        ///   C# 的属性初始化器不能引用同一个实例上的非静态成员（已实测报 CS0236
        ///   「字段初始值设定项无法引用非静态字段、方法或属性」）。原来能写
        ///   `= BaseAtk` 只是因为那时 BaseAtk 还是 const（编译期常量）。
        ///   基础值改成「可变的实例属性」之后，初值只能改在这里给。
        /// </summary>
        public Player()
        {
            Hp = BaseHp;
            // 派生值先算一次：此时没有装备、没有 buff、环境为 null，
            // 结果就是设计文档的初始值 STR 3 / ATK 20 / DEF 5。
            // （改造前 Str 的初值是 0，要等进战斗第一次重算才变成 3；
            //   0 从来没有被任何显示或计算读到过 —— 状态栏的「力量」只在战斗中显示 ——
            //   所以这次顺手改成了正确的初值，不改变任何可观察行为。）
            RecomputeStats();
        }

        /// <summary>
        /// 永久提升基础 STR（通关奖励 / 永久强化道具用），并立刻重算派生值。
        /// 这是「永久成长」的唯一入口 —— 直接写 Str 会在下次重算时被冲掉。
        /// 传负数即为永久降低。
        /// </summary>
        public void GrowBaseStr(double amount)
        {
            BaseStr += amount;
            RecomputeStats();
        }

        /// <summary>
        /// 永久提升基础 ATK（同 <see cref="GrowBaseStr"/>：改的是基础值，不是派生的 Atk）。
        /// 目前没有调用者（还没有「永久 +ATK」的道具/奖励），与 GrowAccessorySlots 一样
        /// 先把「永久成长」的出口留好，免得将来又有人去直接改 Atk。
        /// </summary>
        public void GrowBaseAtk(double amount)
        {
            BaseAtk += amount;
            RecomputeStats();
        }

        /// <summary>
        /// 永久提升基础 DEF。金属的「消耗 1 块永久 +1 DEF」走这条路（见 <see cref="Items.Metal"/>）——
        /// 改造前它直接写 p.Def += 1，而每回合开头的护盾重置会顺带把 Def 还原成基础值，
        /// 于是那 1 点 DEF 在下一回合开头就没了：不报错、不崩溃，只是静默失效。
        /// </summary>
        public void GrowBaseDef(double amount)
        {
            BaseDef += amount;
            RecomputeStats();
        }

        /// <summary>
        /// 永久提升基础 HP（将来「通关奖励 +生命值」用）。
        /// 顺手把当前血量也抬同样多：BaseHp 是血量上限，只加上限不加当前血量，
        /// 奖励看起来会像没生效（这与 Shield 是同一个「资源不能重算」的道理）。
        /// 目前没有调用者；传负数即为降低上限（当前血量也会跟着降，调用方自己保证别把玩家扣死）。
        /// </summary>
        public void GrowBaseHp(double amount)
        {
            BaseHp += amount;
            Hp += amount;
        }

        /// <summary>直接把基础 STR 设成某个值（调试用，避免写 GrowBaseStr(目标 - BaseStr) 这种别扭算式）。</summary>
        public void SetBaseStr(double value)
        {
            BaseStr = value;
            RecomputeStats();
        }

        public string Name { get; set; }

        public bool IsDead { get; set; }

        // 存活判定：派生值用计算属性（原 isAlive() 方法）
        public bool IsAlive => Hp > 0;

        /// <summary>
        /// 重算所有派生值：STR / ATK / DEF（CONVENTIONS 2.3 的「重算」语义）。
        ///
        /// 为什么必须「从基础值重算」，而不是谁想改就 -= / += ：
        ///   改这些值的来源有好几个（环境、buff、装备）。用加减法的话，
        ///   每个来源都得自己负责「什么时候加回来」—— buff 有三条移除路径
        ///   （自然到期 / 战斗结束 ClearBuffs / 将来的驱散），漏掉任何一条，
        ///   属性就永久偏移一点，而且不报错、不崩溃，只是「打着打着变弱了」。
        ///   重算没有这些问题：不管谁改了什么，值永远等于「基础 + 当前所有修正之和」。
        ///
        /// ⚠️ 三种修正的**叠加顺序不能改**：装备必须放最后。
        ///   装备的通道是 IEquippable.ApplyPassive(p)，道具自己写 `p.Atk += 加成`
        ///   —— 是累加式，必须在上面把 Atk/Def 赋成基础值**之后**才能加；
        ///   否则打第二场会再叠一次，十场之后 ATK 就爆了。
        ///   环境与 buff 提供的是「修正量」（返回增量，不写结果），谁先谁后只受加法交换律影响，
        ///   放在前面读起来更接近「基础 + 各方修正」这句话。
        ///
        /// ⚠️ 这里**不碰** Hp / Shield：它们是资源，会被消耗（掉血 / 吃盾），
        ///   进来重算等于回满 —— 那是静默的无限治疗。护盾的每回合重置是另一条通路
        ///   （见 <see cref="ResetShieldForTurn"/>）。
        /// </summary>
        public void RecomputeStats()
        {
            // 1. 从基础值重来（「先重置再应用」，见 CONVENTIONS 2.3）
            Str = BaseStr;
            Atk = BaseAtk;
            Def = BaseDef;

            // 2. 环境修正（如熔岩瀑布缺水扣 STR）。
            //    环境目前只声明 STR 的修正量；将来若有环境要改 ATK/DEF，
            //    照 StrModifier 的样子加一个虚属性，在这里读一行即可。
            Environment env = CurrentEnvironment;
            if (env != null) Str += env.StrModifier;

            // 3. buff 修正（如某个增益 +1 STR）。buff 消失时不需要谁去「加回来」：
            //    下次重算就是「基础 + 当前还在的修正」。
            foreach (Buff b in Buffs)
            {
                Str += b.StrBonus;
                Atk += b.AtkBonus;
                Def += b.DefBonus;
            }

            // 4. 装备加成 —— 必须是最后一步，理由见本方法注释
            ForEachEquippedItem(item =>
            {
                if (item is IEquippable eq) eq.ApplyPassive(this);
            });
        }
    }
}
