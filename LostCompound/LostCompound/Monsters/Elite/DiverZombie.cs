using LostCompound.Buffs;
using LostCompound.Buffs.Types;
using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Elite
{
    /// <summary>
    /// 洞潜僵尸（DESIGN §6「地图 2 水淹洞穴」，**精英**）。
    ///
    /// 数值（文档 + Hyberty补全）：**100 血**、atk 10、str 5、def 2。
    /// 机制（文档原文）：「每回合 10% 自动死亡；50%（困难 70%）上 4 种 debuff 之一」。
    ///
    /// 每回合只做一件事，按顺序判定：
    ///   ① **10% 自动死亡** —— 自己归零（体力耗尽），由 RemoveDeadMonsters 统一收尸；
    ///   ② 否则 **50%** 给玩家挂 4 种 debuff 里的**随机一种**；
    ///   ③ 都没有发生 → 普通攻击。
    ///
    /// 4 种 debuff 与持续回合（Hyberty裁定：减压病 3 回合、碱中毒 5 回合；
    /// 缺氧文档写 5、搅乱泥沙文档写 1）：
    ///   · 减压病 <see cref="DecompressionSickness"/>：-1 STR / -2 ATK
    ///   · 呼吸性碱中毒 <see cref="RespiratoryAlkalosis"/>：每回合 -2 血
    ///   · 搅乱泥沙 <see cref="MuddyWater"/>：这一回合打不开背包
    ///   · 缺氧 <see cref="Hypoxia"/>：每回合 -0.1 STR，5 回合后恢复
    ///
    /// ⚠️ 困难模式（概率 70%）还没做 —— 难度系统落地时把 <see cref="DebuffChance"/> 改成按难度取值即可。
    /// </summary>
    public sealed class DiverZombie : Monster
    {
        /// <summary>每回合自己猝死的概率（文档：10%）。</summary>
        public const double SelfDeathChance = 0.1;

        /// <summary>每回合挂 debuff 的概率（文档：50%，困难 70%）。</summary>
        public const double DebuffChance = 0.5;

        /// <summary>各 debuff 的持续回合（Hyberty裁定 / 文档）。</summary>
        public const int DecompressionTurns = 3;
        public const int AlkalosisTurns = 5;
        public const int MuddyWaterTurns = 1;
        public const int HypoxiaTurns = 5;

        public DiverZombie()
        {
            Type = MonsterType.DiverZombie;
            Name = "洞潜僵尸";
            Hp = 100;
            Atk = 10;
            Str = 5;
            Def = 2;
            Shield = 0;
            SkipTurn = 0;
        }

        public override MonsterRank Rank => MonsterRank.Elite;

        public override void Attack(Player p)
        {
            Console.WriteLine("洞潜僵尸拽住了你!");
            double dmg = Calculator.CalculateAttack(Str, Atk);
            p.TakeDamage(dmg);
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

            // ① 10% 自己猝死
            if (Random.Shared.NextDouble() < SelfDeathChance)
            {
                Console.WriteLine(Name + "的体力耗尽了，沉了下去。");
                Hp = 0;
                return;
            }

            // ② 50% 挂一种 debuff
            if (Random.Shared.NextDouble() < DebuffChance)
            {
                ApplyRandomDebuff(p);
                return;
            }

            // ③ 否则普通攻击
            Attack(p);
        }

        /// <summary>从 4 种 debuff 里随机挂一种（同类叠加由 Player.AddBuff 的 MergeFrom 处理）。</summary>
        private void ApplyRandomDebuff(Player p)
        {
            int roll = Random.Shared.Next(4);
            switch (roll)
            {
                case 0:
                    p.AddBuff(new DecompressionSickness(1, DecompressionTurns));
                    Console.WriteLine("你得了减压病！STR-1 ATK-2（" + DecompressionTurns + " 回合）");
                    break;
                case 1:
                    p.AddBuff(new RespiratoryAlkalosis(1, AlkalosisTurns));
                    Console.WriteLine("你呼吸性碱中毒了！每回合 -2 血（" + AlkalosisTurns + " 回合）");
                    break;
                case 2:
                    p.AddBuff(new MuddyWater(1, MuddyWaterTurns));
                    Console.WriteLine("泥沙搅了起来，你这一回合打不开背包！");
                    break;
                default:
                    p.AddBuff(new Hypoxia(1, HypoxiaTurns));
                    Console.WriteLine("你缺氧了！每回合 -0.1 STR（" + HypoxiaTurns + " 回合）");
                    break;
            }
        }
    }
}
