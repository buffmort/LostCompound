using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Items;
using LostCompound.Monsters;
using LostCompound.Maps;
using LostCompound.UI;
using LostCompound.Buffs;

namespace LostCompound.Tools
{
    public class Player
    {

        public double Hp { get; set; } = 200;
        // 设计文档 7.3：基础 ATK = 2 + STR × √ATK，STR 基础 = 3，ATK = 20
        // 初值用常量而不是字面量，避免「改了一处忘了另一处」
        public double Atk { get; set; } = BaseAtk;

        /// <summary>
        /// 当前 STR。环境（如熔岩瀑布缺水）会直接改这个值 ——
        /// 所以环境每次都必须「从 <see cref="BaseStr"/> 重算」，不能 -= 累加，
        /// 否则惩罚会与「打了多少回合」挂钩，而且水回满了也恢复不了。
        /// </summary>
        public double Str { get; set; } = BaseStr;

        /// <summary>玩家基础 STR（设计文档 7.3：3）。环境重算时要拿它当基准。</summary>
        public const double BaseStr = 3;
        public string Name { get; set; }
        public double Def { get; set; } = BaseDef;

        /// <summary>
        /// 护盾值（本次改动：整套替换掉原来的 `public int Block` 次数制）。
        ///
        /// 语义：护盾 = 「额外的血量」。受伤时**先扣盾，盾不够扣的部分才扣血**。
        /// 与旧 Block 的区别：旧机制 1 点 Block 完全免疫一次伤害（哪怕 999 点），
        /// 用户裁定「太超模」，所以改成按点数消耗的护盾值制。
        ///
        /// 为什么是 double：与 Hp / Def / Atk 一致，将来做「按比例给盾」的装备不用再改类型。
        /// </summary>
        public double Shield { get; set; }

        /// <summary>
        /// 增加护盾。用方法而不是裸写 `Shield += x`：CONVENTIONS 2.3 规定
        /// 「累加语义用 AddXxx 方法」，与 Monster.AddShield / AddSkipTurn 保持同一写法。
        /// </summary>
        public void AddShield(double amount) => Shield += amount;

        public int SkipTurn { get; set; }
        public bool IsDead { get; set; }
        private Room[][] _map;      // 当前地图
        public int PlayerRow { get; private set; }     // 玩家所在行
        public int PlayerCol { get; private set; }     // 玩家所在列
        private bool _debug;

        // ---- TUI 状态栏联动 ----
        private ConsoleScreen _screen;          // 当前接管输出的分栏界面（不在游戏中时为 null）
        private string _statusHeader = "";      // 状态栏固定行：当前房间
        private bool _inBattle;                 // 是否处于战斗中（决定状态栏是否追加血量等）

        // 玩家背包 / 待入包 / 已解锁配方
        public List<Item> Inventory { get; } = [];
        public List<Item> PendingItems { get; } = [];
        public List<ICraftable> Recipes { get; } = [];

        /// <summary>
        /// 背包容量（设计文档 8.1）：初始 15 格。
        /// 一格 = 一个 Item 条目（可堆叠的道具多份共用一格）。
        /// 装不下时超出部分直接丢弃（用户决定）。
        /// </summary>
        public int InventoryCapacity { get; private set; } = 15;

        /// <summary>背包容量上限（设计文档 8.1：最终 22）。</summary>
        public const int MaxInventoryCapacity = 22;

        /// <summary>
        /// 扩充背包容量。设计文档：第 2 次通关 +3（→18），第 4 次通关 +4（→22）。
        /// 通关系统尚未实现，先留接口。
        /// </summary>
        public void GrowInventoryCapacity(int amount)
        {
            int before = InventoryCapacity;
            InventoryCapacity = Math.Min(InventoryCapacity + amount, MaxInventoryCapacity);
            if (InventoryCapacity != before)
            {
                Console.WriteLine($"背包容量扩充至 {InventoryCapacity} 格！");
            }
        }

        /// <summary>
        /// 饰品栏（设计文档 8.2）：与背包分开存放，不占背包格。
        /// 初始 5 格，每通关 +1，上限 15。
        ///
        /// 按用户决定（方案 B）：任何「被动物件」（武器/防具/饰品）都能放进这里 ——
        /// 饰品栏本质上是「额外 5 个放被动物件的格子」，不限类别。
        /// </summary>
        public List<Item> Accessories { get; } = [];

        /// <summary>饰品栏容量。初始 5，每通关 +1，上限 15。</summary>
        public int AccessorySlots { get; private set; } = 5;

        /// <summary>饰品栏容量上限（设计文档 8.2）。</summary>
        public const int MaxAccessorySlots = 15;

        /// <summary>
        /// 通关一次后扩充饰品栏（+1 格，不超过上限）。
        /// 通关系统尚未实现，这个方法先留着，等进度系统做出来调用。
        /// </summary>
        public void GrowAccessorySlots()
        {
            if (AccessorySlots < MaxAccessorySlots)
            {
                AccessorySlots++;
                Console.WriteLine($"饰品栏扩充至 {AccessorySlots} 格！");
            }
        }

        // 存活判定：派生值用计算属性（原 isAlive() 方法）
        public bool IsAlive => Hp > 0;

        // ==================== 状态效果（buff，如流血） ====================
        // 原先这里的 DotDamage / DotDuration 字段与 AddDotDamage / AddDotDuration
        // 已按你的要求整体删除（DOT 机制改为流血 buff），周期性伤害只走下面这条出口。
        /// <summary>挂在玩家身上的状态效果。结算时机见 Tools.ApplyBuffs（大回合开场）。</summary>
        public List<Buff> Buffs { get; } = [];

