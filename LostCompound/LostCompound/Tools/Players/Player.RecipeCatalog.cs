using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.Items.Containers;
using LostCompound.Items.Consumables;
using LostCompound.Items.Equipment;
using LostCompound.Items.Materials;

namespace LostCompound.Tools.Players
{
    /// <summary>
    /// 「所有已实现的配方」—— 合成台（标准合成）要做补集时的**全集**。
    ///
    /// 为什么要单独一个方法（而不是直接读 `Player.Recipes`）：
    ///   `Player.Recipes` 是「**当前手上有的**配方」，随解锁状态变化（配方 0 / 配方 1 / 合成台临时补集）。
    ///   补集需要的是「游戏里**全部**配方」这个固定全集 —— 两者搞混就会算出空集或重复项。
    ///
    /// 维护约定：新增一条配方（不管属于配方 0/1/2/3）**必须**在这里也加一行，
    ///   否则它在合成台里永远看不见。这与 `ItemFactory.All` 的「新增道具必须登记」是同一类约定。
    ///   注意这里**只管数据**（产物与材料），实际发放仍走
    ///   <see cref="Player.AddDefaultRecipes"/> / <see cref="Player.AddTier1Recipes"/> 那两处。
    /// </summary>
    public partial class Player
    {
        /// <summary>全部已实现的配方（与 AddDefaultRecipes / AddTier1Recipes 的内容一一对应）。</summary>
        public static List<ICraftable> BuildAllImplementedRecipes()
        {
            return
            [
                // ── 配方 0 ──
                new Recipe("碳", [[ItemIds.WhitePhosphorus, 1], [ItemIds.Wood, 1]], () => new Charcoal(1)),
                new Recipe("热硫", [[ItemIds.WhitePhosphorus, 1], [ItemIds.Charcoal, 1], [ItemIds.Sulphur, 1]], () => new FushionSluphur(1)),
                new Recipe("玻璃罐", [[ItemIds.Sand, 3]], () => new GlassJar(1)),
                new Recipe("硫酸剑", [[ItemIds.IronSword, 1], [ItemIds.SulfuricAcid, 1]], () => new SulfuricSword(1)),
                new Recipe("镜子", [[ItemIds.CompressedSandBottle, 5], [ItemIds.WhitePhosphorus, 1], [ItemIds.GlassJar, 1], [ItemIds.Metal, 2]], () => new Mirror(1)),
                new Recipe("水瓶", [[ItemIds.GlassJar, 1]], () => new WaterBottle(1)),
                new Recipe("布料", [[ItemIds.Wood, 5]], () => new Cloth(1)),
                new Recipe("热硫罐", [[ItemIds.FushionSluphur, 1], [ItemIds.GlassJar, 1]], () => new HeatSulphurCan(1)),
                new Recipe("药", [[ItemIds.RedPhosphorus, 1], [ItemIds.WaterBottle, 1]], () => new Medicine(1)),
                new Recipe("毒气弹", [[ItemIds.FushionSluphur, 1], [ItemIds.WhitePhosphorus, 1], [ItemIds.GlassJar, 1]], () => new GasBomb(1)),
                new Recipe("斗篷", [[ItemIds.Wood, 3], [ItemIds.Metal, 1], [ItemIds.Cloth, 1]], () => new Cloak(1)),
                new Recipe("压缩沙瓶", [[ItemIds.Sand, 20], [ItemIds.GlassJar, 1]], () => new CompressedSandBottle(1)),

                // ── 配方 1 ──
                new Recipe("镐子", [[ItemIds.Metal, 5], [ItemIds.Wood, 3]], () => new Pickaxe(1)),
                new Recipe("碘片", [[ItemIds.Iodine, 1], [ItemIds.RedPhosphorus, 1], [ItemIds.WaterBottle, 1]], () => new IodineTablet(1)),
                new Recipe("金属罐", [[ItemIds.Metal, 1], [ItemIds.WhitePhosphorus, 1]], () => new MetalCan(1)),
                new Recipe("铁剑", [[ItemIds.Wood, 2], [ItemIds.Metal, 2]], () => new IronSword(1)),
                new Recipe("磷化剑", [[ItemIds.IronSword, 1], [ItemIds.WhitePhosphorus, 2], [ItemIds.RedPhosphorus, 2]], () => new PhosphorusSword(1)),
                new Recipe("盔甲", [[ItemIds.Metal, 3], [ItemIds.WhitePhosphorus, 1]], () => new Armor(1)),
                new Recipe("盾", [[ItemIds.Metal, 3]], () => new Shield(1)),
                new Recipe("硫酸", [[ItemIds.WaterBottle, 1], [ItemIds.FushionSluphur, 1]], () => new SulfuricAcid(1)),
                new Recipe("放射金属罐", [[ItemIds.RadioactiveOre, 2], [ItemIds.MetalCan, 1]], () => new RadioactiveMetalCan(1)),
            ];
        }
    }
}
