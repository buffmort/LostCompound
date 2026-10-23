using LostCompound.Tools;
using LostCompound.Monsters;

using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    public sealed class Zombie : Monster
    {
        /// <summary>
        /// 档位：**普通**。
        ///
        /// ⚠️ 这里是本次唯一「文档没有直接对应」的一只，理由与保留意见都写下来供复核：
        ///   · 文档 §6 的表里标「精英」的是**洞潜僵尸**（100 血、每回合 10% 自动死亡、会上 4 种 debuff），
        ///     而 §12.4 已经记着「代码里的 Zombie 是 30 血，与文档的洞潜僵尸不是同一只」；
        ///     §6.2「普通怪物（全地图可出现）」里另有一只只有血量/掉落的**僵尸**（30 血）。
        ///   · 本类的 30 血正好对上 §6.2 那只「僵尸」，也与 §12.4 的记载一致，
        ///     所以按「§6.2 普通怪物」判为 **Normal**。
        ///   · 若用户认为代码里的 Zombie 就是要当**洞潜僵尸**（精英）用，
        ///     那么这一行改成 <c>MonsterRank.Elite</c> 即可，毒气弹的判定会自动跟着走
        ///     （唯一受影响的是「毒气弹 10% 秒杀」对僵尸是否生效）。
        /// </summary>
        public override MonsterRank Rank => MonsterRank.Normal;

        public Zombie()
        {
            Atk = 20;
            Str = 0.5;
            Def = 0;
            Hp = 30;
            Name = "僵尸";
        }
        public Zombie(double atk, double str, double def, double hp, string name)
        {
            Atk = atk;
            Def = def;
            Str = str;
            Hp = hp;
            Name = name;
        }

        public override void Attack(Player p)
        {
            Console.WriteLine("僵尸咬了你一口!");
            double dmg = Calculator.CalculateAttack(Str, Atk);
            p.TakeDamage(dmg);

        }

        public override void TakeDamage(double dmg)
        {
            // 顺序：先 DEF 减免，再吃盾 —— 骨架在 Monster.TakeDamageWithDefaultLog 里，
            // 本类只提供自己那两句文案（僵尸与假人/岩浆怪用同一套措辞，但各自声明，
            // 避免将来其中一只单独改文案时被另一只连带改掉）。
            TakeDamageWithDefaultLog(dmg);
        }

        /// <summary>护盾吃光整刀时的播报（文案与重构前逐字一致）。</summary>
        protected override void LogShieldAbsorbed()
        {
            Console.WriteLine(Name + "的护盾挡下了伤害！（剩余护盾:" + Shield.ToString("F1") + "）");
        }

        /// <summary>实际扣血时的播报（文案与重构前逐字一致）。</summary>
        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
        }

        public override void OnMonsterTurn(Player p)
        {
            // 回合头、流血结算与跳过检查都由 Tools.RunBattle 统一处理：
            // 这里如果自己再结算一次流血，就会和 Tools.ApplyBuffs 重复结算（原 bug）。
            int option = RollIndex(3);
            switch (option)
            {
                case 0:
                    Attack(p);
                    break;
                case 1:
                    // 原为 Block += 1（次数制，一次全免，太超模）；按用户裁定改为加 5 点护盾值。
                    AddShield(5);
                    Console.WriteLine(Name + " 增加了 5 点护盾！");
                    break;
                case 2:
                    break;
            }
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count);
    }
}
