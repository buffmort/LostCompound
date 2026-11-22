using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Tools
{
    /// <summary>
    /// **怪物意图的编排者**：每回合开局把全体怪物的意图摇一遍。
    ///
    /// 为什么单独一个类，而不是在 Tools.RunBattle 里 `foreach (m in monsters) m.PlanIntent(p)`：
    ///   · 战斗循环的职责是「推进流程」，不该知道「意图怎么摇、什么时候摇、要不要摇」。
    ///     写在循环里的话，以后加「先攻排序」「意图锁定」「某些怪不参与摇」这类规则，
    ///     全都要往那个已经很长的循环里塞 if。
    ///   · 抽出来之后，循环里只剩一句 `intents.BeginRound(p)`，
    ///     规则的增删都改这一个文件，不动流程。
    ///
    /// 时机：必须在**玩家行动之前**（否则意图没法用于决策），
    /// 且必须在**环境推进之后**（缺水扣 STR 会改伤害，先推进再摇才准）。
    /// </summary>
    public sealed class MonsterIntents
    {
        private readonly IReadOnlyList<Monster> _monsters;

        public MonsterIntents(IReadOnlyList<Monster> monsters)
        {
            _monsters = monsters ?? [];
        }

        /// <summary>
        /// 新一轮开始：给所有活着的怪物摇出本回合意图。
        /// 死掉的跳过 —— 它们的意图没人看，摇了也只是浪费一次随机数。
        /// </summary>
        public void BeginRound(Player p)
        {
            for (int i = 0; i < _monsters.Count; i++)
            {
                Monster m = _monsters[i];
                if (m == null || !m.IsAlive) continue;
                m.PlanIntent(p);
            }
        }

        /// <summary>
        /// 把当前意图拍成「敌人框里显示的那几行」。
        ///
        /// 放在这里而不是 Player.UI：显示格式（`名字(血) - 意图`）属于「意图」这件事，
        /// 界面只负责把字符串画进框里。以后想改成分行显示、加颜色、加图标，
        /// 都只改这一个方法。
        /// </summary>
        public List<string> FormatLines(Player p, int maxRows)
        {
            List<string> lines = [];
            int shown = 0;

            for (int i = 0; i < _monsters.Count && shown < maxRows; i++)
            {
                Monster m = _monsters[i];
                if (m == null) continue;

                string line = "[" + i + "] " + m.ToString().TrimEnd();

                // 护盾：> 0 才显示。
                // 为什么必须显示（Hyberty 2026-11-22）：盾是「这一刀会不会被吃掉」的唯一线索，
                // 而 Monster.ToString() 只给「名字(血)」。龙每回合 +5 盾（上限 15）、
                // 沙葬者用沙压沙甲 —— 看不见盾等于让玩家盲猜该打谁、该不该补刀。
                // 放在名字后面而不是意图后面：护盾是**怪自己现在的状态**，
                // 与「这回合要干什么」是两回事，混进意图尾巴会被误读成「打 10 并加 5 盾」。
                if (m.Shield > 0) line += " 盾 " + m.Shield.ToString("F1");

                // 空串 = 这只怪还没接意图，就不加尾巴（不留一个孤零零的破折号）
                string intent = m.IntentText(p);
                if (!string.IsNullOrEmpty(intent)) line += " - " + intent;

                lines.Add(line);
                shown++;
            }

            return lines;
        }
    }
}
