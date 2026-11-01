using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.Tools;
using LostCompound.Tools.Debug;

namespace LostCompound.Tools.Players
{
    public partial class Player
    {
        public List<ICraftable> Recipes { get; } = [];

        /// <summary>
        /// 配方 1 是否已解锁。**只给存档用**：`Recipes` 里装的是 `Func&lt;Item&gt;`，
        /// 写不进文件，所以存档记的是「解锁到第几档」，读档时按档位重跑
        /// <see cref="AddDefaultRecipes"/> / <see cref="AddTier1Recipes"/>。
        /// </summary>
        public bool Tier1RecipesUnlocked { get; private set; }

        /// <summary>配方 0 是否已经加过（见 <see cref="AddDefaultRecipes"/> 的幂等守卫）。不进存档：读档时重建。</summary>
        private bool _defaultRecipesAdded;

        /// <summary>返回菜单用的编号。所有菜单统一用它，不再有的地方用 255、有的地方用 -1。</summary>
        public const int MenuBack = -1;

        public void UnlockAllRecipes()
        {
            // 这道守卫现在**不可达**：唯一的调用点（OnPlayerTurn 的 case 128 / 地图上的 128）
            // 自己带了 DebugState.Enabled 守卫，未开 debug 时根本走不到这里。
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

        /// <summary>
        /// 解锁配方 0（开局自动）。**数据来自 <see cref="RecipeBook.All"/> 的前 12 条** ——
        /// 本方法不再自己写一遍配方内容（那会和 RecipeBook 形成两份真相，改一处漏一处）。
        /// </summary>
        public void AddDefaultRecipes()
        {
            // 幂等守卫：这个方法可能被调用两次（开局 RunMainLoop 一次 + 调试指令 128 再一次）。
            // 没有守卫时 `Recipes.Add` 会把同一批配方追加第二遍，合成界面就会出现两条一模一样的条目。
            if (_defaultRecipesAdded) return;
            _defaultRecipesAdded = true;

            Console.WriteLine("你成功添加了合成表0!");
            foreach (ICraftable recipe in RecipeBook.Build(tier0Only: true))
            {
                Recipes.Add(recipe);
            }
        }

        /// <summary>
        /// 解锁配方 1（第十章第 5 次通关，或调试指令 128）。
        /// 数据同样来自 <see cref="RecipeBook.All"/> —— 取 `Tier0Count` 之后的那些。
        /// </summary>
        public void AddTier1Recipes()
        {
            // 幂等守卫：理由同 AddDefaultRecipes；另外「你解锁了配方1!」重复打印会误导玩家。
            if (Tier1RecipesUnlocked) return;

            Console.WriteLine("你解锁了配方1!");

            // 存档只需要知道「解锁到第几档」，不需要把 Recipes 列表本身写进文件
            // （列表里是 Func<Item>，不可序列化）。读档时按这个标记重新解锁一次。
            Tier1RecipesUnlocked = true;

            // 取「全部」再跳过前 Tier0Count 条 = 配方 1 的条目
            List<ICraftable> all = RecipeBook.Build(tier0Only: false);
            for (int i = RecipeBook.Tier0Count; i < all.Count; i++)
            {
                Recipes.Add(all[i]);
            }
        }

        /// <summary>
        /// **合成菜单**（原「徒手合成」）。打开后有两条路：
        ///   1. **查询合成表** —— 列出你手上全部配方（含材料不足的），用来查"某个东西要什么料"；
        ///   2. **进行合成** —— 只列材料够的，合一次后**回到本菜单**，可以连续合。
        ///
        /// 为什么查询和合成要分开：把两者混成一张表（全列 + 标材料不足）会让"选编号"变得危险 ——
        /// 选中一条材料不足的只能报错退出，玩家还得重新打开。分开之后：
        ///   查询 = 只看，不用挑；合成 = 只列能合的，选什么都能成。
        ///
        /// 退出统一用 <see cref="MenuBack"/>（-1），与背包菜单一致；输入流结束（EOF）也返回 -1，
        /// 两者同义，不再像以前那样有的地方用 255、有的地方用 -1。
        /// </summary>
        public void CraftMenu()
        {
            while (true)
            {
                Console.WriteLine("====合成====");
                Console.WriteLine("1. 查询合成表");
                Console.WriteLine("2. 进行合成");
                Console.WriteLine(MenuBack + ". 返回");

                int choice = ReadInt("选择操作");
                if (choice == MenuBack) return;
                if (_inputClosed) return;
                if (choice == 1)
                {
                    ShowRecipeBook();
                    continue;
                }
                if (choice == 2)
                {
                    CraftOne();
                    continue;      // 合完回到菜单 —— 可以连续合
                }
                Console.WriteLine("选择无效，请输入 1 / 2 / " + MenuBack);
            }
        }

        /// <summary>
        /// 合成列表每页显示几条。与调试的「获取道具」同一节奏（一页 10 条、字母选、n/p 翻页），
        /// 因为配方会越加越多（现在 21 条，配方 2/3 做完会到 40+），一次刷完会把日志区顶掉。
        /// </summary>
        public const int RecipePageSize = 10;

        /// <summary>
        /// 查询合成表：列出**手上全部配方**（含材料不足的），材料不足的标出来。
        /// 只读，不消耗任何东西，也不改变状态。翻页浏览，<see cref="MenuBack"/> 退出。
        /// </summary>
        public void ShowRecipeBook()
        {
            if (Recipes.Count == 0)
            {
                Console.WriteLine("你还不会任何配方。");
                return;
            }

            // 浏览模式：选中字母什么都不做，翻页才动 —— 这里把"选中"当成"继续看"，
            // 只有按 -1 才退出。用同一个分页渲染，避免两处各写一套翻页逻辑。
            PagedRecipeList("====合成表====", Recipes, markCraftable: true, out _);
        }

        /// <summary>
        /// 列**材料够的**配方并合成一条。合成完成后由调用方决定是否回到菜单（<see cref="CraftMenu"/> 会）。
        /// 返回是否真的合成了（供测试/将来 UI 判断）。
        /// </summary>
        public bool CraftOne()
        {
            List<ICraftable> available = [];
            foreach (ICraftable recipe in Recipes)
            {
                if (recipe.CanCraft(this))
                {
                    available.Add(recipe);
                }
            }

            if (available.Count == 0)
            {
                Console.WriteLine("你没有任何道具合成!");
                return false;
            }

            int picked = PagedRecipeList("====可合成====", available, markCraftable: false, out int selected);
            if (picked == MenuBack || selected < 0)
            {
                return false;
            }

            CraftAndSettle(Recipes.Count > 0 ? available[selected] : available[selected]);
            return true;
        }

        /// <summary>
        /// **分页选择器**：合成表与可合成列表共用。
        ///
        /// 交互：
        ///   · 一页 <see cref="RecipePageSize"/> 条，用**本页内编号 0~9** 选；
        ///   · `n` 下一页、`p` 上一页（循环，第 1 页按 p 跳到最后一页）；
        ///   · <see cref="MenuBack"/>（-1）= 返回。
        ///
        /// 返回 <see cref="MenuBack"/> 表示玩家退出；否则返回 0（浏览模式）或选中项的下标
        /// （通过 <paramref name="selected"/> 传出，-1 表示没选东西）。
        ///
        /// 为什么要分页：配方会越加越多，一次刷 21~40 行会把日志区顶掉，
        /// 「请选择」那句提示直接看不见 —— 跟调试的「获取道具」当初一模一样的问题。
        /// </summary>
        private int PagedRecipeList(string title, List<ICraftable> list, bool markCraftable, out int selected)
        {
            selected = -1;
            int pages = Math.Max(1, (list.Count + RecipePageSize - 1) / RecipePageSize);
            int page = 1;

            while (true)
            {
                Console.WriteLine(pages > 1 ? title + "（第 " + page + "/" + pages + " 页）" : title);

                int start = (page - 1) * RecipePageSize;
                int end = Math.Min(start + RecipePageSize, list.Count);
                for (int i = start; i < end; i++)
                {
                    ICraftable recipe = list[i];
                    string mark = markCraftable
                        ? (recipe.CanCraft(this) ? "  ✔" : "  （材料不足）")
                        : "";
                    // **编号是本页内的 0 起序号**（0~9），不是全局下标 ——
                    // 屏幕上一眼能看到的就是这个数，不会出现「翻到第 3 页却要输 23」这种别扭事。
                    Console.WriteLine("  " + (i - start) + ". " + recipe.Name + " " + recipe.DescribeMaterials(this) + mark);
                }

                Console.WriteLine(pages > 1
                    ? "  n=下一页  p=上一页  " + MenuBack + "=返回"
                    : "  " + MenuBack + "=返回");

                Console.WriteLine("请选择（编号 / n / p）:");
                string line = Console.ReadLine();
                if (line == null)
                {
                    _inputClosed = true;
                    return MenuBack;
                }

                string key2 = line.Trim().ToLowerInvariant();
                if (key2.Length == 0) continue;
                if (key2 == MenuBack.ToString()) return MenuBack;
                if (key2 == "n")
                {
                    page = page % pages + 1;
                    continue;
                }
                if (key2 == "p")
                {
                    page = (page - 2 + pages) % pages + 1;
                    continue;
                }
                if (int.TryParse(key2, out int typed))
                {
                    int local = typed;                 // 本页内序号（0 起）
                    int index = start + local;
                    if (local >= 0 && index < end)
                    {
                        selected = index;
                        // 浏览模式（markCraftable = true）选中只是继续看，不返回
                        if (markCraftable) continue;
                        return 0;
                    }
                    Console.WriteLine("这一页没有这个编号（0-" + (end - start - 1) + "）。");
                    continue;
                }
                Console.WriteLine("输入无法识别，请输入编号 / n / p。");
            }
        }

        /// <summary>
        /// 标准合成（合成台，DESIGN §4.2）—— 与徒手合成的区别只有「有哪些配方可选」：
        /// 进房时 <see cref="Rooms.Types.CraftRoom"/> 会把**全部已实现配方 − 已有配方**这个补集
        /// 临时加进来；离开房间时再删掉（徒手没有工具，做不了）。
        ///
        /// 列表口径与徒手合成一致：**只列材料够的**（Hyberty 2026-11-01 明确）。
        /// 合完回到菜单，可以连续合。
        /// </summary>
        public void CraftWithStation()
        {
            while (true)
            {
                Console.WriteLine("====标准合成（合成台）====");

                if (CraftOne())
                {
                    continue;      // 合成功了 → 回菜单，继续合
                }
                return;            // 没有可合的 / 玩家返回
            }
        }

        /// <summary>
        /// 合成一条配方并做全部收尾（两个合成入口共用）。
        ///
        /// 收尾三件事的顺序不能换：
        ///   ① <see cref="RemoveConsumedItems"/>：硫酸剑(44) 的 ShouldRemove 恒为 true，
        ///      不立刻扫的话它会挂在背包里直到下回合开头才消失；
        ///   ② <see cref="RecomputeStats"/>：合成会消耗材料，而材料里可能有加成的装备
        ///      （硫酸剑要吃掉铁剑 +7 ATK），不重算会「白赚」到下一回合；
        ///   ③ <see cref="RefreshAfterItemUse"/>：不刷界面状态框还是旧数值。
        /// </summary>
        private void CraftAndSettle(ICraftable recipe)
        {
            recipe.Craft(this);
            RemoveConsumedItems();
            RecomputeStats();
            RefreshAfterItemUse();
        }
    }
}
