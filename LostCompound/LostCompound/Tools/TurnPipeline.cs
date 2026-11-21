namespace LostCompound.Tools
{
    /// <summary>
    /// **大回合的执行清单**：把「一个回合里按什么顺序做哪些事」变成数据。
    ///
    /// 为什么需要它（Hyberty 2026-11-21 提的 B 方案）：
    ///   原先回合顺序是**隐含在 RunBattle 的语句和缩进里**的 ——
    ///   环境推进、摇意图、玩家回合、怪物回合各是一段代码，想确认"谁在谁前面"得通读。
    ///   而且顺序**改不了**：想做「城市地图让玩家双动」这种差异，只能往循环里塞 if。
    ///   做成清单之后，顺序一眼看完，环境还能往清单里追加步骤。
    ///
    /// ⚠️ **每回合必须 Clone 一份再往里追加**，不能直接往模板上塞：
    ///   模板是常驻的，如果环境每回合都 Add 一个「玩家行动」，
    ///   第 2 回合玩家动 2 次、第 3 回合动 3 次 …… 回合越多越离谱。
    ///   Clone 出来的副本跑完就丢，环境每次追加都是干净的。
    ///
    /// 每一步是一个 <c>Func&lt;bool&gt;</c>：
    ///   返回 true  = 这一步正常做完，继续下一步；
    ///   返回 false = 战斗结束/中止（有人死了、怪清空了、输入流断了），后面不再执行。
    /// 为什么用返回值而不是共享一个标志位：标志位要记得在每个分支里维护，
    /// 迟早漏一处；返回值是**每一步自己的责任**，漏了编译器也不会让你过（必须 return）。
    /// </summary>
    public sealed class TurnPipeline
    {
        // ==================== 内置步骤名 ====================
        // 环境要「插在某一步后面」时用这些常量做锚点，
        // 而不是在环境里手写中文字符串（那样改个名就静默失配了）。

        /// <summary>buff / 持续伤害结算。</summary>
        public const string BuffStep = "状态结算";

        /// <summary>环境推进（掉水、涨水位、环境判负）。</summary>
        public const string EnvironmentStep = "环境推进";

        /// <summary>给全体怪物摇意图。</summary>
        public const string IntentStep = "摇意图";

        /// <summary>玩家回合开始的簿记（清盾、道具计时、状态栏）。</summary>
        public const string PlayerTurnStartStep = "玩家回合开始";

        /// <summary>玩家行动一次（攻击/防御/用道具/采沙）。**「城市双动」就插在这一步后面。**</summary>
        public const string PlayerActionStep = "玩家行动";

        /// <summary>怪物逐个出手。</summary>
        public const string MonsterTurnStep = "怪物回合";

        private readonly List<(string Name, Func<bool> Step)> _steps;

        public TurnPipeline()
        {
            _steps = [];
        }

        private TurnPipeline(List<(string Name, Func<bool> Step)> steps)
        {
            _steps = steps;
        }

        /// <summary>当前有几步（调试 / 断言用）。</summary>
        public int Count => _steps.Count;

        /// <summary>执行顺序的名字列表 —— 直接打印出来就能看清这个环境的回合格局长什么样。</summary>
        public IReadOnlyList<string> Order
        {
            get
            {
                List<string> names = [];
                foreach ((string name, Func<bool> _) in _steps) names.Add(name);
                return names;
            }
        }

        /// <summary>复制一份。每回合跑之前都要 Clone（见类注释里那个「越动越多」的坑）。</summary>
        public TurnPipeline Clone() => new([.. _steps]);

        /// <summary>追加到末尾。</summary>
        public void Add(string name, Func<bool> step)
        {
            if (step == null) return;
            _steps.Add((name, step));
        }

        /// <summary>
        /// 插到某个已有步骤**后面**（找不到就退回追加到末尾）。
        ///
        /// 为什么需要"插在谁后面"：环境的追加点是有语义的 ——
        /// 「城市双动」必须插在「玩家行动」之后，而不是整个清单的最后
        /// （最后已经是怪物回合结束了，那时候再让玩家动就是另一回事）。
        /// </summary>
        public void InsertAfter(string afterName, string name, Func<bool> step)
        {
            if (step == null) return;

            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].Name == afterName)
                {
                    _steps.Insert(i + 1, (name, step));
                    return;
                }
            }

            _steps.Add((name, step));
        }

        /// <summary>
        /// 把已有的某一步**复制一份**插到它后面，并给副本换个名字。
        ///
        /// 这是「城市双动」这类需求的正确入口：环境只说「玩家那个行动再来一次」，
        /// 不需要知道那一步内部调了什么、要不要判胜负、要不要收尸 ——
        /// 副本复用同一个委托，语义和第一次完全相同。
        ///
        /// 返回 false = 没找到那个名字（调用方可以据此报警或忽略）。
        /// </summary>
        public bool InsertCopyAfter(string afterName, string newName)
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].Name != afterName) continue;
                _steps.Insert(i + 1, (newName, _steps[i].Step));
                return true;
            }
            return false;
        }

        /// <summary>按顺序执行，任一步返回 false 就停。</summary>
        public bool Run()
        {
            foreach ((string _, Func<bool> step) in _steps)
            {
                if (!step()) return false;
            }
            return true;
        }
    }
}
