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
        /// **行为计数**：本回合玩家能做几次行动。默认 1，2 = 「城市双动」式的两次行动。
        ///
        /// Hyberty 2026-11-22 的裁定：这是一个**独立的、可配置的玩法开关**，
        /// 与 <see cref="Buffs.Types.ExtraTurn"/>（额外回合）**共存**、互不覆盖：
        ///   · 行为计数决定「这一回合的玩家行动那一步里跑几次」（同一份行动菜单重复 N 次）；
        ///   · 额外回合是清单里**另一步**，在玩家行动之后按 buff 层数再加几次。
        /// 所以「行为计数 2 + 额外回合 1」= 这一回合玩家一共动 3 次。
        ///
        /// 为什么不做成 buff：额外回合本来就有 buff，但两者的时机不同 ——
        /// 额外回合要排在「玩家行动」**之后**（它是在正常行动之外的追加），
        /// 行为计数是「玩家行动」这一步**自己内部**的重复。混成一个 buff 的话，
        /// 「哪一次算追加」就说不清了。
        ///
        /// 谁改它：目前只有调试指令（132）与将来的城市环境。
        /// **改了就要负责改回 1** —— 它不是 buff，不会随战斗结束自动还原；
        /// 一个只让玩家双动两回合的环境，必须在效果结束时自己 Set 回 1。
        /// </summary>
        public int ActionCount
        {
            get => _actionCount;
            // 夹到 >= 1：0 或负数会让玩家整回合无法行动，那不是「少动一次」而是卡死。
            set => _actionCount = Math.Max(1, value);
        }

        private int _actionCount = 1;

        /// <summary>
        /// 累加跳过回合数（与 <see cref="Monsters.Monster.AddSkipTurn"/> 对称，见 CONVENTIONS 2.3）。
        /// 为什么要有这个方法：<c>SkipTurn</c> 是**累加语义**的量（连中两次「困住」就该被跳两回合），
        /// 而它原先只由调用点裸写 <c>p.SkipTurn = 1</c> —— 那种写法是**赋值**：连中两次也只剩 1，
        /// 而且一旦将来第二个来源也来写，两边会互相覆盖。出口统一成 AddXxx 之后，
        /// 「谁改过它」不再重要，值永远等于各方增量之和。
        /// 战斗结束时由 Tools.RunBattle 的 finally 清零（跨战斗残留是另一个 bug，见那里）。
        /// </summary>
        public void AddSkipTurn(int count) => SkipTurn += count;

        /// <summary>
        /// 本回合是否被跳过（眩晕 / 被困住 / 压缩沙瓶）。
        ///
        /// 由 <see cref="OnTurnStart"/> 在回合开始时消费一次并记录，
        /// 之后同一回合里的**每一次玩家行动**都读它 ——
        /// 这样「城市双动」被眩晕时是整回合都不能动，而不是少动一次。
        ///
        /// 为什么做成 Player 上的属性，而不是在回合清单里用一个局部变量往下传：
        ///   局部变量要靠闭包捕获取值，等于让「玩家回合开始」那一步和「玩家行动」
        ///   暗地里共享状态 —— 清单上看起来是两步，实际是耦合的。
        ///   状态本来就属于玩家，放在玩家身上最直白。
        /// </summary>
        public bool SkippedThisTurn { get; private set; }

        /// <summary>
        /// 清掉「本回合被跳过」的标记。**只在战斗结束时**调用（见 Tools.RunBattle 的 finally）：
        /// 平时它由每回合的 <see cref="OnTurnStart"/> 覆盖，但战斗结束没有"下一回合"，
        /// 不清的话下一场战斗的第一个判断会读到上一场的残留。
        /// </summary>
        public void ClearSkippedThisTurn() => SkippedThisTurn = false;

        // ==================== 状态效果（buff，如流血） ====================
        // 原先这里的 DotDamage / DotDuration 字段与 AddDotDamage / AddDotDuration
        // 已按你的要求整体删除（DOT 机制改为流血 buff），周期性伤害只走下面这条出口。
        /// <summary>挂在玩家身上的状态效果。结算时机见 Tools.ApplyBuffs（大回合开场）。</summary>
        public List<Buff> Buffs { get; } = [];

        /// <summary>
        /// 战斗结束时清空 buff —— **但保留声明了 <see cref="Buff.SurvivesBattleEnd"/> 的那些**。
        ///
        /// 为什么由 Player 来做而不是让 Tools 直接 `Buffs.Clear()`：
        ///   · 这是玩家自己的状态，清理规则属于它；
        ///   · 更重要的是它能被单独测试 —— `Tools.ClearBuffs` 是 private 且要跑完整场战斗
        ///     才走得到，藏在里面的话"哪些该留、哪些该删"只能靠肉眼读代码。
        ///
        /// 为什么默认全清、跨战斗的要显式声明：见 <see cref="Buff.SurvivesBattleEnd"/> 的注释 ——
        /// 漏掉一次清空，流血就会跟着玩家进每一场战斗，变成每回合无限掉血。
        /// </summary>
        public void ClearBattleBuffs() => Buffs.RemoveAll(b => !b.SurvivesBattleEnd);

        /// <summary>
        /// 解开寄生虫留下的「背包污染」标记（<see cref="Item.Contaminated"/>）。
        ///
        /// 为什么和 <see cref="ClearBattleBuffs"/> 放在一起、由战斗结束统一调用：
        /// 污染的语义就是「本场战斗不可使用」，和其它战斗内状态是同一条生命线 ——
        /// 战斗一结束就该无差别解除。散在各处的调用点（离开房间 / 换层 / 读档）
        /// 一定会漏一个，漏掉的后果是玩家带着一件永久废掉的道具走到最后。
        ///
        /// 之所以还要单独一个方法而不是塞进 RemoveConsumedItems：
        /// RemoveConsumedItems 走的是 IChangeable.ShouldRemove（删道具），
        /// 而这里是**保住道具、只摘标记**，混在一起会让「删」和「改状态」绞成一团。
        /// </summary>
        public void ClearItemContamination()
        {
            foreach (Item item in Inventory)
            {
                item.Contaminated = false;
            }
        }


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
                    NotifyDebuffIfNeeded(exist);
                    return;
                }
            }
            Buffs.Add(b);
            RecomputeStats();              // buff 可能改属性，立刻重算
            RefreshBuffBox();
            NotifyDebuffIfNeeded(b);
        }

        /// <summary>
        /// 中了**负面**状态时往信息框推一条（增益不推 —— 那会把信息框刷满）。
        ///
        /// 文案用叠加**之后**的实际状态（层数与剩余回合），而不是刚 new 出来的那个：
        /// 同类 buff 会走 MergeFrom，「中了 3 层」和最终生效的层数可能不是一回事。
        /// </summary>
        private void NotifyDebuffIfNeeded(Buff b)
        {
            if (b == null || !b.IsDebuff) return;
            NotifyInfo("中了 " + b.Name + "（" + b.Describe() + "）");
        }

        /// <summary>跳过回合：返回"这次是否被跳过"，顺手扣一次计数。
        /// 与 Monster.TryConsumeSkipTurn 对称 —— 由 <see cref="OnTurnStart"/> 每回合调用一次，
        /// 结果记进 <see cref="SkippedThisTurn"/> 供本回合的每一次玩家行动读取。</summary>
        public bool TryConsumeSkipTurn()
        {
            if (SkipTurn <= 0) return false;
            SkipTurn--;
            Console.WriteLine("你被跳过回合！");
            return true;
        }

        /// <summary>
        /// 玩家**一次行动结束**时调用：把机会交给各个 buff 自己判断。
        ///
        /// 返回 true = 有 buff 要求「再动一次」（例如 <see cref="ExtraTurn"/>）。
        /// 具体谁要、扣不扣层，全部由 buff 内部的 <see cref="Buff.OnPlayerActionEnd"/> 决定 ——
        /// 这里只负责问一遍。
        ///
        /// 为什么倒序 + 立刻 break：同一时刻只需要一次额外行动。
        /// 万一将来有两个 buff 都返回 true（比如「额外回合」+「连击」），
        /// 应该由设计决定谁优先，而不是在这里糊里糊涂地叠加两次。
        /// </summary>
        public bool NotifyPlayerActionEnd()
        {
            // 用下标 while 而不是 foreach：回调里可能把 buff 从列表里删掉
            // （ExtraTurn 用光的那一层就是自己删自己），foreach 会抛「集合已修改」。
            // 每次循环条件都重新读 Count，所以删完也不会越界。
            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                if (i >= Buffs.Count) break;             // 上一轮回调删掉了元素，下标已失效
                if (Buffs[i].OnPlayerActionEnd(this)) return true;
            }
            return false;
        }

        /// <summary>把一个 buff 从身上摘掉（供 buff 自己在回调里「用光了就消失」）。</summary>
        public void RemoveBuff(Buff buff)
        {
            if (buff == null) return;
            if (Buffs.Remove(buff)) RefreshBuffBox();
        }

        /// <summary>刷新右列 Buff 框（供 buff 在回调里改了自己的层数之后调用）。</summary>
        public void RefreshBuffBoxPublic() => RefreshBuffBox();

        /// <summary>
        /// 按「身上现在有哪些辐射源」重建非环境辐射 buff。
        ///
        /// 谁调它：道具进背包（<c>OnAcquired</c>）、道具被移除、装/卸饰品栏。
        /// 为什么是「重建」而不是增量加减一条账，见 <see cref="Buffs.NonEnvRadioactive.Sync"/>。
        ///
        /// 这个方法是**幂等**的：身上没有辐射源时它会把这个 buff 摘掉，
        /// 有辐射源时会挂上（或刷新）一个 —— 所以调用方不用先判断再调。
        /// </summary>
        internal void SyncRadiationBuff()
        {
            NonEnvRadioactive existing = null;
            foreach (Buff b in Buffs)
            {
                if (b is NonEnvRadioactive found)
                {
                    existing = found;
                    break;
                }
            }

            if (existing == null)
            {
                existing = new NonEnvRadioactive(0, Buff.Permanent);
                Buffs.Add(existing);
            }

            existing.Sync(this);

            // Sync 之后可能一条账都不剩（道具全扔了 / 戴着金属罐屏蔽了）——
            // 那就别让 Buff 框挂着一个「非环境辐射」却什么都不做。
            if (existing.IsExpired)
            {
                Buffs.Remove(existing);
            }

            RefreshBuffBox();
        }

        /// <summary>
        /// 回合开始的处理。战斗循环每回合先调用它；跳过检查也在它内部完成
        /// （见 <see cref="SkippedThisTurn"/>）——
        /// 被跳过的回合里，道具计时、待入包合并、状态栏刷新仍然要照常执行。
        /// </summary>
        public void OnTurnStart(EnvType envType)
        {
            // ⚠️ 原先这里打「=====你的回合=====」。已删（Hyberty 2026-11-21）：
            //     回合头已经写了「=====第 N 回合=====」，玩家马上就会看到选项菜单 ——
            //     两行标题挨着出现，读者得先分辨"哪句是新回合、哪句是同一件事的第二次播报"。
            //     玩家回合的边界已经由「选项菜单出现」这个动作本身表达了，不需要额外一行。
            //
            //     保留它的唯一场景是「额外回合」（Tools 会打 =====额外回合=====），
            //     那种情况下确实是新的一段，所以那一行不动。

            // 跳过检查原先由 Tools.RunBattle 在 OnTurnStart 之后单独调一次，
            // 现在收进这里，并把结果记在 SkippedThisTurn 上供本次回合的每一次玩家行动读取。
            // 位置刻意放在「你的回合」之后：这样日志顺序仍是「你的回合」→「你被跳过回合！」，
            // 与搬家前一致；而 SkipTurn 每回合只在这里消费一次（不是每个行动扣一次）。
            SkippedThisTurn = TryConsumeSkipTurn();

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
            // ⚠️ 血量 / 防御 / 护盾这三行**已删除**（Hyberty 2026-11-21 的信息化简）。
            // 理由：右列状态框本来就常驻显示它们，日志里每回合再打一遍是纯噪音 ——
            // 一回合光"框架"就占 5 行，真正发生的事只有 2-3 行。
            // 日志只留「发生的事」（谁打了谁、掉了多少），状态看固定区域。
            //
            // ⚠️ 环境状态（DescribeStatus）**也已删除**（Hyberty 2026-11-21 实机验证）：
            //   当时以为它"不在别处显示"，实际右列**状态框**常驻显示同一行
            //   （"熔岩瀑布水量：145/200"，格式还更干净，没有日志里的那个 .0）。
            //   清屏之后它成了战斗框里唯一一行"不是事件"的内容，纯冗余。
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

            // 框只有 ConsoleScreen.BuffRows 行。**超出时不能静默丢** ——
            // 被丢掉的那些 buff 效果照常生效（如洞潜僵尸 4 种 debuff 叠满），
            // 玩家看不到就会以为没中招。所以最后一行留作「还有 N 个」提示。
            int limit = ConsoleScreen.BuffRows;
            bool overflow = Buffs.Count > limit;
            int show = overflow ? Math.Max(limit - 1, 0) : Buffs.Count;

            List<string> lines = [];
            for (int i = 0; i < show && i < Buffs.Count; i++)
            {
                Buff b = Buffs[i];
                lines.Add(b.Name + ": " + b.Describe());
            }
            if (overflow)
            {
                lines.Add("还有 " + (Buffs.Count - show) + " 个状态…");
            }
            _screen.DrawBuffs(string.Join("\n", lines));
        }
    }
}
