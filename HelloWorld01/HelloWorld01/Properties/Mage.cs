using System;
using System.Collections.Generic;

namespace HelloWorld01.Properties
{
    public class Mage : role
    {
        // static：整个类共用一个随机数发生器，避免多个实例摇出同一串随机数
        private static readonly Random rd = new Random();

        public Mage(int team, String name, int Hp, int ATK, int DEF,double cc)
            : base(team, name, Hp, ATK, DEF, cc)
        {
        }

        // 返回行动记录：打全场敌人，所以记录里有多个目标
        public override DamageRecord attack(List<role> field, int round)
        {
            int damage = ATK * 2;

            DamageRecord action = new DamageRecord(round, this, ActionKind.Attack, damage, false);

            // Enemies 已经过滤掉死者和队友，这里不用再判断
            foreach (role target in Enemies(field))
            {
                target.takeDamage(action, damage);
            }

            OnAttacked(action);
            return action;
        }

        public override void defend()
        {
            this.shield += 10;
            if (this.shield > 25) this.shield = 25;   // 上限必须低于对方单回合爆发，否则破不了盾就僵持
        }

        public override DamageRecord onTurn(List<role> field, int round)
        {
            // 血量低时只有 50% 概率自保，否则照常出手 —— 避免双方一直加盾谁也打不死
            if (this.Hp < 30 && rd.NextDouble() < 0.5)
            {
                defend();
                return new DamageRecord(round, this, ActionKind.Defend, 0, false);
            }
            if (rd.NextDouble() < 0.6)
            {
                defend();
                return new DamageRecord(round, this, ActionKind.Defend, 0, false);
            }

            return attack(field, round);
        }
    }
}
