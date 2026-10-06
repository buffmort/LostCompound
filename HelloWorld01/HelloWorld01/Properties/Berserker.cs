using System;
using System.Collections.Generic;

namespace HelloWorld01.Properties
{
    public class Berserker : role
    {
        // static：整个类共用一个随机数发生器，避免多个实例摇出同一串随机数
        private static readonly System.Random rd = new System.Random();

        public Berserker(int team, String name, int Hp, int ATK, int DEF, double cc)
            : base(team, name, Hp, ATK, DEF, cc)
        {
            
        }

        // 返回行动记录：只打一个人，所以记录里只有一个目标
        public override DamageRecord attack(List<role> field, int round)
        {
            List<role> enemies = Enemies(field);

            int damage = ATK;

            // cc 是 0~1 的暴击率，命中则伤害翻倍
            bool crit = rd.NextDouble() < cc;
            if (crit) damage *= 2;

            DamageRecord action = new DamageRecord(round, this, ActionKind.Attack, damage, crit);

            // 敌人可能在本回合先被打光了
            if (enemies.Count > 0)
            {
                role target = enemies[rd.Next(enemies.Count)];
                target.takeDamage(action, damage);
            }

            OnAttacked(action);
            return action;
        }

        public override void defend()
        {
            this.shield += 20;
            if (this.shield > 35) this.shield = 35;   // 上限必须低于对方单回合爆发，否则破不了盾就僵持
        }

        public override DamageRecord onTurn(List<role> field, int round)
        {
            // 血量低时只有 50% 概率自保，否则照常出手 —— 避免双方一直加盾谁也打不死
            if (this.Hp < 20 && rd.NextDouble() < 0.5)
            {
                defend();
                return new DamageRecord(round, this, ActionKind.Defend, 0, false);
            }
            if (rd.NextDouble() < 0.5)
            {
                defend();
                return new DamageRecord(round, this, ActionKind.Defend, 0, false);
            }

            return attack(field, round);
        }
    }
}
