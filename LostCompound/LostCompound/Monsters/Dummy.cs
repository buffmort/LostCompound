using LostCompound.Tools;

namespace LostCompound.Monsters
{
    public sealed class Dummy : Monster
    {
        /// <summary>
        /// 档位：**普通**。
        /// 测试假人是调试/训练用的怪（§10 的 train 战斗），文档 §6 的怪物表里**没有**它，
        /// 所以既不是精英也不是 Boss，按普通处理。
        /// </summary>
        public override MonsterRank Rank => MonsterRank.Normal;

        public Dummy()
        {
            Type = MonsterType.Dummy;
            Name = "测试假人";
            Hp = 1000;
            Shield = 0;   // 护盾值（原 Block = 0 的次数制已删除）
        }
        public override void Attack(Player p)
        {
            Console.WriteLine("假人无动于衷...");
        }

        public override void TakeDamage(double dmg)
        {
            // 骨架在 Monster.TakeDamageWithDefaultLog 里（DEF 减免 → 吃盾 → 播报），
            // 本类只提供自己那两句文案。假人 Def 为 0，结算结果与重构前一致。
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
            // 回合头与跳过检查由 Tools.RunBattle 统一处理
            Attack(p);
        }

        protected override int RollIndex(int count) => 0;
    }
}
