using LostCompound.Items;
using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// 寄生虫（普通怪，6–12 层）—— 背包污染。
    ///
    /// 设计（Hyberty 2026-11-22 设计书）：
    ///   · HP 45、DEF 1、STR 2、ATK 10
    ///   · 每次命中玩家，随机污染背包里 1 个可堆叠物品（材料/消耗品，
    ///     **不污染武器/防具/饰品**）
    ///   · 被污染物品本场战斗不可使用，但**可正常合成**
    ///   · 最多同时污染 3 个，满了之后命中不再新增
    ///   · 战斗结束自动恢复
    ///
    /// 威胁点不是伤害，是**剥夺玩家的解**：带 1 瓶万能药，被污染就等于没带。
    /// 数值上 Basic 只有 8.3，对 def 20 的玩家基本只掏 1 点 —— 杀伤力全在污染上，
    /// 所以设计书要求它「不跳回合，每轮都命中」，也就是它的意图永远是普攻。
    /// </summary>
    public sealed class Parasite : Monster
    {
        private const double BaseHp = 45;
        private const double BaseDef = 1;
        private const double BaseAtk = 10;

        /// <summary>最多同时污染几个道具。</summary>
        private const int MaxContaminated = 3;

        /// <summary>不参与的条目：武器 / 防具 / 饰品（设计书要求）。</summary>
        /// <remarks>
        /// 按「能不能装备」判断而不是按目录判断：<see cref="Capabilities.IEquippable"/>
        /// 就是「这件东西是装备」的权威定义，将来新增一把剑不用回来改这张表。
        /// 不可堆叠的消耗品（压缩沙瓶、镜子）仍然算「消耗品」，可以被污染 ——
        /// 设计书写的是「材料/消耗品」，没排除一次性道具。
        /// </remarks>
        private static bool CanBeContaminated(Item item)
        {
            if (item == null) return false;
            if (item.Contaminated) return false;                    // 已经污染过了
            if (item is Items.Capabilities.IEquippable) return false;      // 武器/防具/饰品
            return true;
        }

        public Parasite()
        {
            Name = "寄生虫";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = 2;
            Def = BaseDef;
            Type = MonsterType.Parasite;
        }

        public override MonsterRank Rank => MonsterRank.Normal;

        /// <summary>
        /// 意图明说「打 N + 污染」——设计书里它的攻击频率是关键信息（每轮都命中），
        /// 玩家据此决定先杀谁。不把「污染」写进意图的话，玩家会以为它只是一只弱怪。
        /// </summary>
        public override string IntentText() => "打 " + Atk.ToString("F0") + " + 污染";

        public override void PlanIntent(Player p) => SetPlannedOption(0);

        public override void Attack(Player p)
        {
            Console.WriteLine("寄生虫弹射过来，细长的口器扎进你的背包缝里！");
            p.TakeDamage(Atk);

            // 玩家被这一下打死就不必再污染了（背包马上就没人背了）
            if (!p.IsAlive) return;

            Contaminate(p);
        }

        public override void OnMonsterTurn(Player p)
        {
            TakePlannedOption(1);
            Attack(p);
        }

        /// <summary>
        /// 随机挑一件还能污染的道具标上 <see cref="Item.Contaminated"/>。
        ///
        /// 为什么标记挂在道具实例上、而不是 Player 侧维护一张名单：
        ///   · 名单要按 ID/实例记，道具被丢掉、合成掉、堆叠合并之后名单会悬空；
        ///   · 标记跟着实例走，捡到新的一件就是干净的，天然正确；
        ///   · 「战斗结束自动恢复」只需 <c>Player.ClearItemContamination()</c> 扫一遍，
        ///     不用反查谁被谁污染过。
        /// </summary>
        private static void Contaminate(Player p)
        {
            List<Item> candidates = [];
            foreach (Item item in p.Inventory)
            {
                if (CanBeContaminated(item)) candidates.Add(item);
            }

            if (candidates.Count == 0)
            {
                // 要么背包是空的，要么能污染的已经到上限了。
                // 不打印「污染失败」这类噪音 —— 玩家看到清单里三个 [被污染] 就懂了。
                return;
            }

            int already = 0;
            foreach (Item item in p.Inventory)
            {
                if (item.Contaminated) already++;
            }
            if (already >= MaxContaminated) return;

            Item victim = candidates[Random.Shared.Next(candidates.Count)];
            victim.Contaminated = true;
            Console.WriteLine("寄生虫在你的背包里产了卵 —— 「" + victim.Name + "」被污染了。");
        }

        /// <summary>
        /// 挨打：只有数值结算，没有额外机制 —— 寄生虫的威胁全在「背包污染」上，
        /// 它自己不抗打、不反弹、不吸收。
        /// </summary>
        public override void TakeDamage(double dmg)
        {
            TakeDamageWithDefaultLog(dmg);
        }

        protected override int RollIndex(int count) => count <= 0 ? 0 : Random.Shared.Next(count);

        protected override void LogShieldAbsorbed()
        {
            Console.WriteLine(Name + "被弹开了！");
        }

        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
        }
    }
}
