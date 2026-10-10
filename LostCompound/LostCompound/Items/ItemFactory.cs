namespace LostCompound.Items
{
    /// <summary>
    /// 道具工厂：ID → 名称 → 实例。
    ///
    /// 为什么需要它：整份代码里道具是「一个类一个构造器」散着的，
    /// 没有任何地方能「按 ID 造一个道具」。调试发放、将来的存档读档、
    /// 事件按 ID 给道具（设计文档 5.1/5.2 都写了「获得配方0任意材料」这类），
    /// 都需要这张表。
    ///
    /// 维护约定：新增道具时**必须**在这里补一行，否则它无法被按 ID 创建。
    /// 顺序固定为 **ID 升序**（调试菜单直接按这个顺序打印，用户要求「for 所有道具 id」）。
    /// </summary>
    public static class ItemFactory
    {
        /// <summary>
        /// 所有可创建的道具：ID、显示名、按数量造实例的工厂。
        ///
        /// 名称来源：各道具类 <c>Item(...)</c> 那一行的中文字符串（逐字抄，不重写）；
        /// 构造来源：<c>count => new Xxx(count)</c>（主构造器）。
        /// 这张表以 <see cref="ItemIds"/> 的常量为准 —— 每个常量都必须在这里有且仅有一行。
        /// </summary>
        public static IReadOnlyList<(int Id, string Name, Func<int, Item> Create)> All { get; } =
        [
            (ItemIds.Sulphur, "硫磺", count => new Sulphur(count)),
            (ItemIds.Wood, "木头", count => new Wood(count)),
            (ItemIds.Charcoal, "木炭", count => new Charcoal(count)),
            (ItemIds.FushionSluphur, "热硫", count => new FushionSluphur(count)),
            (ItemIds.WhitePhosphorus, "白磷", count => new WhitePhosphorus(count)),
            (ItemIds.Sand, "沙子", count => new Sand(count)),
            (ItemIds.GlassJar, "玻璃罐", count => new GlassJar(count)),
            (ItemIds.WaterBottle, "水瓶", count => new WaterBottle(count)),
            (ItemIds.Metal, "金属", count => new Metal(count)),
            (ItemIds.SulfuricAcid, "硫酸", count => new SulfuricAcid(count)),
            (ItemIds.RadioactiveOre, "放射性矿物", count => new RadioactiveOre(count)),
            (ItemIds.Pickaxe, "镐子", count => new Pickaxe(count)),
            (ItemIds.Iodine, "碘", count => new Iodine(count)),
            (ItemIds.IodineTablet, "碘片", count => new IodineTablet(count)),
            (ItemIds.MetalCan, "金属罐", count => new MetalCan(count)),
            (ItemIds.RadioactiveMetalCan, "放射金属罐", count => new RadioactiveMetalCan(count)),
            (ItemIds.IronSword, "铁剑", count => new IronSword(count)),
            (ItemIds.PhosphorusSword, "磷化剑", count => new PhosphorusSword(count)),
            (ItemIds.Armor, "盔甲", count => new Armor(count)),
            (ItemIds.Shield, "盾", count => new Shield(count)),
            (ItemIds.RedPhosphorus, "红磷", count => new RedPhosphorus(count)),
        ];

        /// <summary>
        /// 按 ID 造道具；未登记的 ID 返回 <c>null</c>（不抛异常 —— 调用方大多是调试/事件路径，
        /// 为一个不存在的 ID 崩掉整个游戏不值得）。
        /// </summary>
        public static Item Create(int id, int count)
        {
            foreach ((int Id, string Name, Func<int, Item> Create) entry in All)
            {
                if (entry.Id == id)
                {
                    return entry.Create(count);
                }
            }
            return null;
        }

        /// <summary>ID 是否已登记。</summary>
        public static bool IsKnown(int id)
        {
            foreach ((int Id, string Name, Func<int, Item> Create) entry in All)
            {
                if (entry.Id == id)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
