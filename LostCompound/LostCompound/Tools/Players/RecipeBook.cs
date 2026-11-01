using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.Items.Containers;
using LostCompound.Items.Consumables;
using LostCompound.Items.Equipment;
using LostCompound.Items.Materials;
using LostCompound.Tools;

namespace LostCompound.Tools.Players
{
    /// <summary>
    /// **合成表的唯一数据源**（配方 0 / 1 / 2 / 3 都登记在这里）。
    ///
    /// 为什么要有这张表：原先同一批配方写在两个地方 ——
    ///   · `AddDefaultRecipes` / `AddTier1Recipes`（真正发给玩家的那两处）；
    ///   · `BuildAllImplementedRecipes`（合成台取补集用的"全集"，又抄了一遍）。
    /// 两份内容一致时看不出问题，但**下次只改一处**就会出两种静默错误：
    ///   · 只改发放处 → 合成台的补集算少了，新配方在合成台里看不见；
    ///   · 只改全集 → 合成台里有一条永远造不出来的幽灵配方。
    /// 这是项目里反复踩过的「同一知识写两遍」的坑（MobSpool / EnvironmentCatalog 都踩过）。
    ///
    /// 现在：**只有这一张表**，下面三个入口都从它派生 ——
    ///   · <see cref="Player.AddDefaultRecipes"/> 取前 <see cref="Tier0Count"/> 条（配方 0）；
    ///   · <see cref="Player.AddTier1Recipes"/> 取剩下的（配方 1）；
    ///   · <see cref="BuildAllImplementedRecipes"/> 取全部（合成台的补集全集）。
    ///
    /// 新增一条配方：**只在这里加一行**，并把档位摆对（配方 0 的一定要排在前 12 条之前）。
    /// </summary>
    public static class RecipeBook
    {
        /// <summary>
        /// 配方 0 的条数 —— 表里**前 12 条**属于配方 0（开局自动解锁），其余属于配方 1。
        /// 加配方 0 的条目时记得把它插在前 12 条之内、并把本常量 +1。
        /// </summary>
        public const int Tier0Count = 12;

        /// <summary>
        /// 全部已实现的配方，**按档位顺序排列**（前 <see cref="Tier0Count"/> 条是配方 0）。
        /// 每项：名称、材料（id + 数量）、产物工厂（`Func<Item>`，不能存实例 —— 见 Tools\Recipe 的注释）。
        /// </summary>
        public static readonly (string Name, int[][] Materials, Func<Item> Create)[] All =
        [
            // ── 配方 0（前 12 条）──
            ("碳", [[ItemIds.WhitePhosphorus, 1], [ItemIds.Wood, 1]], () => new Charcoal(1)),
            ("热硫", [[ItemIds.WhitePhosphorus, 1], [ItemIds.Charcoal, 1], [ItemIds.Sulphur, 1]], () => new FushionSluphur(1)),
            ("玻璃罐", [[ItemIds.Sand, 3]], () => new GlassJar(1)),
            ("硫酸剑", [[ItemIds.IronSword, 1], [ItemIds.SulfuricAcid, 1]], () => new SulfuricSword(1)),
            ("镜子", [[ItemIds.CompressedSandBottle, 5], [ItemIds.WhitePhosphorus, 1], [ItemIds.GlassJar, 1], [ItemIds.Metal, 2]], () => new Mirror(1)),
            ("水瓶", [[ItemIds.GlassJar, 1]], () => new WaterBottle(1)),
            ("布料", [[ItemIds.Wood, 5]], () => new Cloth(1)),
            ("热硫罐", [[ItemIds.FushionSluphur, 1], [ItemIds.GlassJar, 1]], () => new HeatSulphurCan(1)),
            ("药", [[ItemIds.RedPhosphorus, 1], [ItemIds.WaterBottle, 1]], () => new Medicine(1)),
            ("毒气弹", [[ItemIds.FushionSluphur, 1], [ItemIds.WhitePhosphorus, 1], [ItemIds.GlassJar, 1]], () => new GasBomb(1)),
            ("斗篷", [[ItemIds.Wood, 3], [ItemIds.Metal, 1], [ItemIds.Cloth, 1]], () => new Cloak(1)),
            ("压缩沙瓶", [[ItemIds.Sand, 20], [ItemIds.GlassJar, 1]], () => new CompressedSandBottle(1)),

            // ── 配方 1（第 13 条起）──
            ("镐子", [[ItemIds.Metal, 5], [ItemIds.Wood, 3]], () => new Pickaxe(1)),
            ("碘片", [[ItemIds.Iodine, 1], [ItemIds.RedPhosphorus, 1], [ItemIds.WaterBottle, 1]], () => new IodineTablet(1)),
            ("金属罐", [[ItemIds.Metal, 1], [ItemIds.WhitePhosphorus, 1]], () => new MetalCan(1)),
            ("铁剑", [[ItemIds.Wood, 2], [ItemIds.Metal, 2]], () => new IronSword(1)),
            ("磷化剑", [[ItemIds.IronSword, 1], [ItemIds.WhitePhosphorus, 2], [ItemIds.RedPhosphorus, 2]], () => new PhosphorusSword(1)),
            ("盔甲", [[ItemIds.Metal, 3], [ItemIds.WhitePhosphorus, 1]], () => new Armor(1)),
            ("盾", [[ItemIds.Metal, 3]], () => new Shield(1)),
            ("硫酸", [[ItemIds.WaterBottle, 1], [ItemIds.FushionSluphur, 1]], () => new SulfuricAcid(1)),
            ("放射金属罐", [[ItemIds.RadioactiveOre, 2], [ItemIds.MetalCan, 1]], () => new RadioactiveMetalCan(1)),
        ];

        /// <summary>按档位造一批配方（`tier0Only` = 只要配方 0 的那前 12 条）。</summary>
        public static List<ICraftable> Build(bool tier0Only)
        {
            int count = tier0Only ? Tier0Count : All.Length;
            List<ICraftable> result = [];
            for (int i = 0; i < count && i < All.Length; i++)
            {
                (string name, int[][] materials, Func<Item> create) = All[i];
                result.Add(new Recipe(name, materials, create));
            }
            return result;
        }
    }

    public partial class Player
    {
        /// <summary>
        /// 全部已实现的配方（合成台做补集时的"全集"）。
        /// 数据来自 <see cref="RecipeBook.All"/> —— 不再单独维护一份。
        /// </summary>
        public static List<ICraftable> BuildAllImplementedRecipes() => RecipeBook.Build(tier0Only: false);
    }
}
