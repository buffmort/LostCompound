using System;
using System.Collections.Generic;
using System.Linq;

namespace HelloWorld01.Properties
{
    public abstract class role
    {
        // 阵营：0 = 红方，1 = 蓝方
        public readonly int team;

        // 不可改变：readonly 字段，只能在声明它的类（也就是 role）的构造函数里赋值一次
        public readonly String name;

        protected int Hp { get; set; }
        protected int ATK { get; set; }
        protected int DEF { get; set; }

        protected double cc { get; set; }

        // 盾计数，默认 0
        protected int shield { get; set; }

        // ---- 事件：外部用 += 订阅，用 -= 退订 ----
        // 出手事件：参数 = 这次行动的完整记录（由行动方抛）
        public event Action<DamageRecord> Attacked;
        // 挨打事件：参数 = 这次行动的完整记录（由挨打方抛，可在记录里找到自己）
        public event Action<DamageRecord> Damaged;

        public role(int team, String name, int Hp, int ATK, int DEF ,double cc)
        {
            this.team = team;
            this.name = name;
            this.Hp = Hp;
            this.ATK = ATK;
            this.DEF = DEF;
            this.cc = cc;
            this.shield = 0;
        }

        // 对外只读：外部（Main、UI）能查血量/护盾/存活，但不能直接改
        public int HpNow { get { return Hp; } }
        public int ShieldNow { get { return shield; } }
        public bool IsDead { get { return Hp <= 0; } }

        // 返回本次行动的记录；field 是"整个战场"，敌人由角色自己筛
        public abstract DamageRecord attack(List<role> field, int round);
        public abstract void defend();

        // 从战场里筛出"还活着的敌人"：攻击只会打这些人，从根上杜绝误伤和打尸体
        protected List<role> Enemies(List<role> field)
        {
            return field.Where(r => r.team != team && !r.IsDead).ToList();
        }

        // 受到伤害：先按 DEF 递减减免，再扣盾（盾也吃减免），最后扣血；
        // 结果写进 action 这条记录，然后抛 Damaged 事件
        public void takeDamage(DamageRecord action, int damage)
        {
            if (damage <= 0) return;

            // 收益递减的减免公式：DEF 25 -> 20%，DEF 100 -> 50%，永远到不了 100% 免疫
            double mitigation = DEF / (DEF + 100.0);
            if (mitigation < 0.0) mitigation = 0.0;
            if (mitigation > 1.0) mitigation = 1.0;

            int reduced = (int)Math.Round(damage * (1.0 - mitigation), MidpointRounding.AwayFromZero);

            // 减免后的伤害先由盾承受
            if (shield > 0)
            {
                int absorbed = reduced < shield ? reduced : shield;
                shield -= absorbed;
                reduced -= absorbed;
            }

            // 剩下的才扣血
            int hpLost = 0;
            if (reduced > 0)
            {
                hpLost = reduced > Hp ? Hp : reduced;   // 实际掉血不超过当前血量
                Hp -= hpLost;
            }

            action.AddTarget(this, hpLost, Hp);

            if (Damaged != null) Damaged(action);
        }

        // 供子类触发出手事件：C# 的 event 只能在声明它的类内部触发，
        // 子类里写 Attacked(...) 会报 CS0070，所以必须开这个 protected 方法
        protected void OnAttacked(DamageRecord action)
        {
            if (Attacked != null) Attacked(action);
        }

        // 返回本次行动的记录：出手就是攻击记录，防御则是一条 kind = Defend 的记录
        public abstract DamageRecord onTurn(List<role> field, int round);
    }
}