        /// <summary>
        /// 给玩家挂一个状态效果。
        /// 同类 buff **叠加到已有对象上**，而不是挂两个同名对象：理由
        ///   1. 「流血 10 点」是一个状态，不是两个状态；分开放会让上限 10 的判断散成多处；
        ///   2. 显示层（OnPlayerTurn 里的状态列表）不需要再去重；
        ///   3. 判断「同类」用运行时类型（GetType()）而不是 Name：Name 是给人看的文本，
        ///      将来完全可能出现两个同名但结算不同的 buff，用类型判断更严格。
        /// 叠加规则（伤害取 min、剩余回合相加）写在 Buff.MergeFrom 里，玩家/怪物共用一份。
        /// </summary>
        public void AddBuff(Buff b)
        {
            if (b == null) return;
            foreach (Buff exist in Buffs)
            {
                if (exist.GetType() == b.GetType())
                {
                    exist.MergeFrom(b);
                    RecomputeStr();        // buff 可能改 STR，立刻重算
                    RefreshBuffBox();      // 叠加会改伤害/剩余回合，框要立刻跟上
                    return;
                }
            }
            Buffs.Add(b);
            RecomputeStr();                // buff 可能改 STR，立刻重算
            RefreshBuffBox();
        }

        /// <summary>
        /// 重算当前 STR = 基础 STR + 环境修正 + 所有 buff 的修正。
        ///
        /// 为什么必须「从基础值重算」，而不是谁想改就 -= / += ：
        ///   改 STR 的来源有好几个（环境、buff、将来的装备）。用加减法的话，
        ///   每个来源都得自己负责「什么时候加回来」—— buff 有三条移除路径
        ///   （自然到期 / 战斗结束 ClearBuffs / 将来的驱散），漏掉任何一条，
        ///   STR 就永久偏移一点，而且不报错、不崩溃，只是「打着打着变弱了」。
        ///   重算没有这些问题：不管谁改了什么，值永远等于「基础 + 当前所有修正之和」。
        /// </summary>
        public void RecomputeStr()
        {
            double str = BaseStr;

            // 环境修正（如熔岩瀑布缺水）
            Environment env = CurrentEnvironment;
            if (env != null) str += env.StrModifier;

            // buff 修正（如某个增益 +1 STR）
            foreach (Buff b in Buffs)
            {
                str += b.StrBonus;
            }

            Str = str;
        }

        /// <summary>跳过回合：返回"这次是否被跳过"，顺手扣一次计数。
        /// 与 Monster.TryConsumeSkipTurn 对称 —— 由战斗循环统一调用。</summary>
        public bool TryConsumeSkipTurn()
        {
            if (SkipTurn <= 0) return false;
            SkipTurn--;
            Console.WriteLine("你被跳过回合！");
            return true;
        }

        public void AddItem(Item newItem)
        {
            if (newItem == null) return;

            if (!newItem.IsStackable)
            {
                // 不可堆叠 = 独占一格。背包满了就丢弃（按用户决定：超过了直接删除）
                if (Inventory.Count >= InventoryCapacity)
                {
                    Console.WriteLine($"背包已满，{newItem.Name} x{newItem.Count} 被丢弃了！");
                    return;
                }
                Inventory.Add(newItem);
                Console.WriteLine($"你获得了 {newItem.Name} x{newItem.Count}！");
                return;
            }

            // 超过单格上限的，直接删
            if (newItem.Count > newItem.MaxStack)
            {
                newItem.Count = newItem.MaxStack;
            }

            int remaining = newItem.Count;
            int added = 0;   // 实际加进背包的数量（堆叠时可能小于 original count）

            foreach (Item existItem in Inventory)
            {
                if (remaining <= 0) break;
                if (existItem.Type != newItem.Type) continue;
                if (!existItem.IsStackable) continue;

                int space = existItem.MaxStack - existItem.Count;
                if (space <= 0) continue;

                int move = Math.Min(space, remaining);
                existItem.Count += move;
                remaining -= move;
                added += move;
            }

            if (remaining > 0)
            {
                // 现有堆叠都满了，需要新开一格；背包满则丢弃剩余部分
                if (Inventory.Count >= InventoryCapacity)
                {
                    Console.WriteLine($"背包已满，{newItem.Name} x{remaining} 被丢弃了！");
                }
                else
                {
                    newItem.Count = remaining;
                    Inventory.Add(newItem);
                    added += remaining;
                }
            }

            // 统一在这里播报获得，调用方不要再重复打印
            if (added > 0)
            {
                Console.WriteLine($"你获得了 {newItem.Name} x{added}！");
            }
        }

        /// <summary>
        /// 回合开始的处理。战斗循环每回合先调用它，再做跳过检查 ——
        /// 被跳过的回合里，道具计时、待入包合并、状态栏刷新仍然要照常执行。
        /// </summary>
        public void OnTurnStart(EnvType envType)
        {
            Console.WriteLine("=====你的回合=====");
            RefreshStatus();               // 战斗中让状态栏显示血量/防御/护盾
            // 环境状态（水量、水位）每回合都在变，所以每回合都要重算 STR，
            // 不能只在进战斗时算一次 —— 否则缺水削弱要等到下一场战斗才生效。
            RecomputeStr();
            RefreshBuffBox();              // Buff 框：进新回合时剩余回合已经 -1，必须重画
            HandleChangeableItems(envType);
            Inventory.AddRange(PendingItems);
            PendingItems.Clear();
            Calculator.Delay(250);
            Console.WriteLine("你的血量:{0:F1}", Hp);
            Console.WriteLine("你的防御:{0:F1}", Def);
            Console.WriteLine("你的护盾剩余:" + Shield);
            // 环境状态（如熔岩瀑布水量）：供玩家决定要不要用水瓶
            if (CurrentEnvironment != null)
            {
                Console.WriteLine(CurrentEnvironment.DescribeStatus());
            }
        }

