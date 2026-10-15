using LostCompound.Items;
using LostCompound.MapEnvironment;

namespace LostCompound.Rooms
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    public static class LootTables
    {
        /**
         * 根据环境类型返回该环境下可能掉落的道具列表。
         * 每次调用都新建一个 List 并放入新的道具实例，所以调用方（宝箱/空房）
         * 拿去加进背包后不会影响后续抽取。
         */
        public static List<Item> GetLootTable(EnvType envType)
        {
            List<Item> items = [];

            switch (envType)
            {
                case EnvType.None:
                    // 空环境（EnvironmentFactory.Create 返回 null）：**故意返回空表**。
                    // 这里原先有一句 `Console.WriteLine("请调试！ 这是一个空环境!")` ——
                    // 已删除，理由不是「不好看」，而是它与事实矛盾且帮不上忙：
                    //   1. 正常玩法**走不到**这个分支。两个选环境的菜单（GameLobby / DebugMenu）
                    //      都只列 EnvironmentCatalog.All 里**已实现**的环境；EnvType.None 与
                    //      「枚举里有、工厂没实现」的环境都进不了 catalog。所以这不是一个
                    //      「玩家会遇到的缺表」，只有将来有人往 catalog 里加了没有工厂分支的名字时才会出现。
                    //   2. 真出现时，那句话里也没有任何可用信息（不说是哪个环境、也不说该做什么），
                    //      玩家只会看到一行像崩溃残留的英文/中文混排。
                    //   3. 兜底掉落**不该**放在这里：本方法按设计就是「环境 → 该环境专属掉落表」，
                    //      给 None 塞一份材料表等于让「环境没实现」这件事静默地继续能玩下去，
                    //      正是 EnvironmentCatalog 注释里要防的那种静默失效。缺环境就该按缺环境处理
                    //      （空房什么都不掉、StartRoom 走 BuildBasicStartPool 的基础池 —— 那一处
                    //       是专门为 None 写的兜底，见 GetStartPool）。
                    break;
                case EnvType.LavaWaterfall:
                    // 熔岩瀑布环境：掉落的特殊道具
                    items.Add(new Sulphur(1));
                    items.Add(new Sulphur(2));
                    items.Add(new Charcoal(1));
                    items.Add(new WhitePhosphorus(1));
                    items.Add(new RedPhosphorus(1));
                    items.Add(new RedPhosphorus(2));
                    items.Add(new Wood(5));
                    items.Add(new Wood(5));
                    items.Add(new Wood(5));
                    break;
                case EnvType.FloodedCavern:
                    // 设计文档 5.2：水淹洞穴的空房间是「水瓶」的正式来源，这里就是那条来源。
                    //
                    // 沿革：早期为了让新增的水瓶/金属/硫酸有个地方测试，曾在熔岩瀑布分支里
                    // 临时塞过这三样。现在水淹洞穴做好了，它们各自回到文档规定的来源 ——
                    // 水瓶 = 本环境，金属 = 矿洞开采（尚未做），硫酸 = 合成（配方 1 已接）。
                    items.Add(new WaterBottle(1));
                    items.Add(new Sand(2));
                    items.Add(new GlassJar(1));
                    break;
                // 其余环境（沙漠/矿洞/墓地/放射性区域/雾都城市）尚未实现，
                // 等做出来后再在这里补1
                // 各自的掉落表。
                default:
                    // 未知环境默认返回空列表
                    break;
            }
            return items;
        }

        // ==================== 起始道具表 ====================
        // 与 GetLootTable 分开的两个理由：
        //   1. 语义不同：GetLootTable 是「宝箱 / 空房间里捡到的东西」，
        //      这里是「开局让你挑的物资」—— 两者内容可以不同，将来也可以各自演化
        //      （比如掉落表加了新道具，不应该顺带改变开局手感）。
        //   2. 结构不同：起始道具是「数量已定的一份道具」，还额外需要一张与之
        //      一一对应的权重表（GetStartWeights），而掉落表是纯列表、不看权重。
        // 调用方：StartRoom 每层开局各调用一次。

        /// <summary>
        /// 起始道具池：本层 StartRoom 发放初始物资时用它。
        /// 与 GetLootTable 分开 —— 后者是「宝箱/空房捡到的东西」，
        /// 这里是「开局让你挑的物资」，两者内容可以不同、将来也可以各自演化。
        ///
        /// 返回的每一项是「数量已定的一份道具」，由 StartRoom 抽 5 个给玩家选 1，共 3 轮。
        /// 权重表与它一一对应（见 GetStartWeights）。
        /// </summary>
        public static List<Item> GetStartPool(EnvType envType)
        {
            List<Item> pool = [];

            switch (envType)
            {
                case EnvType.None:
                    // 空环境同样给出基础池：StartRoom 拿不到东西会卡在 0 个候选项上，
                    // 所以这里绝不能返回空表。
                    pool.AddRange(BuildBasicStartPool());
                    break;
                case EnvType.LavaWaterfall:
                    pool.AddRange(BuildLavaWaterfallStartPool());
                    break;
                case EnvType.FloodedCavern:
                    pool.AddRange(BuildFloodedCavernStartPool());
                    break;
                default:
                    // 尚未实现的环境：暂时回退到基础池，等各环境做出来后各自给表。
                    pool.AddRange(BuildBasicStartPool());
                    break;
            }

            return pool;
        }

        /// <summary>与 GetStartPool 返回的列表一一对应的权重（数字越大越容易抽到）。</summary>
        public static List<int> GetStartWeights(EnvType envType)
        {
            List<int> weights = [];

            switch (envType)
            {
                case EnvType.None:
                    weights.AddRange(BasicStartWeights);
                    break;
                case EnvType.LavaWaterfall:
                    weights.AddRange(LavaWaterfallStartWeights);
                    break;
                case EnvType.FloodedCavern:
                    weights.AddRange(FloodedCavernStartWeights);
                    break;
                default:
                    // 与 GetStartPool 的 default 分支保持一致：同样回退到基础池权重
                    weights.AddRange(BasicStartWeights);
                    break;
            }

            return weights;
        }

        // ---- 各环境的池子与权重 ----
        // ⚠️ 维护须知：下面每一对「池子构造方法 / 权重数组」的顺序与长度必须严格一一对应。
        //    错位（或长度不等）编译器不会报错，只会让权重张冠李戴，是静默错误。
        //    所以每一项后面都标了它自己的权重数字，改完请逐项核对：
        //    池子加一项，权重数组就必须在**同一位置**加一个数。

        /// <summary>
        /// 基础池（熔岩瀑布用，也是尚未实现环境的兜底）—— 按设计文档 2.2 的表。
        /// 共 9 项：文档表的 8 项材料 + 药(20)（用户裁定 6 加进池子）。
        /// </summary>
        private static List<Item> BuildBasicStartPool() =>
        [
            new Wood(2),              // 权重 10
            new Sulphur(3),           // 权重 10
            new Wood(3),              // 权重 8
            new Sulphur(2),           // 权重 8
            new GlassJar(2),          // 权重 5
            new WhitePhosphorus(3),   // 权重 5
            new RedPhosphorus(2),     // 权重 5
            new Sand(6),              // 权重 5

            // 药(20)：用户裁定 6 = 加进开局池（位置与权重照设计文档 2.2 的表，权重 1）。
            // 数量 2 是按裁定 1「所有道具给的量 +1」的口径（文档 2.2 表写的是「1 药」）。
            // 加这一项的理由：药的类与配方早就实现了（配方 0：红磷 + 水瓶，见
            // Player.AddDefaultRecipes），只差「要不要放到开局」这一条 —— 原先这里只有
            // 一句 TODO 占位。「要不要加」属于会改开局平衡的事，已由用户裁定，不再由代码自行决定。
            new Medicine(2),          // 权重 1
            // 咖啡(30) **仍然不进池**：它完全未实现（依赖雾都城市，见 DESIGN 12.3）。
            // 将来真要做时按同一口径是「2 咖啡 / 权重 1」（裁定 1 的数量 +1），
            // 且必须与本数组**同一位置**同步加权重（见上面「⚠️ 维护须知」）。
        ];

        /// <summary>
        /// 基础池的权重 —— 设计文档 2.2 的表，顺序与 <see cref="BuildBasicStartPool"/> 一一对应。
        /// 末位那个 1 是药(20) 的权重；咖啡(30) 未实现，不在表里。
        /// 熔岩瀑布的权重就是这一份（<see cref="LavaWaterfallStartWeights"/> 是它的别名）。
        /// </summary>
        private static readonly int[] BasicStartWeights = [10, 10, 8, 8, 5, 5, 5, 5, 1];

        /// <summary>
        /// 熔岩瀑布的起始池（语义别名，内容与基础池完全相同）：
        /// 熔岩瀑布是设计文档里固定第一层，2.2 那张表给的就是它这一套，
        /// 所以这里直接复用基础池 —— 将来若熔岩瀑布要单独调整，只需让本方法返回自己的列表
        /// （同时必须给它配一张新的 <see cref="LavaWaterfallStartWeights"/>）。
        /// </summary>
        private static List<Item> BuildLavaWaterfallStartPool()
        {
            return BuildBasicStartPool();
        }

        /// <summary>熔岩瀑布的起始权重（= 基础权重）。</summary>
        private static int[] LavaWaterfallStartWeights => BasicStartWeights;
        // ↑ 熔岩瀑布池的长度检查：BuildLavaWaterfallStartPool 委托给基础池（9 项 ——
        //   文档 2.2 表的 8 项材料 + 药，见 BuildBasicStartPool），
        //   权重也是 9 项，两边一致。将来若让熔岩瀑布用自己的池子，别忘了同步这一行。
        //
        // 为什么写成「别名」而不是再抄一份 [10, 10, 8, 8, 5, 5, 5, 5, 1]：
        //   那份数组原先在这里和 BasicStartWeights 各存了一份**完全相同**的常量，
        //   而本文件的维护须知（见上面「⚠️ 维护须知」）要求池子与权重严格逐位对应 ——
        //   同一个池子配两份权重，将来改池子时只要漏改一份，其中一条通路就会静默地用错权重。
        //   池子那边已经是委托写法（BuildLavaWaterfallStartPool => BuildBasicStartPool），
        //   权重这边统一成同一份，两个环境的配对关系就只由一处决定。
        //   用属性而不是 `static readonly int[] = BasicStartWeights`：后者在字段初始化顺序上
        //   依赖声明先后（同类的静态字段初始化按书写顺序执行），属性则在每次读取时才求值，
        //   不存在这个隐蔽的顺序陷阱。调用方 GetStartWeights 只 AddRange 复制、不改数组。

        /// <summary>
        /// 水淹洞穴的起始池（用户要求自定，数量与权重的设计理由见 <see cref="FloodedCavernStartWeights"/>）。
        /// </summary>
        private static List<Item> BuildFloodedCavernStartPool() =>
        [
            new WaterBottle(2),   // 权重 12
            new Metal(2),         // 权重 10
            new Sand(4),          // 权重 8
            new GlassJar(2),      // 权重 8
            new SulfuricAcid(1),  // 权重 6
            new Charcoal(2),      // 权重 6
            new Sulphur(2),       // 权重 5
            new WhitePhosphorus(2), // 权重 4
            new RedPhosphorus(2),   // 权重 3
        ];

        /// <summary>
        /// 水淹洞穴的起始权重，与 <see cref="BuildFloodedCavernStartPool"/> 一一对应。
        ///
        /// 设计理由（数量 / 权重都是自定的，用户授权）：
        ///   · 水瓶 ×2 / 权重 12 —— 本环境的核心。设计文档 5.2 明确「空房间获得水瓶 ×1」，
        ///     且「热硫 + 水瓶 → 硫酸」是这里唯一写明的对策合成链，所以水瓶必须是最容易抽到的。
        ///     一次给 2 个：1 个用于堵水／合成，1 个留作消耗缓冲，避免开局就被水位逼死。
        ///   · 金属 ×2 / 权重 10 —— 金属的正式来源是矿洞开采，但在水淹洞穴里它是「泡在水里的沉料」，
        ///     主题贴合且能让玩家立刻有可用的合成／装备材料。权重次高，但不抢水瓶的位置。
        ///   · 沙子 ×4 / 权重 8 —— 洞穴里的淤积物；玻璃罐的原料，保证玩家能自己造容器。
        ///   · 玻璃罐 ×2 / 权重 8 —— 设计文档 12.1 配方 0 里「水瓶 = 1 玻璃罐」，
        ///     给玻璃罐等于给了「再造水瓶」的能力，和本环境的主题闭环。
        ///   · 硫酸 ×1 / 权重 6 —— 硫酸是本环境合成的产物（水瓶 + 热硫），
        ///     直接给 1 个是「捡到前人遗留的成品」的味道；给多了会架空合成链，所以只给 1、权重中等。
        ///   · 木炭 ×2 / 权重 6 —— 洞穴里可捡的燃料／还原剂，配合硫磺能走热硫路线。
        ///   · 硫磺 ×2 / 权重 5 —— 走「热硫 + 水瓶 → 硫酸」链条的必需原料，给一份即可。
        ///   · 白磷 ×2 / 权重 4 —— 潮湿环境里白磷本就难以保存，给少量、低权重，符合主题。
        ///   · 红磷 ×2 / 权重 3 —— 同理，且红磷在熔岩瀑布池里价值更高，这里刻意压低，
        ///     让两个环境的开局手感区分开。
        ///   刻意不放的东西：放射矿（属矿洞／放射性区域的专属来源）、
        ///   碘 / 碘片 / 万能药等配方 1-2 的产物（不该开局白送）。
        /// </summary>
        private static readonly int[] FloodedCavernStartWeights = [12, 10, 8, 8, 6, 6, 5, 4, 3];
    }
}
