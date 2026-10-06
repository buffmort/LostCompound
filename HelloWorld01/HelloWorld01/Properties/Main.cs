using System;
using System.Collections.Generic;
using System.Linq;

namespace HelloWorld01.Properties
{
    public class main
    {
        static void Main(String[] args)
        {
            // 阵营：0 = 红方，1 = 蓝方
            List<role> team1 = new List<role>();
            team1.Add(new Mage(0, "红方法师", 110, 18, 15, 0.3));
            team1.Add(new Berserker(0, "红方狂战", 170, 12, 25, 0.6));

            List<role> team2 = new List<role>();
            team2.Add(new Mage(1, "蓝方法师", 110, 18, 15, 0.3));
            team2.Add(new Berserker(1, "蓝方狂战", 170, 12, 25, 0.6));

            // 战场 = 两队所有人；角色自己从里面筛敌人，调用方不会再传错列表
            List<role> field = new List<role>();
            field.AddRange(team1);
            field.AddRange(team2);

            // 想战斗中实时输出就订阅事件；不订阅则完全静音，只有战后的汇总
            // foreach (role r in field) r.Attacked += rec => Console.WriteLine(rec);

            // 所有回合的记录都攒到这里，战斗中不打印
            List<List<DamageRecord>> history = new List<List<DamageRecord>>();

            int round = 1;

            // 双方都还有活人时继续打
            while (Alive(team1).Count > 0 && Alive(team2).Count > 0)
            {
                // ---- 每回合：每队每个活着的角色各行动一次 ----
                List<DamageRecord> records = new List<DamageRecord>();
                foreach (role r in Alive(team1)) records.Add(r.onTurn(field, round));
                foreach (role r in Alive(team2)) records.Add(r.onTurn(field, round));

                // 只存不打印
                history.Add(records);

                round++;

                if (round > 1000)   // 保险丝：防止某些配置下打不完
                    break;
            }

            // ================= 战斗结束，统一循环打印 =================
            Console.WriteLine();
            Console.WriteLine("=== 战斗结束（共 {0} 回合）===", history.Count);

            // 胜负判定：看两边还剩几个活人
            bool t1Alive = Alive(team1).Count > 0;
            bool t2Alive = Alive(team2).Count > 0;

            if (t1Alive && !t2Alive)
                Console.WriteLine(">>> 红方（team1）获胜！");
            else if (t2Alive && !t1Alive)
                Console.WriteLine(">>> 蓝方（team2）获胜！");
            else if (!t1Alive && !t2Alive)
                Console.WriteLine(">>> 同归于尽，平局！");
            else
                Console.WriteLine(">>> 达到回合上限，双方均未全灭，平局！");

            // 1. 逐回合打印该回合的行动记录
            for (int i = 0; i < history.Count; i++)
            {
                List<DamageRecord> records = history[i];

                Console.WriteLine();
                Console.WriteLine("--- 第 {0} 回合：全场总伤害 {1} ---",
                    i + 1, records.Sum(x => x.TotalDamage));

                foreach (DamageRecord rec in records)
                {
                    Console.WriteLine("  " + rec);
                }
            }

            // 2. 整场战斗的总结
            List<DamageRecord> all = history.SelectMany(x => x).ToList();

            Console.WriteLine();
            Console.WriteLine("=== 全场总结 ===");
            Console.WriteLine("总伤害：{0}（实际掉血 {1}）",
                all.Sum(x => x.TotalDamage), all.Sum(x => x.HpLost.Sum(y => y)));

            Console.WriteLine("--- 单个角色总伤害 ---");
            PrintPerCharacter(all);

            Console.WriteLine("--- 最终战况 ---");
            PrintTeam(team1);
            PrintTeam(team2);
        }

        // 取出还活着的角色（LINQ Where）
        static List<role> Alive(List<role> team)
        {
            return team.Where(r => !r.IsDead).ToList();
        }

        static void PrintTeam(List<role> team)
        {
            Console.WriteLine("{0}：{1}",
                team.Count > 0 ? team[0].name + " 队" : "空队",
                string.Join("、", team.Select(r =>
                    r.name + "(" + r.HpNow + ")" + (r.IsDead ? "[阵亡]" : "")).ToArray()));
        }

        // 按 damager（现在是对象引用，不是名字）分组统计
        static void PrintPerCharacter(List<DamageRecord> records)
        {
            var rows = records
                .GroupBy(x => x.damager)
                .Select(grp => new
                {
                    Name = grp.Key.name,
                    Team = grp.Key.team == 0 ? "红" : "蓝",
                    Total = grp.Sum(x => x.TotalDamage),
                    Attacks = grp.Count(x => x.kind == ActionKind.Attack),
                    Defends = grp.Count(x => x.kind == ActionKind.Defend),
                    Hits = grp.Sum(x => x.Targets.Count)
                })
                .OrderByDescending(x => x.Total);

            foreach (var row in rows)
            {
                Console.WriteLine("{0}[{1}]：总伤害 {2}，出手 {3} 次，防御 {4} 次，命中 {5} 人次",
                    row.Name, row.Team, row.Total, row.Attacks, row.Defends, row.Hits);
            }
        }
    }
}
