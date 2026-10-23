using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Items;
using LostCompound.Monsters;
using LostCompound.Maps;
using LostCompound.UI;
using LostCompound.Buffs;
using LostCompound.Items.Capabilities;
using LostCompound.Items.Materials;
using LostCompound.Items.Consumables;
using LostCompound.Items.Containers;
using LostCompound.Items.Equipment;
using LostCompound.Rooms.Types;
using LostCompound.Tools.Debug;
using LostCompound.Buffs.Types;

namespace LostCompound.Tools.Players
{
    public partial class Player
    {

        public int SkipTurn { get; set; }

        /// <summary>
        /// 累加跳过回合数（与 <see cref="Monsters.Monster.AddSkipTurn"/> 对称，见 CONVENTIONS 2.3）。
        /// 为什么要有这个方法：<c>SkipTurn</c> 是**累加语义**的量（连中两次「困住」就该被跳两回合），
        /// 而它原先只由调用点裸写 <c>p.SkipTurn = 1</c> —— 那种写法是**赋值**：连中两次也只剩 1，
        /// 而且一旦将来第二个来源也来写，两边会互相覆盖。出口统一成 AddXxx 之后，
        /// 「谁改过它」不再重要，值永远等于各方增量之和。
        /// 战斗结束时由 Tools.RunBattle 的 finally 清零（跨战斗残留是另一个 bug，见那里）。
        /// </summary>
        public void AddSkipTurn(int count) => SkipTurn += count;

        // ==================== 状态效果（buff，如流血） ====================
        // 原先这里的 DotDamage / DotDuration 字段与 AddDotDamage / AddDotDuration
        // 已按你的要求整体删除（DOT 机制改为流血 buff），周期性伤害只走下面这条出口。
        /// <summary>挂在玩家身上的状态效果。结算时机见 Tools.ApplyBuffs（大回合开场）。</summary>
        public List<Buff> Buffs { get; } = [];

        /// <summary>
        /// 给玩家挂一个状态效果。
        /// 同类 buff **叠加到已有对象上**，而不是挂两个同名对象：理由
        ///   1. 「流血 10 点」是一个状态，不是两个状态；分开放会让上限 10 的判断散成多处；
        ///   2. 显示层（OnPlayerTurn 里的状态列表）不需要再去重；
        ///   3. 判断「同类」用运行时类型（GetType()）而不是 Name：Name 是给人看的文本，
        ///      将来完全可能出现两个同名但结算不同的 buff，用类型判断更严格。
        /// 叠加规则（伤害取 min、剩余回合相加）写在 Buff.MergeFrom 里，玩家/怪物共用一份。
        /// </summary>
        public void AddBuff(Buff b)
        {
            if (b == null) return;
            foreach (Buff exist in Buffs)
            {
                if (exist.GetType() == b.GetType())
                {
                    exist.MergeFrom(b);
                    RecomputeStats();      // buff 可能改属性，立刻重算
                    RefreshBuffBox();      // 叠加会改伤害/剩余回合，框要立刻跟上
                    return;
                }
            }
            Buffs.Add(b);
            RecomputeStats();              // buff 可能改属性，立刻重算
            RefreshBuffBox();
        }

        /// <summary>跳过回合：返回"这次是否被跳过"，顺手扣一次计数。
        /// 与 Monster.TryConsumeSkipTurn 对称 —— 由战斗循环统一调用。</summary>
        public bool TryConsumeSkipTurn()
        {
            if (SkipTurn <= 0) return false;
            SkipTurn--;
            Console.WriteLine("你被跳过回合！");
            return true;
        }

