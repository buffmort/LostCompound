using System.Collections.Generic;

namespace HelloWorld01.Properties
{
    // 一次行动的类型：出手，还是防御
    public enum ActionKind { Attack, Defend }

    // 一次行动的记录：谁、第几回合、干了什么、打到了谁、每人掉了多少血
    public class DamageRecord
    {
        public readonly int round;
        public readonly role damager;      // 行动方（对象引用，不再是名字）
        public readonly ActionKind kind;
        public readonly int damage;        // 对每个目标的伤害（AOE 时所有目标相同）
        public readonly bool crit;

        private readonly List<role> targets = new List<role>();
        private readonly List<int> hpLost = new List<int>();
        private readonly List<int> hpAfter = new List<int>();

        public DamageRecord(int round, role damager, ActionKind kind, int damage, bool crit)
        {
            this.round = round;
            this.damager = damager;
            this.kind = kind;
            this.damage = damage;
            this.crit = crit;
        }

        // 只给 role.takeDamage 用：把"谁被打了、掉多少、剩多少"追加进来
        internal void AddTarget(role target, int lost, int remain)
        {
            targets.Add(target);
            hpLost.Add(lost);
            hpAfter.Add(remain);
        }

        public List<role> Targets { get { return targets; } }
        public List<int> HpLost { get { return hpLost; } }
        public List<int> HpAfter { get { return hpAfter; } }

        // 本次行动打出的总伤害：没打到人就按 0 算
        public int TotalDamage { get { return damage * targets.Count; } }

        public override string ToString()
        {
            if (kind == ActionKind.Defend)
                return string.Format("[第 {0} 回合] {1} 防御", round, damager.name);

            if (targets.Count == 0)
                return string.Format("[第 {0} 回合] {1} 出手，但场上已无目标", round, damager.name);

            string[] parts = new string[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                parts[i] = string.Format("{0}(掉血 {1}，剩余 {2})",
                    targets[i].name, hpLost[i], hpAfter[i]);
            }

            return string.Format("[第 {0} 回合] {1} 攻击：{2} 点伤害{3} -> {4}",
                round, damager.name, damage, crit ? "（暴击）" : "", string.Join("、", parts));
        }
    }
}
