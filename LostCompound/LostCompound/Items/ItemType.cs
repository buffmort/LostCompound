namespace LostCompound.Items
{
    // 成员名改为 PascalCase；原有成员的声明顺序保持不变，从而其整数值不变。
    // 新增成员一律追加在末尾，避免影响已有成员的数值。
    public enum ItemType
    {
        Sulphur,
        Wood,
        Charcoal,
        FushionSluphur,
        WhitePhosphorus,
        RedPhosphorus,
        Sand,
        GlassJar,
        WaterBottle,
        Metal,
        SulfuricAcid,

        // 配方 1 新增
        Pickaxe,
        Iodine,
        IodineTablet,
        MetalCan,
        IronSword,
        PhosphorusSword,
        Armor,
        Shield,

        // 配方 0 补全（ID 11）与配方 1 补全（ID 18）时新增，按约定只追加在末尾
        RadioactiveOre,
        RadioactiveMetalCan,
    }
}