        /// <summary>
        /// 玩家的「行动」部分：展示战场与可选项、读取输入并执行。
        /// 回合开始的簿记放在 OnTurnStart，跳过检查由战斗循环统一做 ——
        /// 所以这里只管"没被跳过时该干什么"。
        /// </summary>
        public void OnPlayerTurn(List<Monster> monsters, EnvType envType)
        {
            if (!IsAlive)
            {
                Console.WriteLine("你死了！");
                IsDead = true;
                return;
            }
            // 注意：流血不在这里结算。大回合开场的 Tools.ApplyBuffs 已经统一
            // 结算过一次，原先在此再扣一次会让玩家每回合吃两次持续伤害、时长掉两倍快。
            // （DOT 机制删除前同样的坑就在这里，口径没变。）
            // 背包中的「可转变道具」（白磷 / 热硫 / 木头等）与耐久结算已经在
            // OnTurnStart → HandleChangeableItems 里处理完了；这里只做回合内的展示。

            // 注意：buff 的状态快照（每回合伤害 / 剩余回合）原先在这里往日志打两行，
            // 现已整段删除 —— 这些信息改由右列状态框下方的「Buff 框」常驻显示（见 RefreshBuffBox），
            // 日志省下这些空间给真正的事件流（谁打了谁、掉了多少血）。
            Calculator.Delay(250);
            // 敌人列表不再写进日志区，改由左列顶部的「敌人框」常驻显示（见 ConsoleScreen.DrawEnemies）。
            // 为什么搬走：Attack / SelectTarget / OpenItems 的提示都要求玩家按编号选目标，
            // 编号必须一直挂在眼前；写进滚动日志的话，几行之后就被冲掉了。
            RefreshEnemyBox(monsters);
            Calculator.Delay(250);
            Console.WriteLine();
            Console.WriteLine("请选择操作");
            Calculator.Delay(250);
            Console.WriteLine("1.攻击  2.防御  3.使用背包道具  4.徒手合成  5.饰品栏");
            if (_debug)
            {
                Console.WriteLine("debug模式操作:");
                Console.WriteLine("127, 超雄模式");
                Console.WriteLine("128, 获得全部合成");
                // 129 是新增的：战斗中当场拿道具（主菜单的「4. 获取道具」发到常驻调试玩家身上，
                // 战斗中由这里拿到正在战斗的这个玩家身上）。编号避开 127/128/255。
                Console.WriteLine("129, 获取道具");
            }
            int option = ReadInt("请选择操作");
            switch (option)
            {
                case 1:
                    Attack(monsters);
                    break;
                case 2:
                    // 原为 Block += 1（次数制，一次全免）；改为加 5 点护盾值。
                    // 数值理由：这是玩家唯一稳定可控的「攒盾」手段，而玩家面对的单次
                    // 最高伤害是龙的 10 点、双子技能的 5 点 —— 5 点盾大致等于「挡掉半刀」，
                    // 一回合换半刀比旧机制「一回合换一整刀」弱得多，符合「旧机制太超模」的裁定。
                    AddShield(5);
                    Console.WriteLine("你举起了护盾！（护盾 +5）");
                    break;
                case 3:
                    OpenItems(monsters);
                    break;
                case 4:
                    HandCraft();
                    break;
                case 5:
                    OpenAccessoryMenu();
                    break;
                case 127:
                    Str = 100;
                    break;
                case 128:
                    UnlockAllRecipes();
                    break;
                case 129:
                    // 定量发放任意道具（ID → 名称 → 数量）。
                    // 复用 DebugMenu 的那张 ItemFactory 表，不在这里再抄一份 ID 列表。
                    DebugMenu.GiveItems(this);
                    break;
                case 255:
                    _debug = true;
                    Console.WriteLine("你已进入debug模式!");
                    break;
                default:
                    Console.WriteLine("你输入的类型不正确！ 已自动跳过回合");
                    break;
            }
        }

        /// <summary>
        /// 玩家受伤。
        ///
        /// 结算顺序（本次裁定）：**先 DEF 减免，再吃盾**。
        /// 理由：若反过来「先吃盾再 DEF 减免」，盾会被防御白吃一部分 ——
        ///   例：打来 10 点、DEF = 5、盾 = 5。
        ///     先 DEF：10 - 5 = 5 最终伤害 → 盾抵 5 → 掉血 0（盾刚好用完）
        ///     先吃盾：盾 5 全吃掉 10 里的 5 → 剩 5 再 - DEF 5 → 掉血 0
        ///   两者这里看似一样，但换成「盾 = 20」就露馅了：
        ///     先 DEF：10 - 5 = 5，盾剩 15，盾的价值 5
        ///     先吃盾：盾吃满 10，再被 DEF 夹到最低 1，盾白丢了 9 点价值
        ///   即「先吃盾」会让盾的每一层都被 DEF 削一次，盾越多亏得越多；
        ///   「先 DEF」则让盾按最终伤害 1:1 抵扣，数值直观、可预期。
        ///
        /// 盾与血的关系：盾就是额外血量，扣完盾才动 Hp（见下面 AbsorbWithShield 的逻辑）。
        /// </summary>
        public void TakeDamage(double dmg)
        {
            double realDmg = Calculator.ApplyDefense(Def, dmg);

            if (Shield >= realDmg)
            {
                // 盾够厚：完全挡住，血量一点不掉
                Shield -= realDmg;
                Console.WriteLine("你的护盾挡下了伤害!");
                RefreshStatus();           // 护盾变化后立刻更新状态栏
                return;
            }

            // 盾不够（或没盾）：先清空盾，剩下的才扣血
            double remaining = realDmg - Shield;
            Shield = 0;
            Hp -= remaining;
            Console.WriteLine("你受到{0:F1}点伤害!", remaining);
            Console.WriteLine();
            RefreshStatus();               // 受伤后立刻更新状态栏
            // 玩家身上可能有 buff 在结算中（如流血每回合走 TakeDamage），
            // 这里顺手刷新一次 Buff 框，保证「结算完的那一跳」马上反映到框里。
            RefreshBuffBox();
        }