        /// <summary>
        /// 回合开始的处理。战斗循环每回合先调用它，再做跳过检查 ——
        /// 被跳过的回合里，道具计时、待入包合并、状态栏刷新仍然要照常执行。
        /// </summary>
        public void OnTurnStart(EnvType envType)
        {
            Console.WriteLine("=====你的回合=====");

            // 护盾是「本回合」的资源，不跨回合累积 —— 回合一开始先清空，
            // 再由装备被动（盾牌 +5）和本回合的「防御」动作重新攒。
            // 放在 RefreshStatus 之前，状态栏显示的才是重置后的值，而不是上回合残留。
            ResetShieldForTurn();

            // 环境状态（水量、水位）每回合都在变，所以每回合都要重算派生值，
            // 不能只在进战斗时算一次 —— 否则缺水削弱要等到下一场战斗才生效。
            //
            // ⚠️ RecomputeStats 必须在 RefreshStatus **之前**（本次修掉的一个真 bug）：
            //   本回合的环境推进（Tools 里的 env?.OnBattleTurn()）在 OnTurnStart 之前就已经跑完了，
            //   但原先的顺序是 ResetShieldForTurn → RefreshStatus → RecomputeStats ——
            //   状态栏读到的还是**上一回合**的力量/防御，于是熔岩瀑布每回合掉水后
            //   「力量」落后一跳（0.05~0.10），要等玩家挨打触发 TakeDamage 里的刷新才被覆盖。
            //   把重算提到刷新之前，状态栏这一跳就即时了。
            //   （ResetShieldForTurn 仍留在最前：盾是资源、必须最先重置；
            //     RefreshBuffBox 仍在其后 —— 它只画 buff 框，与属性快照无关。）
            RecomputeStats();
            RefreshStatus();               // 战斗中让状态栏显示血量/防御/护盾
            RefreshBuffBox();              // Buff 框：进新回合时剩余回合已经 -1，必须重画
            HandleChangeableItems(envType);
            Inventory.AddRange(PendingItems);
            PendingItems.Clear();
            Console.WriteLine("你的血量:{0:F1}", Hp);
            Console.WriteLine("你的防御:{0:F1}", Def);
            Console.WriteLine("你的护盾剩余:" + Shield);
            // 环境状态（如熔岩瀑布水量）：供玩家决定要不要用水瓶
            if (CurrentEnvironment != null)
            {
                Console.WriteLine(CurrentEnvironment.DescribeStatus());
            }
        }

        /// <summary>
        /// 把玩家身上的 buff 喂给右列的「Buff 框」（状态框正下方）。
        ///
        /// 文案由 **buff 自己**决定（<see cref="Buff.Describe"/>），这里只负责加上显示名。
        /// 例：流血 7 点、剩 2 回合 → <c>流血: 每回合7伤害 · 剩余2回合</c>
        ///     超雄综合症          → <c>超雄综合症: 力量 +1000 · 剩余N回合</c>
        ///
        /// 为什么文案下放到子类（这次踩过的坑）：
        ///   这里原先拼的是 <c>b.Name + ": " + b.Stacks + "伤害 · 剩余" + b.RemainingTurns + "回合"</c>，
        ///   其中「伤害」写死了 —— 等于假设「所有 buff 都是每回合造成伤害的」。
        ///   而 Stacks 的语义本来就由各 buff 自己定义（流血 = 每回合伤害，超雄 = 强度）。
        ///   结果第二个 buff 一挂上就显示出「超雄综合症: 每回合2147483647伤害」，它根本不扣血。
        ///
        ///   中途曾改成中性的 <c>"lvl." + b.Stacks</c>，那只是把「错的话」换成「没信息的话」：
        ///   玩家看到「lvl.2147483647」仍然不知道这是什么量。
        ///   根因不是文案不准确，而是**显示层知道了一个具体子类的语义**：
        ///   每当新增一种 buff，这里就得回来加一个 if —— 那正是该用虚方法消除的场合。
        ///   现在显示层只问「你怎么描述你自己」，对 buff 的种类与单位一无所知，
        ///   以后加第 10 个 buff（回蓝、加速、反伤……）本文件一行都不用动。
        ///
        /// 注意不打印「上限 10」这类规则说明：那是设计文档的内容，
        /// 框里只放当前状态，规则说明文字会挤掉真正会变的数字。
        ///
        /// 空 buff 时传空字符串 —— DrawBuffs 会把整框画成空白（与敌人框清空同一口径）。
        /// </summary>
        private void RefreshBuffBox()
        {
            if (_screen == null) return;

            List<string> lines = [];
            for (int i = 0; i < Buffs.Count && i < ConsoleScreen.BuffRows; i++)
            {
                Buff b = Buffs[i];
                lines.Add(b.Name + ": " + b.Describe());
            }
            _screen.DrawBuffs(string.Join("\n", lines));
        }
    }
}
