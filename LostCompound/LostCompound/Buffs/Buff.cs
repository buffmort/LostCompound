using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Buffs
{
    /// <summary>
    /// 状态效果基类。每个效果是一个对象，挂在 Player 或 Monster 身上，
    /// 由战斗回合管线统一处理（见 Tools.RunBattle）。
    ///
    /// 为什么用对象而不是散落的字段？
    ///   以前持续伤害是 Player/Monster 上的两个字段（DotDamage/DotDuration），只能存在一种，
    ///   想加第二种持续伤害就要再加两个字段。对象化之后，挂几个就是几个，
    ///   而且每个 buff 的结算规则互不干扰。
    ///   DOT 字段与结算已按你的要求整体删除 —— 周期性伤害现在只有 buff 这一条出口。
    ///
    /// 注意：本类只负责「结算自己」，不负责「什么时候结算」——
    /// 时机统一由 Tools.ApplyBuffs 决定（大回合开场，收尸之前）。
    /// </summary>
    public abstract class Buff
    {
        /// <summary>
        /// 初始效果量与初始剩余回合。子类构造函数传进来，避免每个子类自己写一遍字段初始化。
        /// 这里不能直接给 Stacks 赋值：MaxStacks 是虚属性，赋值必须走 AddStacks 才能保证不超过上限。
        /// </summary>
        protected Buff(int stacks, int turns)
        {
            AddStacks(stacks);
            RemainingTurns = Math.Max(turns, 0);
        }

        /// <summary>显示名（如「流血」）。</summary>
        public abstract string Name { get; }

        /// <summary>
        /// 效果量。**它的语义由各 buff 子类自己定义**，基类不规定单位：
        ///   - 流血里 = 每回合扣血数（不是「层数」）；
        ///   - 超雄综合症里 = 强度（当前实现并不读它）。
        /// 所以基类任何地方都不得把它当成「伤害」来用或来解释 ——
        /// 曾经基类注释写的是「流血里它的语义是每回合扣血数」，
        /// 结果第二个 buff（SuperMaleBuff，不扣血）一进来，注释就和事实矛盾了。
        ///
        /// 为什么流血那边改成伤害而不是层数：口径是「上限 10 管的是每回合伤害」——
        /// 现有 7 点再加 5 点应该变成 10 点，而不是 12 层。
        /// 这是**流血自己**的约定，写在 <see cref="BleedBuff"/> 里。
        /// </summary>
        public int Stacks { get; protected set; }

        /// <summary>效果量上限。默认不限 —— 上限也是各 buff 自己的规则（流血是 10）。</summary>
        public virtual int MaxStacks => int.MaxValue;

        /// <summary>
        /// 剩余回合：每结算一次 -1（由各 buff 在自己的 OnTurnTick 里走 ConsumeTurn），
        /// 归零则该效果失效。
        /// 通用规则：`ConsumeTurn` / `AddTurns` 只管数量，**「什么时候算一个回合」
        /// 由各 buff 自己在 OnTurnTick 里决定**（例如流血每次结算都走一回合，
        /// 而超雄只在玩家回合走一回合）—— 基类不假定结算节奏。
        ///
        /// 为什么单独存一个字段而不是把剩余回合并进 Stacks：
        ///   上限只约束「效果量」，而持续时间可以是任意长（同类叠加时相加），
        ///   两者混在一个数里就无法既夹取效果量又保留时长。
        /// </summary>
        public int RemainingTurns { get; private set; }

        /// <summary>
        /// 是否已失效（该移除了）：剩余回合归零，或效果量为 0。
        /// 效果量为 0 也算失效 —— 留着它每回合什么都不做，只会在状态栏里显示一条假状态。
        ///
        /// ⚠️ 这条默认规则本身就是「按流血这类周期性伤害 buff 写的」：它假定「效果量归零 = 没意义」。
        /// 对**效果量与持续时间无关**的 buff（如「开关型」buff、只靠 RemainingTurns 计时的 buff）
        /// 这个默认值是错的。所以它是 virtual：这类子类自己覆写即可
        /// （例如改成 `=> RemainingTurns <= 0`），不必去改基类。
        /// </summary>
        public virtual bool IsExpired => RemainingTurns <= 0 || Stacks <= 0;

        /// <summary>叠加效果量（不超过 MaxStacks）。</summary>
        public void AddStacks(int n)
        {
            Stacks = Math.Min(Stacks + n, MaxStacks);
        }

        /// <summary>
        /// 累加剩余回合。同类叠加时用「两者之和」——
        /// 与旧 DOT 的 AddDotDuration 就是 += 累加的语义保持一致。
        /// </summary>
        public void AddTurns(int n)
        {
            RemainingTurns = Math.Max(RemainingTurns + n, 0);
        }

        /// <summary>结算一次剩余回合（每回合调用一次）。归零后 IsExpired 为真，由回合管线移除。</summary>
        public void ConsumeTurn()
        {
            RemainingTurns = Math.Max(RemainingTurns - 1, 0);
        }

        /// <summary>
        /// 同类叠加的**默认**规则：效果量取 min(现有 + 新增, 上限)，剩余回合取两者之和。
        /// 这套默认语义是**按周期伤害类 buff（流血）定的**：效果量越大越疼、时长相加越长。
        /// 其它类型的 buff（开关型、只刷时长不叠强度等）可以覆写本方法。
        /// 为什么放在基类：Player.AddBuff 和 Monster.AddBuff 都要做这件事
        /// （CONVENTIONS 2.5：同一段逻辑出现两遍就提到基类），否则以后加第三种持有者又要再抄一遍。
        ///
        /// ⚠️ 这只是一个默认实现，**「叠加 = 效果量相加」并不是所有 buff 都成立的语义**
        /// （旧 DOT 的 AddDotDuration 是 += 累加，所以这里沿用了加法）。
        /// 如果某个 buff 的叠加规则不是「相加」（例如「重复挂只刷新时长、不叠强度」），
        /// 覆写本方法（必要时配合 AddStacks/AddTurns）即可，不必改基类。
        /// </summary>
        public virtual void MergeFrom(Buff other)
        {
            AddStacks(other.Stacks);
            AddTurns(other.RemainingTurns);
        }

        /// <summary>
        /// 减少效果量（如设计文档里的「万能药减 4 层流血」）。
        /// 减到 0 即 IsExpired，由回合管线负责移除。万能药本身尚未实现，这里先把口子留好。
        /// </summary>
        public void RemoveStacks(int n)
        {
            Stacks = Math.Max(Stacks - n, 0);
        }

        /// <summary>
        /// 本 buff 对 STR 的修正量（正数加强、负数削弱），默认 0。
        ///
        /// 为什么是「暴露修正量」而不是在 OnTurnTick 里直接改 owner.Str：
        ///   直接改要么互相覆盖（环境和 buff 各写一次 Str），要么累加、
        ///   然后得在 buff 消失时再加回来 —— 而 buff 有三条移除路径
        ///   （到期 / ClearBuffs / 将来的驱散），漏一条就永久偏移。
        ///   做成修正量之后，Player.RecomputeStr 每次从基础值重算，
        ///   挂上自动生效、消失自动恢复，不需要任何「还原」代码。
        /// </summary>
        public virtual double StrBonus => 0.0;

        /// <summary>
        /// 状态栏「Buff 框」里显示的一行效果说明（不含显示名，名字由调用方拼在前面）。
        ///
        /// 为什么要有它：显示层（Player.RefreshBuffBox）原先自己拼
        ///   Name + ": " + Stacks + "伤害 · 剩余" + RemainingTurns + "回合"
        /// —— 那句里写死的「伤害」假设了**所有 buff 都是每回合造成伤害的**。
        /// 第二个 buff（超雄综合症，加 STR、不扣血）挂上去就显示成
        /// 「超雄综合症: 每回合2147483647伤害」，与事实完全不符。
        ///
        /// 根因是「UI 层知道了一个具体子类的语义」。修法是把这个语义下放给子类：
        /// 显示层只问「你怎么描述你自己」，不知道也不关心 Stacks 的单位是什么。
        /// 这正是 CONVENTIONS 里「通用逻辑集中、各子类只保留自己那套」的同一思路 ——
        /// 以后再加第 10 个 buff（回蓝、加速、反伤……），显示层一行都不用改。
        ///
        /// 默认实现刻意**只说通用事实**（剩余回合），不写任何单位 ——
        /// 基类不该猜子类的效果量是什么。子类若觉得不够显眼，覆写它即可。
        /// </summary>
        public virtual string Describe() => "剩余" + RemainingTurns + "回合";

        /// <summary>
        /// 每回合结算一次。由战斗回合管线（Tools.ApplyBuffs）调用。
        /// 各 buff 只负责**自己那套结算规则**，不负责收尸 —— 与原 DOT 结算的纪律一致：
        /// 死亡统一交给 Tools.RemoveDeadMonsters，否则 buff 击杀 + 技能补刀会双发掉落。
        ///
        /// 注意「每回合」是管线给的时机（每个大回合开场一次），不是「一定会扣血」：
        /// 扣血只是流血这一类的行为，加属性/回血的 buff 走同一个入口。
        /// </summary>
        public abstract void OnTurnTick(Player owner);

        /// <summary>怪物身上的版本（结算规则与玩家侧一致，由各 buff 自己决定要不要区分）。</summary>
        public abstract void OnTurnTick(Monster owner);
    }
}