        /// <summary>
        /// 玩家当前所在房间的环境对象（可能为 null：没进地图、或该层没有环境）。
        /// 用 internal 而不是 public：只给同一程序集内的道具（水瓶）读取，不扩大对外公开 API。
        /// </summary>
        internal Environment CurrentEnvironment
        {
            get
            {
                if (_map == null) return null;
                Room room = _map[PlayerRow][PlayerCol];
                return room?.Environment;
            }
        }

        public void Attack(List<Monster> monsters)
        {
            double dmg = Calculator.CalculateAttack(Str, Atk);
            Console.WriteLine("=====攻击界面=====");
            Calculator.Delay(250);

            if (monsters.Count == 0)
            {
                // 没有目标就不能进入重问循环，否则会死循环
                Console.WriteLine("没有可以攻击的目标！");
                return;
            }

            int count = 0;
            foreach (Monster i in monsters)
            {
                Console.Write(count + "." + " " + i + "   ");
                count++;
            }
            Console.WriteLine();

            // 选错目标不再结束回合：重问直到选到合法编号为止
            int choice;
            while (true)
            {
                Console.WriteLine("请选择怪物攻击:");
                choice = ReadInt("选择目标编号");
                if (choice >= 0 && choice < monsters.Count) break;
                Console.WriteLine("您的攻击无效！请重新选择(0-" + (monsters.Count - 1) + "):");
            }

            Monster target = monsters[choice];
            target.TakeDamage(dmg);
            Console.WriteLine("你攻击了" + target + "!");

            // 磷化剑：命中时给目标挂 1 层流血（设计文档第九章配方 1 / 3.2 技能 6）。
            // 「怎么知道在用这把剑」：本项目没有「装备中的武器」这个概念 ——
            // IEquippable 的被动在背包或饰品栏里都生效（见 ApplyEquipmentPassives），
            // 所以这里用同一套判定：身上有磷化剑，攻击就带流血。
            // 目标已被这一击打死就不用挂了（挂上去也没人结算）。
            if (target.IsAlive && HasPhosphorusSword())
            {
                target.AddBuff(new BleedBuff(PhosphorusSword.BleedDamagePerHit, PhosphorusSword.BleedTurnsPerHit));
                Console.WriteLine("磷化剑的磷毒让" + target + "开始流血！");
            }
        }

        /// <summary>身上（背包或饰品栏）是否有磷化剑 —— 判定口径与 ApplyEquipmentPassives 一致。</summary>
        private bool HasPhosphorusSword()
        {
            return Inventory.Any(item => item is PhosphorusSword)
                || Accessories.Any(item => item is PhosphorusSword);
        }

        /// <summary>
        /// 读一个整数。输入非法时提示并重新读取，绝不抛异常。
        /// 替代原先裸用的 int.Parse(Console.ReadLine()) —— 那样在菜单里输入一个字母就会让整个游戏崩溃。
        /// prompt 会同时显示在右侧日志与输入框里（输入框是 TUI 的光标所在处）。
        /// </summary>
        private int ReadInt(string prompt)
        {
            while (true)
            {
                // 输入框里也显示一份提示，否则玩家只能盯着滚动日志看要输入什么
                _screen?.SetPrompt(prompt);
                if (int.TryParse(Console.ReadLine(), out int value))
                {
                    return value;
                }
                Console.WriteLine("输入无效，请输入数字。");
            }
        }

        /// <summary>
        /// 让玩家从 monsters 里选一个对象，返回其下标；没有可选对象时返回 -1。
        /// 输入非法不消耗回合，会一直重问。
        /// </summary>
        public int SelectTarget(List<Monster> monsters)
        {
            Console.WriteLine("请选择对象:");
            int count = 0;
            foreach (Monster i in monsters)
            {
                Console.Write(count + "." + " " + i + "   ");
                count++;
            }

            if (monsters.Count == 0)
            {
                // 没有目标就不能进入重问循环，否则会死循环
                Console.WriteLine("没有可以选择的对象！");
                return -1;
            }

            // 越界不再直接放弃：重问直到选到合法编号（不消耗回合）
            while (true)
            {
                int choice = ReadInt("选择对象编号");
                if (choice >= 0 && choice < monsters.Count)
                {
                    return choice;
                }
                Console.WriteLine("您的投掷无效！请重新选择(0-" + (monsters.Count - 1) + "):");
            }
        }

        // ==================== 饰品栏（设计文档 8.2） ====================

        /// <summary>
        /// 饰品栏交互界面：可以装备（把背包里的被动物件放上去）或卸下。
        /// 选 0 退出，不消耗回合。
        /// </summary>
        private void OpenAccessoryMenu()
        {
            while (true)
            {
                Console.WriteLine("===== 饰品栏 =====");
                ShowAccessories();

                Console.WriteLine("1.装备   2.卸下   0.返回");
                int action = ReadInt("饰品栏：1装备 2卸下 0返回");
                if (action == 0) return;

                if (action == 1)
                {
                    // 只列出背包里真正能装备的（实现了 IEquippable 的被动物件）
                    List<int> equipable = [];
                    for (int i = 0; i < Inventory.Count; i++)
                    {
                        if (Inventory[i] is IEquippable)
                        {
                            equipable.Add(i);
                        }
                    }

                    if (equipable.Count == 0)
                    {
                        Console.WriteLine("背包里没有可以装备的道具。");
                        continue;
                    }

                    Console.WriteLine("可装备的道具（武器/防具/饰品等被动物件）：");
                    foreach (int idx in equipable)
                    {
                        string desc = Inventory[idx] is IEquippable eq ? "  " + eq.DescribePassive() : "";
                        Console.WriteLine("  [" + idx + "] " + Inventory[idx] + desc);
                    }

                    int pick = ReadInt("选择要装备的道具编号(-1取消)");
                    if (pick == -1) continue;
                    EquipAccessory(pick);
                }
                else if (action == 2)
                {
                    if (Accessories.Count == 0)
                    {
                        Console.WriteLine("饰品栏是空的。");
                        continue;
                    }

                    int pick = ReadInt("选择要卸下的道具编号(-1取消)");
                    if (pick == -1) continue;
                    UnequipAccessory(pick);
                }
            }
        }

