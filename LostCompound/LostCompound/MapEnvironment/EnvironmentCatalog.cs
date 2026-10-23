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
        ];
    }
}
