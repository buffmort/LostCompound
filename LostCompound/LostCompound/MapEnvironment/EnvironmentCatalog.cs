using LostCompound.Tools;   // GameProgress.FinalLayer：最后一层是放射性区域

namespace LostCompound.MapEnvironment
{
    /// <summary>
    /// 「已实现的环境」清单 —— 菜单、调试菜单都从这里取，避免两处手抄走样。
    /// 每实现一个新环境就往这里加一行（而不是遍历 EnvType 全枚举：
    /// 未实现的环境会让 EnvironmentFactory.Create 返回 null，
    /// 玩家选到就得到一张「没有环境」的地图）。
    ///
    /// 为什么要有这个文件：这份「编号 → 显示名 → EnvType」的对应关系原先在
    /// Tools\Launch\GameLobby.SelectEnvironment 和 Tools\Debug\DebugMenu.SelectEnvironment 里各抄了一份
    /// （DebugMenu 的注释自己写着「新增环境时两处都要补」）。两处手抄的必然结果是
    /// 某次只补了一处 —— 主菜单能选、调试菜单选不到（或反过来）。
    /// 现在顺序即菜单编号（第 1 项就是 1 号）。
    /// </summary>
    public static class EnvironmentCatalog
    {
        /// <summary>可选环境：菜单编号用的显示名 + 对应的 EnvType。顺序 = 菜单顺序。</summary>
        public static IReadOnlyList<(string Name, EnvType Type)> All { get; } =
        [
            ("熔岩瀑布", EnvType.LavaWaterfall),
            ("水淹洞穴", EnvType.FloodedCavern),
            ("沙漠", EnvType.Desert),
            ("放射性区域", EnvType.RadioactiveArea),
        ];

        /// <summary>
        /// **放射性区域固定是最后一层**（设计文档 §5.6 + Hyberty 2026-11-22 确认：
        /// 「固定最后一层」「1~9 层永不出放射性区域」）。
        ///
        /// 为什么单独一个常量而不是写死数字：`ForLayer` 要拿它跟层数比，
        /// 而「最后一层」的唯一权威是 <see cref="GameProgress.FinalLayer"/> —— 层数上限
        /// 一旦改（比如以后扩到 15 层），这里必须跟着走，不能各写一个魔数。
        /// </summary>
        public const EnvType FinalLayerEnv = EnvType.RadioactiveArea;

        /// <summary>
        /// **第 1 层固定是熔岩瀑布**（Hyberty 2026-11-01 裁定：设计文档 §5.1 的「固定第一层」）。
        /// 第 2 层起**永远不再是熔岩瀑布**，只在其余环境里随机 / 自选。
        /// </summary>
        public const EnvType FirstLayerEnv = EnvType.LavaWaterfall;

        /// <summary>
        /// 第 <paramref name="layer"/> 层**可以**出现的环境清单。
        ///
        ///   · 第 1 层：只有熔岩瀑布（固定，不给选）；
        ///   · 第 2 层 ~ 倒数第 2 层：熔岩瀑布**被排除**，其余「非固定层环境」照常随机 / 自选；
        ///   · 最后一层：只有放射性区域（也不算「自选」——它必须是这一层）。
        ///
        /// 为什么收成一个方法而不是在两个调用点各写一遍筛选：
        ///   「哪些层能有熔岩瀑布」是一条规则，散成两处必然有一天只改一边
        ///   （自选菜单里还有熔岩瀑布、随机却已经排掉它，就是最典型的走样形态）。
        /// </summary>
        public static IReadOnlyList<(string Name, EnvType Type)> ForLayer(int layer)
        {
            // 最后一层：放射性区域独占（Hyberty 2026-11-22）
            if (layer >= GameProgress.FinalLayer)
            {
                return All.Where(entry => entry.Type == FinalLayerEnv).ToList();
            }

            if (layer <= 1)
            {
                return All.Where(entry => entry.Type == FirstLayerEnv).ToList();
            }

            // 中间层：熔岩瀑布（第一层专属）与放射性区域（最后一层专属）都排除
            return All.Where(entry => entry.Type != FirstLayerEnv && entry.Type != FinalLayerEnv).ToList();
        }

        /// <summary>显示名查表；清单里没有（如 <see cref="EnvType.None"/>）就返回「无环境」。</summary>
        public static string NameOf(EnvType type)
        {
            foreach ((string name, EnvType value) in All)
            {
                if (value == type) return name;
            }
            return "无环境";
        }
    }
}