        /// <summary>显示饰品栏内容。</summary>
        public void ShowAccessories()
        {
            Console.WriteLine($"===== 饰品栏 ({Accessories.Count}/{AccessorySlots}) =====");
            if (Accessories.Count == 0)
            {
                Console.WriteLine("（空）");
                return;
            }

            for (int i = 0; i < Accessories.Count; i++)
            {
                string desc = Accessories[i] is IEquippable eq ? "  " + eq.DescribePassive() : "";
                Console.WriteLine(i + "." + Accessories[i] + desc);
            }
        }

        /// <summary>
        /// 把背包里的第 index 个道具装备到饰品栏。
        ///
        /// 按你的决定（方案 B）：任何「被动物件」都能装 —— 武器、防具、饰品都算。
        /// 判断依据就是实现了 <see cref="IEquippable"/>（放背包/饰品栏都生效）。
        /// 材料、消耗品这类没有被动效果的，仍然装不上去。
        /// </summary>
        public bool EquipAccessory(int index)
        {
            if (index < 0 || index >= Inventory.Count)
            {
                Console.WriteLine("没有这个道具！");
                return false;
            }

            Item item = Inventory[index];
            if (item is not IEquippable)
            {
                Console.WriteLine(item.Name + " 没有被动效果，无法装备到饰品栏。");
                return false;
            }

            if (Accessories.Count >= AccessorySlots)
            {
                Console.WriteLine($"饰品栏已满（{AccessorySlots} 格）！");
                return false;
            }

            Inventory.RemoveAt(index);
            Accessories.Add(item);
            Console.WriteLine("你把 " + item.Name + " 装备到了饰品栏。");

            // 被动物件会加属性，装/卸之后要重算，否则属性不会立刻变化
            ApplyEquipmentPassives();
            RefreshStatus();
            return true;
        }

        /// <summary>
        /// 把饰品栏里的第 index 个道具卸回背包。
        /// </summary>
        public bool UnequipAccessory(int index)
        {
            if (index < 0 || index >= Accessories.Count)
            {
                Console.WriteLine("饰品栏没有这个位置！");
                return false;
            }

            Item item = Accessories[index];
            Accessories.RemoveAt(index);
            Inventory.Add(item);
            Console.WriteLine("你把 " + item.Name + " 放回了背包。");

            ApplyEquipmentPassives();
            RefreshStatus();
            return true;
        }

        private void OpenItems(List<Monster> monsters)
        {
            int count = 0;
            foreach (Item i in Inventory)
            {
                if (i is IUseable)
                {
                    Console.WriteLine(count + "." + i + "[可使用]");
                    count++;
                }
                else
                {
                    Console.WriteLine(count + "." + i);
                    count++;
                }
            }

            if (Inventory.Count == 0)
            {
                // 空背包没有任何合法编号，重问会死循环
                Console.WriteLine("你的背包空空如也！");
                return;
            }

            // 选错道具不再结束回合：重问直到选到合法编号为止
            int choice;
            while (true)
            {
                Console.WriteLine("请选择道具使用");
                choice = ReadInt("选择道具编号(-1即退出)");
                if (choice == -1)
                {
                    // 直接返回。原来是 break，但那样 choice 仍是 -1，
                    // 紧接着的 Inventory[choice] 就会越界访问。
                    return;
                }
                if (choice >= 0 && choice < Inventory.Count) break;
                Console.WriteLine("您的使用无效！请重新选择(0-" + (Inventory.Count - 1) + "):");
            }

            Item item = Inventory[choice];
            if (item is IUseable useable)
            {
                useable.Use(this, monsters);
            }
            else
            {
                Console.WriteLine("您的使用无效！");
            }
        }

        /// <summary>
        /// 每个玩家回合结算一次「背包里所有与回合数有关的道具」：
        /// ITurnCount（如木头在熔岩瀑布烧成碳）先推进计数器，
        /// IChangeable 再判断是否该转变/移除，需要转变的产物统一在遍历结束后入包，
        /// 避免边遍历边修改 Inventory。
        /// </summary>
        private void HandleChangeableItems(EnvType envType)
        {
            List<Item> toAdd = [];
            for (int i = 0; i < Inventory.Count; i++)
            {
                Item item = Inventory[i];
                if (item is ITurnCount turnCount)
                {
                    turnCount.OnBattleStart(this, envType);
                }
                if (item is IChangeable c)
                {
                    if (c.ShouldRemove())
                    {
                        Item replacement = c.Become();
                        if (replacement != null)
                        {
                            toAdd.Add(replacement);
                        }
                        Inventory.RemoveAt(i);
                        i--;
                    }
                }
            }
            foreach (Item newItem in toAdd)
            {
                AddItem(newItem);
            }
        }

        public int CountItemById(int id) => Inventory.Where(item => item.Id == id).Sum(item => item.Count);

