using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

namespace LostCompound.Items.Containers
{
    /// <summary>
    /// 热硫罐（ID 19，容器，堆叠 10）：<b>继承热硫（ID 4）的全部效果，只重写「消失」</b>
    /// —— 装进罐子里的热硫不会在 3 回合后自行消失。
    ///
    /// 文档依据：DESIGN 第九章「19 热硫罐 容器 10 热硫 + 玻璃罐 | 继承 4，重写消失 → 不消失」。
    ///
    /// 与 <see cref="FushionSluphur"/> 的差异（只有这一条，其余逐字对齐）：
    ///   · 热硫：<c>Timer = 3</c>，靠 <see cref="ITurnCount.OnBattleStart"/> 每场战斗开头 -1，
    ///     <see cref="ShouldRemove"/> 判 <c>Timer &lt;= 0</c> → 到点被移出背包（还会每回合播报剩余回合）；
    ///     ⚠️ 所谓「跨战斗重置」的真相：Timer 是**实例字段**，而道具是玩家背包里的**长驻对象**
    ///     （Player 跨战斗复用），战斗结束时并不会重建，所以 Timer 会一直往下掉，
    ///     只是靠「每场战斗开头扣一次」的节奏让它在第 3 场战斗后消失。
    ///   · 热硫罐：**不实现 ITurnCount、没有 Timer**，ShouldRemove 只判 <c>Count == 0</c>
    ///     —— 即「只有用光才消失」，与容器/消耗品的常规口径（Metal / WhitePhosphorus）一致。
    ///
    /// 为什么另建一个类而不是给 FushionSluphur 加个 bool 开关：
    ///   两者 ID / 名称 / 堆叠 / 配方 / 生命周期都不同，做成开关会让「热硫在第 3 回合消失」
    ///   这条规则散成两处（开关 + Timer 初值），而按 CONVENTIONS 2.4 每个道具一个类更直白。
    ///   Use 里的效果确实是重复的 —— 如果将来出现第三种「热硫系」道具，就该把这段提到公共基类。
    ///
    /// 必须同时实现 IChangeable：<see cref="Use"/> 里会 <c>Count -= 1</c>，
    /// 归零后不移出背包就会留下一个「热硫罐 x0」永远占着背包格（Metal 上踩过的坑）。
    /// </summary>
    public sealed class HeatSulphurCan(int count) : Item(ItemIds.HeatSulphurCan, "热硫罐", 10, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.HeatSulphurCan;

        /// <summary>
        /// 效果与 <see cref="FushionSluphur.Use"/> 逐字一致：选一只怪跳过 1 回合 + 全体 10 点伤害。
        /// 数值沿用热硫的实现（文档第九章写的是「跳过敌人 1 回合 + 20 伤害」，
        /// 代码是 AOE 10 —— 这是既有的文档/实现缺口，本次不顺手改，保持「继承 4」的语义）。
        /// </summary>
        public void Use(Player p, List<Monster> m)
        {
            Console.WriteLine("====你使用了热硫罐!====");
            Calculator.Delay(250);
            int choice = p.SelectTarget(m);
            if (choice == -1)
            {
                Console.WriteLine("你输入了错误的对象代号!");
                return;
            }
            Monster target = m[choice];
            target.AddSkipTurn(1);
            Console.WriteLine("你给" + target + "造成了眩晕 他跳过了一回合!");
            foreach (Monster monster in m)
            {
                monster.TakeDamage(10);
            }
            Count -= 1;

            // 环境额外效果也一起继承：热硫罐同样是热硫，在水淹洞穴里照样能堵出水口。
            // 与热硫一样只通知环境，具体规则（堵几回合）归环境自己决定。
            p.CurrentEnvironment?.OnFusionSulphurUsed();
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("热硫罐只能在战斗中使用！");
        }

        /// <summary>只有用光才消失 —— 容器不会像裸装热硫那样 3 回合后过期。</summary>
        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}
