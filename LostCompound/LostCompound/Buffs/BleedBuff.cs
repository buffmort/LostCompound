using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Buffs
{
    /// <summary>
    /// 流血（设计文档 3.2 技能 6）：每回合扣 1（困难 2）血，可叠加，上限 10 层。
    ///
    /// 层数语义：N 层每回合扣 N 点（文档「每回合扣 1 血 + 可叠加」的自然读法）。
    ///
    /// 层数是否自动衰减：**不衰减**。理由：
    ///   1. 文档只给了上限（10 层）和减层手段（万能药减 4 层），没写「每回合掉层」；
    ///   2. 现有 DOT 是按「时长」消失的（DotDuration 每回合 -1），而流血只有层数、没有时长，
    ///      文档也没给流血时长 —— 若让它每回合自动 -1 层，等于把层数当成了时长，与上限 10 层并存会自相矛盾
    ///      （那样 10 层也最多只疼 10 回合，且永远叠不上去）。
    ///   3. 于是：层数只增不减，直到战斗结束；想让它逐层衰减请告诉我，我再改。
    /// 待确认项：困难模式的「每回合扣 2」需要难度系统，目前项目还没有难度概念，
    ///   所以 DamagePerStack 固定为 1，困难难度做出来后按难度取 2。
    /// </summary>
    public sealed class BleedBuff(int stacks) : Buff(stacks)
    {
        public override string Name => "流血";

        /// <summary>上限 10 层（设计文档 3.2 技能 6）。</summary>
        public override int MaxStacks => 10;

        /// <summary>每层每回合造成的伤害。普通难度 1，困难难度 2（难度系统未实现）。</summary>
        public const int DamagePerStack = 1;

        /// <summary>本次结算的扣血量 = 层数 × 每层伤害。</summary>
        public int TickDamage => Stacks * DamagePerStack;

        /// <summary>
        /// 玩家身上的结算。走 TakeDamage 的原因与 ApplyDotEffects 一致：
        /// 统一出口（格挡消耗、防御减免、状态栏刷新都在 TakeDamage 里）。
        /// </summary>
        public override void OnTurnTick(Player owner)
        {
            if (Stacks <= 0) return;
            Console.WriteLine(" 你受到 " + TickDamage + " 点流血伤害(流血 x" + Stacks + ")");
            owner.TakeDamage(TickDamage);
        }

        /// <summary>怪物身上的结算（磷化剑命中时挂上去的就是这个）。</summary>
        public override void OnTurnTick(Monster owner)
        {
            if (Stacks <= 0) return;
            Console.WriteLine(owner.Name + " 受到 " + TickDamage + " 点流血伤害(流血 x" + Stacks + ")");
            owner.TakeDamage(TickDamage);
        }
    }
}
