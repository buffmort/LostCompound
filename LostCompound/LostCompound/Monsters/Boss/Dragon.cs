using LostCompound.Tools;
using LostCompound.Monsters;

using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Boss
{
    public sealed class Dragon : Monster
    {
        /// <summary>
        /// 档位：**Boss**。
        ///
        /// 依据：设计文档把龙写在 **§6.1 Boss** 这一节里（「LavaWaterfall —— 1. Dragon 龙」），
        /// 所以取 Boss 而不是 Elite。
        /// ⚠️ 保留意见（供复核）：§6.1 的小标题是「Boss」，但**正文没有逐字再说一遍「龙是 Boss」**；
        ///   用户裁定里对这一只也留了问号（「Dragon=精英还是Boss?」）。本次按「§6.1 就是 Boss 节」判为 Boss。
        ///   若用户裁定龙是「精英」，把这一行改成 <c>MonsterRank.Elite</c> 即可 ——
        ///   唯一受影响的是「毒气弹 10% 秒杀非 Boss」是否对龙生效。
        /// </summary>
        public override MonsterRank Rank => MonsterRank.Boss;

        // 龙的护盾上限（原为 MaxBlock = 1 的「格挡次数上限」）。
        // 数值理由：旧机制下 1 层格挡相当于「完全免疫任意一击」，按 DESIGN 龙单次最多挨
        // 15.42 点（玩家满水攻击力）折算，保留 15 点盾最接近原来的生存时长，
        // 又不会像旧机制那样连 999 点的大招都白吃。每次发动 +5 盾（与僵尸同档）。
        private const double MaxShield = 15;
        private const double ShieldPerTurn = 5;

        public Dragon()
        {
            // 龙的基础属性：防御力 5，生命值高一些，攻击力等可以按需调整
            Type = MonsterType.Dragon;

            Atk = 10;      // 普通攻击力（可能不会被使用，因为我们会直接固定伤害）
            Str = 0.0;     // 力量修正（若使用 Calculator.CalculateAttack 时需要）
            Def = 5;       // 防御力 5
            Hp = 80;       // 龙的生命值
            Name = "龙";
        }

        // 可选的带参数构造器，方便扩展
        public Dragon(double atk, double str, double def, double hp, string name)
        {
            Atk = atk;
            Str = str;
            Def = def;
            Hp = hp;
            Name = name;
        }

        public override void Attack(Player p)
        {
            // 默认普通攻击：造成 10 点伤害（对应第二种行动）
            Console.WriteLine(Name + "喷出烈焰！");
            p.TakeDamage(10);
        }

        public override void TakeDamage(double dmg)
        {
            // 先 DEF 减免，再吃盾，最后播报 —— 骨架在 Monster.TakeDamageWithDefaultLog 里，
            // 本类只提供自己那两句文案（龙的「鳞甲」措辞，其余怪物不一样）。
            TakeDamageWithDefaultLog(dmg);
        }

        /// <summary>护盾吃光整刀时的播报（文案与重构前逐字一致）。</summary>
        protected override void LogShieldAbsorbed()
        {
            // 全部被鳞甲护盾吸收：血量没掉，所以不播报「受到伤害」
            Console.WriteLine(Name + "用鳞甲挡下了攻击！（剩余护盾:" + Shield.ToString("F1") + "）");
        }

        /// <summary>实际扣血时的播报（文案与重构前逐字一致，含 {0}/{1:F1} 的写法与空格）。</summary>
        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine("{0}受到 {1:F1} 点伤害！", Name, hpLoss);
        }

        public override void OnMonsterTurn(Player p)
        {
            // 回合头与跳过检查都移到 Tools.RunBattle 里统一处理了
            // （原先这里把回合头打了两遍，且跳过文案与其它怪物不一致）。
            int option = RollIndex(3); // 随机 0,1,2

            switch (option)
            {
                case 0:
                    // 第一种：增加护盾（上限 15 点），并确保防御力为 5（构造时已设置）。
                    // 原为「格挡 +1（上限 1）」的次数制，按本次改动改为护盾值制。
                    if (Shield < MaxShield)
                    {
                        AddShield(ShieldPerTurn);
                        Console.WriteLine(Name + "蜷起身躯，鳞甲闪烁（护盾 +" + ShieldPerTurn + "）！");
                    }
                    else
                    {
                        Console.WriteLine(Name + "的鳞甲已经足够坚硬！");
                    }
                    // 防御力已经是 5，无需额外修改；如果要动态改变可以在这里设置 Def = 5
                    break;

                case 1:
                    // 第二种：攻击一次，造成 10 点伤害
                    Attack(p);
                    break;

                case 2:
                    // 第三种：攻击三次，每次造成 3 点伤害
                    Console.WriteLine(Name + "疯狂撕咬三次！");
                    for (int i = 0; i < 3; i++)
                    {
                        p.TakeDamage(3);
                    }
                    break;
            }
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count);
    }
}
