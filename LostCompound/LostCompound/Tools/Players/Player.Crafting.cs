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
        public List<ICraftable> Recipes { get; } = [];

        public void UnlockAllRecipes()
        {
            // 这道守卫现在**不可达**：唯一的调用点（OnPlayerTurn 的 case 128）自己带了
            // DebugState.Enabled 守卫，未开 debug 时根本走不到这里（会落到 default，
            // 提示与乱输完全一致 —— 不能在这里另打一句「你不能使用这个命令!」，
            // 那句话等于告诉玩家「存在一个隐藏指令」）。
            // 之所以保留：它是 public 方法，删/改 public API 要先问（CONVENTIONS 1.2）；
            // 留着作为第二道防线，将来若有别的调用点也不会绕过开关。
            if (!DebugState.Enabled)
            {
                Console.WriteLine("你不能使用这个命令!");
                return;
            }
            AddDefaultRecipes();
            AddTier1Recipes();
        }

        public void AddDefaultRecipes()
        {
            Console.WriteLine("你成功添加了合成表0!");
            // 产物一律传工厂 `() => new Xxx(1)` 而不是 `new Xxx(1)` 实例 ——
            // 配方如果持有实例，那个实例会被 AddItem 直接放进背包，配方与背包同指一个对象，
            // 于是「合成翻倍」「产物被打光后材料照扣」两种坏法都会出现。
            // 详见 Items\Capabilities\ICraftable.CreateProduct 与 Tools\Recipe 构造函数的注释。
            // 碳：白磷 + 木头（DESIGN 第九章 ID 3「白磷 + 木头」，效果「可点燃；点燃后清除背包 debuff」）。
            // 数量：文档没写 → 按 DESIGN 第九章的通用规则取 × 1（白磷 1 + 木头 1）。
            // 木炭类（Charcoal）本来只能靠「熔岩瀑布高温烤木头 3 回合」（Wood.Become）得到，
            // 这条配方给了玩家一条主动的、可预期的获取路径。
            // 注意：木炭的「点燃后清除背包 debuff」效果依赖背包 debuff 系统，尚未实现。
            Recipes.Add(new Recipe("碳",
                [[ItemIds.WhitePhosphorus, 1], [ItemIds.Wood, 1]],
                () => new Charcoal(1)));

            Recipes.Add(new Recipe("热硫",
                [[ItemIds.WhitePhosphorus, 1], [ItemIds.Charcoal, 1], [ItemIds.Sulphur, 1]],
                () => new FushionSluphur(1)));
            // 玻璃罐：沙子 × 3。
            // ⚠️ 变更记录（用户裁定「撤销」）：中间曾按 DESIGN 第九章的玻璃罐条目试过
            //   「白磷 × 1 + 沙子 × 1」，但白磷要靠杀怪 / Mine 才有，这让玻璃罐显著变贵，
            //   并顺着「水瓶 = 1 玻璃罐」「热硫罐 = 热硫 + 玻璃罐」把成本一路抬高 ——
            //   平衡上不可接受，故撤销，改回「沙子 × 3」。
            //   数量 3 是文档明写的（DESIGN 第九章配方 0 表），不属于「文档没写数量默认 1」那一类。
            Recipes.Add(new Recipe("玻璃罐",
                [[ItemIds.Sand, 3]],
                () => new GlassJar(1)));

            // 水瓶：1 玻璃罐（按你的决定，文档原本写的是「水淹洞穴空房间掉落」）
            Recipes.Add(new Recipe("水瓶",
                [[ItemIds.GlassJar, 1]],
                () => new WaterBottle(1)));

            // 布料：5 木头（文档 ID 41，第九章道具表「5 木头合成」）。
            Recipes.Add(new Recipe("布料",
                [[ItemIds.Wood, 5]],
                () => new Cloth(1)));

            // 热硫罐：热硫 + 玻璃罐（文档 ID 19）。
            // 数量：文档没写 → 按通用规则取 × 1（热硫 1 + 玻璃罐 1）。
            Recipes.Add(new Recipe("热硫罐",
                [[ItemIds.FushionSluphur, 1], [ItemIds.GlassJar, 1]],
                () => new HeatSulphurCan(1)));

            // 药：红磷 + 水瓶（文档 ID 20）。
            // 「磷 + 水瓶」里的单字「磷」已由用户裁定为**红磷（ID 31）**，歧义解除，配方接上。
            // 数量：文档没写 → 按通用规则各取 × 1。
            Recipes.Add(new Recipe("药",
                [[ItemIds.RedPhosphorus, 1], [ItemIds.WaterBottle, 1]],
                () => new Medicine(1)));

            // 毒气弹：热硫 + 白磷 + 玻璃罐（文档 ID 26）。
            // 数量：文档没写 → 按通用规则各取 × 1。
            // 注意：材料里的热硫有 3 场战斗的保质期（FushionSluphur.Timer），
            // 所以这条配方实际上要求玩家先合成热硫、再尽快用掉。
            Recipes.Add(new Recipe("毒气弹",
                [[ItemIds.FushionSluphur, 1], [ItemIds.WhitePhosphorus, 1], [ItemIds.GlassJar, 1]],
                () => new GasBomb(1)));

            // 斗篷：3 木头 + 1 金属 + 1 布料（文档 ID 27）。
            // 获取方式已裁定为**合成**（用户裁定 6）：文档原先写的「放射性区域低概率」已删除，
            // §9 的斗篷行与本条配方现在一致，不再是偏离（历史见 Items\Equipment\Cloak.cs 的注释）。
            Recipes.Add(new Recipe("斗篷",
                [[ItemIds.Wood, 3], [ItemIds.Metal, 1], [ItemIds.Cloth, 1]],
                () => new Cloak(1)));

            // 压缩沙瓶：20 沙子 + 玻璃罐（文档 ID 29）。
            // 文档写的「玻璃瓶」项目里不存在，玻璃容器只有玻璃罐（ID 7），故按 7 处理 —— 待复核。
            Recipes.Add(new Recipe("压缩沙瓶",
                [[ItemIds.Sand, 20], [ItemIds.GlassJar, 1]],
                () => new CompressedSandBottle(1)));
        }

        /// <summary>
        /// 解锁配方 1（设计文档十：第 5 次通关解锁）。
        /// 收录文档里写明合成材料的配方 1 条目（含硫酸：水瓶 + 热硫）。
        /// 注意：硫酸的「开采效果」本身是占位，但它可以合成（文档第九章配方 1 有写材料）。
        /// </summary>
        public void AddTier1Recipes()
        {
            Console.WriteLine("你解锁了配方1!");

            // 镐子：5 金属 + 3 木头
            Recipes.Add(new Recipe("镐子",
                [[ItemIds.Metal, 5], [ItemIds.Wood, 3]],
                () => new Pickaxe(1)));

            // 碘片：碘 + 红磷 + 水瓶
            Recipes.Add(new Recipe("碘片",
                [[ItemIds.Iodine, 1], [ItemIds.RedPhosphorus, 1], [ItemIds.WaterBottle, 1]],
                () => new IodineTablet(1)));

            // 金属罐：金属 + 白磷
            Recipes.Add(new Recipe("金属罐",
                [[ItemIds.Metal, 1], [ItemIds.WhitePhosphorus, 1]],
                () => new MetalCan(1)));

            // 铁剑：2 木头 + 2 金属
            Recipes.Add(new Recipe("铁剑",
                [[ItemIds.Wood, 2], [ItemIds.Metal, 2]],
                () => new IronSword(1)));

            // 磷化剑：铁剑 + 2 白磷 + 2 红磷
            Recipes.Add(new Recipe("磷化剑",
                [[ItemIds.IronSword, 1], [ItemIds.WhitePhosphorus, 2], [ItemIds.RedPhosphorus, 2]],
                () => new PhosphorusSword(1)));

            // 盔甲：3 金属 + 1 白磷
            Recipes.Add(new Recipe("盔甲",
                [[ItemIds.Metal, 3], [ItemIds.WhitePhosphorus, 1]],
                () => new Armor(1)));

            // 盾：3 金属
            Recipes.Add(new Recipe("盾",
                [[ItemIds.Metal, 3]],
                () => new Shield(1)));

            // 硫酸：水瓶 + 热硫（文档第九章配方 1；开采效果仍是占位）
            Recipes.Add(new Recipe("硫酸",
                [[ItemIds.WaterBottle, 1], [ItemIds.FushionSluphur, 1]],
                () => new SulfuricAcid(1)));

            // 放射金属罐：2 放射矿 + 金属罐
            // 注意：放射性矿物（ID 11）目前没有获取途径（Mine / 放射性区域都没做），
            // 所以这条配方现在只能靠调试拿到材料才合得出来。
            Recipes.Add(new Recipe("放射金属罐",
                [[ItemIds.RadioactiveOre, 2], [ItemIds.MetalCan, 1]],
                () => new RadioactiveMetalCan(1)));
        }

        public void HandCraft()
        {
            Console.WriteLine("====徒手合成====");
            List<ICraftable> available = [];

            // 第一步：遍历所有配方，筛选可合成的
            foreach (ICraftable recipe in Recipes)
            {
                if (recipe.CanCraft(this))
                {
                    available.Add(recipe);
                }
            }

            // 第二步：检查是否有可合成的
            if (available.Count == 0)
            {
                Console.WriteLine("你没有任何道具合成!");
                return;
            }

            // 第三步：展示可合成列表
            for (int i = 0; i < available.Count; i++)
            {
                ICraftable r = available[i];
                Console.WriteLine(i + ". " + r.Name + " " + r.DescribeMaterials(this));
            }

            // 第四步：获取选择并执行
            Console.WriteLine("255. 返回");

            // 选错配方不再直接返回：重问直到选到合法编号（不消耗回合）
            int choice;
            while (true)
            {
                Console.WriteLine("请选择..");
                choice = ReadInt("选择配方编号");
                if (choice == 255) return;
                if (choice >= 0 && choice < available.Count) break;
                // 输入流结束：重问只会空转（ReadInt 每次都立刻返回 -1）。
                // 按 255「返回」的同一出口退出合成界面；手输 -1 时 _inputClosed 为 false，
                // 仍走下面那句重问提示，交互不变。
                if (_inputClosed) return;
                Console.WriteLine("选择无效! 请重新选择(0-" + (available.Count - 1) + ", 255 返回)");
            }
            available[choice].Craft(this);
            HandCraft();
        }
        /*
        * 后面是地图逻辑
        * */
    }
}