        /// <summary>
        /// 从背包扣除指定 ID 的若干个道具（跨多个堆叠扣），数量归零的堆叠直接移除。
        /// 调用前先整体校验总量，避免扣到一半发现不够、留下「扣了一半」的状态。
        /// </summary>
        public void ConsumeItem(int id, int amount)
        {
            // 先检查总数量是否足够
            if (CountItemById(id) < amount)
            {
                Console.WriteLine("道具数量不足！");
                return;
            }
            int remaining = amount;
            for (int i = 0; i < Inventory.Count && remaining > 0; i++)
            {
                Item item = Inventory[i];
                if (item.Id == id)
                {
                    int deduct = Math.Min(item.Count, remaining);
                    item.Count -= deduct;
                    remaining -= deduct;
                    if (item.Count <= 0)
                    {
                        Inventory.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public string GetItemNameById(int id)
        {
            foreach (Item item in Inventory)
            {
                if (item.Id == id)
                {
                    return item.Name;
                }
            }
            return "null";
        }

        public void UnlockAllRecipes()
        {
            if (!_debug)
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
            Recipes.Add(new Recipe("热硫",
                [[ItemIds.WhitePhosphorus, 1], [ItemIds.Charcoal, 1], [ItemIds.Sulphur, 1]],
                new FushionSluphur(1)));
            Recipes.Add(new Recipe("玻璃罐",
                [ [ItemIds.Sand, 3]],
                new GlassJar(1)));

            // 水瓶：1 玻璃罐（按你的决定，文档原本写的是「水淹洞穴空房间掉落」）
            Recipes.Add(new Recipe("水瓶",
                [[ItemIds.GlassJar, 1]],
                new WaterBottle(1)));
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
                new Pickaxe(1)));

            // 碘片：碘 + 红磷 + 水瓶
            Recipes.Add(new Recipe("碘片",
                [[ItemIds.Iodine, 1], [ItemIds.RedPhosphorus, 1], [ItemIds.WaterBottle, 1]],
                new IodineTablet(1)));

            // 金属罐：金属 + 白磷
            Recipes.Add(new Recipe("金属罐",
                [[ItemIds.Metal, 1], [ItemIds.WhitePhosphorus, 1]],
                new MetalCan(1)));

            // 铁剑：2 木头 + 2 金属
            Recipes.Add(new Recipe("铁剑",
                [[ItemIds.Wood, 2], [ItemIds.Metal, 2]],
                new IronSword(1)));

            // 磷化剑：铁剑 + 2 白磷 + 2 红磷
            Recipes.Add(new Recipe("磷化剑",
                [[ItemIds.IronSword, 1], [ItemIds.WhitePhosphorus, 2], [ItemIds.RedPhosphorus, 2]],
                new PhosphorusSword(1)));

            // 盔甲：3 金属 + 1 白磷
            Recipes.Add(new Recipe("盔甲",
                [[ItemIds.Metal, 3], [ItemIds.WhitePhosphorus, 1]],
                new Armor(1)));

            // 盾：3 金属
            Recipes.Add(new Recipe("盾",
                [[ItemIds.Metal, 3]],
                new Shield(1)));

            // 硫酸：水瓶 + 热硫（文档第九章配方 1；开采效果仍是占位）
            Recipes.Add(new Recipe("硫酸",
                [[ItemIds.WaterBottle, 1], [ItemIds.FushionSluphur, 1]],
                new SulfuricAcid(1)));

            // 放射金属罐：2 放射矿 + 金属罐
            // 注意：放射性矿物（ID 11）目前没有获取途径（Mine / 放射性区域都没做），
            // 所以这条配方现在只能靠调试拿到材料才合得出来。
            Recipes.Add(new Recipe("放射金属罐",
                [[ItemIds.RadioactiveOre, 2], [ItemIds.MetalCan, 1]],
                new RadioactiveMetalCan(1)));
        }

        public void HandCraft()
        {
            Console.WriteLine("====徒手合成====");
            Calculator.Delay(250);
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
                Console.WriteLine("选择无效! 请重新选择(0-" + (available.Count - 1) + ", 255 返回)");
            }
            available[choice].Craft(this);
            HandCraft();
        }
        /*
        * 后面是地图逻辑
        * */
        // 手动指定起始坐标进入地图
        // 注意：这里只定位并标记到达，不触发 OnEnter；房间事件由调用方（StartGame）在展示地图后触发。
        public void EnterMap(Room[][] map, int startRow, int startCol)
        {
            _map = map;
            PlayerRow = startRow;
            PlayerCol = startCol;
            map[PlayerRow][PlayerCol].MarkArrived();
        }

        // 自动寻找 StartRoom 进入地图
        public void EnterMap(Room[][] map)
        {
            _map = map;
            for (int y = 0; y < map.Length; y++)
            {
                for (int x = 0; x < map[0].Length; x++)
                {
                    if (map[y][x] is StartRoom)
                    {
                        PlayerRow = y;
                        PlayerCol = x;
                        map[y][x].MarkArrived();
                        return;
                    }
                }
            }
            throw new InvalidOperationException("地图中没有起始房间！");
        }

        public void ShowAvailableDirections()
        { //检查可以走的方向
            Console.WriteLine("可行动方向：");
            // 上
            if (PlayerRow - 1 >= 0 &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Up) &&
                    _map[PlayerRow - 1][PlayerCol] != null)
            {
                Console.WriteLine("0.上");
            }
            // 下
            if (PlayerRow + 1 < _map.Length &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Down) &&
                    _map[PlayerRow + 1][PlayerCol] != null)
            {
                Console.WriteLine("1.下");
            }
            // 左
            if (PlayerCol - 1 >= 0 &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Left) &&
                    _map[PlayerRow][PlayerCol - 1] != null)
            {
                Console.WriteLine("2.左");
            }
            // 右
            if (PlayerCol + 1 < _map[0].Length &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Right) &&
                    _map[PlayerRow][PlayerCol + 1] != null)
            {
                Console.WriteLine("3.右");
            }
        }

        public bool Move(Direction direction)
        {
            if (!Enum.IsDefined(direction))
            {
                Console.WriteLine("无效方向！");
                return false;
            }
            Room current = _map[PlayerRow][PlayerCol];
            if (!current.HasDoor(direction))
            {
                Console.WriteLine("这个方向没有门！");
                return false;
            }

            int newRow = PlayerRow, newCol = PlayerCol;
            switch (direction)
            {
                case Direction.Up: newRow--; break; // 上
                case Direction.Down: newRow++; break; // 下
                case Direction.Left: newCol--; break; // 左
                case Direction.Right: newCol++; break; // 右
            }

            // 边界检查
            if (newRow < 0 || newRow >= _map.Length || newCol < 0 || newCol >= _map[0].Length)
            {
                Console.WriteLine("撞墙了！");
                return false;
            }

            // 离开旧房间（清除玩家标记，触发 OnLeave）
            current.LeaveSetup(this);

            // 更新坐标
            PlayerRow = newRow;
            PlayerCol = newCol;

            // 只标记「玩家已到达」，不在这里触发 OnEnter。
            // 事件改由 StartGame 在打印地图之后调用，输出顺序变为：地图 → 房间类型 → 房间事件。
            _map[PlayerRow][PlayerCol].MarkArrived();

            return true;
        }
        // ========== 加入地图（自动寻找 StartRoom） ==========
        public void JoinMap(Room[][] map)
        {
            _map = map;
            for (int y = 0; y < map.Length; y++)
            {
                for (int x = 0; x < map[0].Length; x++)
                {
                    if (map[y][x] is StartRoom)
                    {
                        PlayerRow = y;
                        PlayerCol = x;
                        // 只标记到达；起始房间的事件由 StartGame 在打印第一张地图后触发
                        map[y][x].MarkArrived();
                        return;
                    }
                }
            }
            throw new InvalidOperationException("地图中没有起始房间！");
        }

        /// <summary>刷新状态栏。战斗时会追加血量/防御/护盾/力量。</summary>
        private void RefreshStatus()
        {
            if (_screen == null) return;
            string s = _statusHeader;
            if (_inBattle)
            {
                // 护盾按 F1 保留一位小数：它是 double，且装备加成将来可能是小数（如盾 +5.0）
                s += "\n血量:" + Hp.ToString("F1") + "  防御:" + Def.ToString("F1") + "  护盾:" + Shield.ToString("F1");
                // 显示 STR（力量）而不是最终的攻击力：攻击力 = 2 + STR × √ATK 是派生值，
                // 而 STR 才是环境（如熔岩瀑布缺水）与装备真正在改的东西，
                // 看它才知道「自己是被削了还是被加强了」。
                s += "\n力量:" + Str.ToString("F1");
                // s += "\n攻击力:" + Atk.ToString("F1");
            }
            Environment env = CurrentEnvironment;
            if (env != null) s += "\n" + env.DescribeStatus();
            _screen.DrawStatus(s);
        }

        /// <summary>
        /// 把战场上的敌人列表喂给左列顶部的「敌人框」。
        ///
        /// 格式：每行 "[i] 僵尸(30.0)"，最多 <see cref="ConsoleScreen.EnemyRows"/> 行；
        /// 不足的行由 DrawEnemies 自己补空，超出的行会被框裁掉（设计上最多同时 3 个敌人）。
        /// 行尾的空格要 TrimEnd：Monster.ToString() 结尾自带一个空格（那是给
        /// SelectTarget 的 "0. 僵尸(30.0)   1. ..." 横向排列用的），框内显示不需要它。
        /// </summary>
        private void RefreshEnemyBox(List<Monster> monsters)
        {
            if (_screen == null) return;

            List<string> lines = [];
            for (int i = 0; i < monsters.Count && i < ConsoleScreen.EnemyRows; i++)
            {
                lines.Add("[" + i + "] " + monsters[i].ToString().TrimEnd());
            }
            _screen.DrawEnemies(string.Join("\n", lines));
        }

        /// <summary>
        /// 把玩家身上的 buff 喂给右列的「Buff 框」（状态框正下方）。
        ///
        /// 格式：每行一条 "名称: 每回合X伤害 · 剩余Y回合"。
        /// 例：流血 7 点、剩 2 回合 → <c>流血: 每回合7伤害 · 剩余2回合</c>
        /// 为什么这么排：玩家看 buff 只想一次看懂三件事 ——
        ///   **什么效果 / 每回合多少 / 还剩多久**。
        /// 名称放最前（一眼扫到种类），两个数字带上单位（伤害/回合）并排，
        /// 中间用 " · " 分隔，避免 "7伤害 2回合" 这种两个数字连在一起被看成一串。
        ///
        /// 注意不打印「上限 10」这类规则说明：那是设计文档的内容，
        /// 框里只放当前状态，规则说明文字会挤掉真正会变的数字。
        ///
        /// 空 buff 时传空字符串 —— DrawBuffs 会把整框画成空白（与敌人框清空同一口径）。
        /// </summary>
        private void RefreshBuffBox()
        {
            if (_screen == null) return;

            List<string> lines = [];
            for (int i = 0; i < Buffs.Count && i < ConsoleScreen.BuffRows; i++)
            {
                Buff b = Buffs[i];
                lines.Add(b.Name + ": 每回合" + b.Stacks + "伤害 · 剩余" + b.RemainingTurns + "回合");
            }
            _screen.DrawBuffs(string.Join("\n", lines));
        }

        /// <summary>
        /// 进入/离开战斗。战斗状态会影响状态栏内容；离开战斗时顺手清空敌人框。
        ///
        /// 「战斗结束清空敌人框」为什么放在这里 —— Tools.RunBattle 用 try/finally
        /// 保证无论怎么退出都会走 SetInBattle(false)，在 Player 这一侧清空能覆盖
        /// 全部战斗入口（FightRoom / BossRoom / DebugMenu），不必去动 Tools.cs。
        ///
        /// 不清的后果：最后一名敌人被打死那次，OnPlayerTurn 里的 RefreshEnemyBox
        /// 不会再被调用（怪死光后循环直接 break），框里会残留一个 0 血的敌人。
        /// </summary>
        public void SetInBattle(bool v)
        {
            _inBattle = v;
            if (v)
            {
                ApplyEquipmentPassives();
            }
            else
            {
                _screen?.DrawEnemies("");         // 空串 = 敌人框回到 3 行空白
                _screen?.DrawBuffs("");           // 同理清空 Buff 框（Tools.ClearBuffs 也会清掉 Buffs 列表）
            }
            // 进/出战斗都要重算 STR：水量的修正、buff 的修正都得反映进来
            RecomputeStr();
            RefreshStatus();
        }

        /// <summary>
        /// 把背包里所有被动装备（Equippable）的加成应用到玩家身上。
        ///
        /// 关键：先从基础值重算，再加装备加成。
        /// 如果直接 Atk += 加成，打第二场战斗时会再叠一次，10 场之后 ATK 就爆了。
        /// 所以这里先还原成「基础值」，再让每件装备各加一次。
        /// </summary>
        private void ApplyEquipmentPassives()
        {
            // 还原基础值（这些是 Player 的初始属性，见字段声明处）
            Atk = BaseAtk;
            Def = BaseDef;
            // 护盾也一起归零：它是「盾牌」这类被动道具的加成，与 Atk/Def 同属重算类数值。
            // （CONVENTIONS 2.3：重算类必须每场战斗都从基础值重来，否则打第二场会再叠一次。
            //   影响面：每场战斗开始时清空战斗外攒下的护盾 —— 盾本来就是战斗内资源，这是预期行为。）
            Shield = 0;

            // 背包里的被动道具
            foreach (Item item in Inventory)
            {
                if (item is IEquippable eq)
                {
                    eq.ApplyPassive(this);
                }
            }

            // 饰品栏里的被动道具（设计文档 7 节：放在背包或饰品栏中即可生效）
            foreach (Item item in Accessories)
            {
                if (item is IEquippable eq)
                {
                    eq.ApplyPassive(this);
                }
            }
        }

        /// <summary>玩家基础攻击力（不含装备加成）。</summary>
        private const double BaseAtk = 20;

        /// <summary>玩家基础防御力（不含装备加成）。</summary>
        private const double BaseDef = 5;

        /// <summary>
        /// 用分区布局重画「地图」与「状态」两块固定区域。
        /// 注意这里不再清屏：地图固定在屏幕上方，日志在下方滚动，两者互不影响。
        /// </summary>
        private void DrawScreen(ConsoleScreen screen, Room[][] map, TreeMazeGenerator tmg)
        {
            screen.DrawMap(tmg.ToDisplayString());

            // 位置/房间这两行是状态栏的固定部分，缓存起来供战斗中刷新时复用
            // 「当前位置」已按要求去掉：地图上玩家所在格子本来就有 * 标记，再报一遍坐标是冗余。
            _statusHeader = "当前房间:" + map[PlayerRow][PlayerCol].Type;

            RefreshStatus();
        }

        public void StartGame(Room[][] map, TreeMazeGenerator tmg)
        {
            JoinMap(map);

            // 地图文本的行数/列数决定地图区的大小（行数 = rows*2+1，列数算最长一行）
            string[] mapLines = tmg.ToDisplayString().Replace("\r", "").Split('\n');
            int mapColumns = mapLines.Max(line => line.Length);
            ConsoleScreen screen = new ConsoleScreen(mapLines.Length, mapColumns);
            screen.Install();
            _screen = screen;              // 供 RefreshStatus / ReadInt 使用
            Console.WriteLine("游戏开始！输入方向探索地图。");

            // 开局解锁「配方0」（热硫 + 玻璃罐）。
            // 历史：这个方法原先只在一个已删除的调试入口里被调用，主游戏从来没解锁过任何配方，
            // 导致玩家选「4.徒手合成」永远只会看到「你没有任何道具合成!」。
            // 放在 Install 之后，提示才会进日志区（放前面会被 Install 的清屏抹掉）。
            AddDefaultRecipes();
            // 这样玩家先看到自己身处何地，再看到房间里发生了什么。
            bool pendingEnter = true;    // 起始房间的事件也要等第一张地图
            bool justMoved = false;

            try
            {
                while (IsAlive)
                {
                    // ---- 1. 地图 + 状态（分区绘制，不会影响日志区）----
                    DrawScreen(screen, map, tmg);
                    if (justMoved)
                    {
                        Console.WriteLine("你移动到了新房间。");
                        justMoved = false;
                    }

                    // ---- 2. 房间事件（宝箱、战斗等）----
                    if (pendingEnter)
                    {
                        pendingEnter = false;
                        map[PlayerRow][PlayerCol].OnEnter(this);
                    }

                    // ---- 3. 事件可能改变了状态（如水量），重画一次 ----
                    DrawScreen(screen, map, tmg);

                    ShowAvailableDirections();

                    // 输入框里也提示一次可走的方向，别让玩家只看到日志区在滚
                    _screen?.SetPrompt("选择方向（0上 1下 2左 3右，4 退出）");
                    if (!int.TryParse(Console.ReadLine(), out int direction))
                    {
                        Console.WriteLine("输入无效，请输入数字。");
                        continue;
                    }
                    if (direction == 4)
                    {
                        Console.WriteLine("游戏结束，感谢游玩！");
                        break;
                    }

                    if (Move((Direction)direction))
                    {
                        pendingEnter = true;
                        justMoved = true;
                    }
                }
            }
            finally
            {
                // 离开地图时把 Console.Out 还给真实输出，否则主菜单的输出会跑进日志区
                screen.Uninstall();
                _screen = null;
            }
        }
    }
}
