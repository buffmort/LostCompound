using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Buffs
{
    /// <summary>
    /// 流血（设计文档 3.2 技能 6）。DOT 机制已删除，全部周期性伤害改由本类承担。
    ///
    /// 数值语义（按你的口径，与旧 DOT 不同，务必看清）：
    ///   - Stacks = **每回合扣血数**（不是「层数 × 1 点」的层数概念）。
    ///     例：现在 7 点，再挂一个 5 点 → min(7 + 5, 10) = 10 点，而不是 12 层。
    ///   - Stacks 上限 10，由基类 AddStacks 按 MaxStacks 夹取。
    ///   - RemainingTurns = 剩余回合，每结算一次 -1，归零则该 buff 失效（由 Tools.ApplyBuffs 移除）。
    ///   - 同类叠加：伤害取 min(现有 + 新增, 10)，剩余回合取两者之和
    ///     （相加是为了保持旧 DOT 的 AddDotDuration 就是 += 累加的语义）。
    ///
    /// 例（你给的验收例子）：现在 7 点、剩 2 回合，再挂「5 点、3 回合」
    ///   → 伤害 min(7+5,10) = 10；剩余回合 2 + 3 = 5。
    ///
    /// 为什么伤害走 TakeDamage 而不是直接 Hp -= X：这是上一轮定下的口径，保持不变 ——
    /// 护盾吸收、防御减免、状态栏刷新都在 TakeDamage 里，绕开它就会出现
    /// 「同样是每回合扣血、行为却不一致」的静默偏差。
    ///
    /// 待确认项：困难模式的「每回合扣 2」需要难度系统，目前项目还没有难度概念，
    ///   所以这里只按存进来的伤害值结算，困难难度做出来后按难度对伤害乘 2。
    /// </summary>
    public sealed class BleedBuff(int damagePerTurn, int turns) : Buff(damagePerTurn, turns)
    {
        public override string Name => "流血";

        /// <summary>本 buff 的 ID（见 <see cref="BuffIds"/>）。</summary>
        public override int Id => BuffIds.Bleed;

        /// <summary>
        /// 流血的显示文案。**「伤害」这个词只能出现在这里** ——
        /// 它是流血的语义，不是所有 buff 的语义，所以由本子类自己声明，
        /// 而不是让显示层（Player.RefreshBuffBox）替所有 buff 写死。
        ///
        /// 时长段：永久（调试 130 传 <see cref="Buff.Permanent"/>）时走
        /// <see cref="Buff.RemainingTurnsText"/> 显示「永久」，不再把哨兵 -1 印成「剩余-1」。
        /// ⚠️ 非永久分支**原样保留**这里的 <c>"剩余" + RemainingTurns</c> 写法（不带「回合」）：
        ///   这一处历来就没有单位（与基类默认的「剩余N回合」不同），属于既有文案，
        ///   不属于本次改动，**逐字不变**（见报告里的待确认项）。
        /// </summary>
        public override string Describe() =>
            "每回合" + Stacks + "伤害 · 剩余" + (IsPermanent ? RemainingTurnsText : RemainingTurns.ToString());

        /// <summary>每回合伤害上限 10（设计文档 3.2 技能 6 的「上限 10」）。</summary>
        public override int MaxStacks => MaxDamagePerTurn;

        /// <summary>每回合伤害上限。</summary>
        public const int MaxDamagePerTurn = 10;

        /// <summary>
        /// 本次结算的扣血量 = 每回合伤害。
        /// 保留这个属性名是为了让调用处读起来仍是「这一跳扣多少」，而不是「效果量」。
        /// </summary>
        public int TickDamage => Stacks;

        /// <summary>
        /// 玩家身上的结算。走 TakeDamage 的原因见类注释。
        /// 顺序：先扣血、再走剩余回合 -1 —— 这样最后 1 回合也会真的结算一次。
        /// </summary>
        public override void OnTurnTick(Player owner)
        {
            if (IsExpired) return;
            Console.WriteLine(" 你受到 " + TickDamage + " 点流血伤害(流血 x" + Stacks + ")");
            owner.TakeDamage(TickDamage);
            ConsumeTurn();
        }

        /// <summary>怪物身上的结算（白磷/红磷/磷化剑挂上去的就是这个）。</summary>
        public override void OnTurnTick(Monster owner)
        {
            if (IsExpired) return;
            Console.WriteLine(owner.Name + " 受到 " + TickDamage + " 点流血伤害(流血 x" + Stacks + ")");
            owner.TakeDamage(TickDamage);
            ConsumeTurn();
        }
    }
}
