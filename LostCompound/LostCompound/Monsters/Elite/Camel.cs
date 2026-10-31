using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Elite
{
    /// <summary>
    /// 穿模骆驼（DESIGN §6「地图 3 沙漠」，**精英**）。
    ///
    /// 文档原文：「50 血；20%（困难 30%）穿模（跳过玩家回合）；Hard 前两回合必跳过；ATK = 5」。
    ///
    /// 三条落地口径：
    ///   ① **ATK = 5 是固定伤害**，不走 `Calculator.CalculateAttack`（文档给的就是一个数；
    ///      公式在 STR=0 时会算出 2，对不上）。走 <see cref="Player.TakeDamage"/>，
    ///      照常过 DEF 与护盾 —— 与食人鱼同一口径。
    ///   ② **穿模 = 让玩家跳过下一回合**：`p.AddSkipTurn(1)`，由战斗循环的
    ///      `TryConsumeSkipTurn` 消费（与热硫/压缩沙瓶跳过怪物是同一套机制）。
    ///      穿模的这一回合**不攻击**（它把玩家卡住了，不是顺便再踹一脚）。
    ///   ③ **困难模式的 30% 与「Hard 前两回合必跳过」还没做**（难度系统未做）。
    /// </summary>
    public sealed class Camel : Monster
    {
        /// <summary>血量（文档：50）。</summary>
        public const double MaxHp = 50;

        /// <summary>穿模概率（文档：20%，困难 30%）。</summary>
        public const double PhaseThroughChance = 0.2;

        /// <summary>每次造成的固定伤害（文档：ATK = 5）。</summary>
        public const double BiteDamage = 5;

        public Camel()
        {
            Type = MonsterType.Camel;
            Name = "穿模骆驼";
            Hp = MaxHp;
            Atk = 0;      // 伤害是固定值，见类注释①
            Str = 0;
            Def = 0;
            Shield = 0;
            SkipTurn = 0;
        }

        public override MonsterRank Rank => MonsterRank.Elite;

        public override void Attack(Player p)
        {
            Console.WriteLine("穿模骆驼踢了你一脚!");
            p.TakeDamage(BiteDamage);
        }

        public override void TakeDamage(double dmg)
        {
            TakeDamageWithDefaultLog(dmg);
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count);

        public override void OnMonsterTurn(Player p)
        {
            if (TryConsumeSkipTurn())
            {
                Console.WriteLine(Name + " 的回合被跳过了。");
                return;
            }

            // 穿模：它从模型里穿过去，玩家这一回合没法动
            if (Random.Shared.NextDouble() < PhaseThroughChance)
            {
                Console.WriteLine("穿模骆驼从你的模型里穿了过去，你这一回合动不了！");
                p.AddSkipTurn(1);
                return;
            }

            Attack(p);
        }
    }
}
