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

        // ==================== 基础值（永久成长的载体） ====================
        // 本项目把「会变的数值」分成两类，存放与改写方式完全不同（CONVENTIONS 2.3）：
        //   派生值 Str / Atk / Def —— 由来源决定，永远等于「基础值 + 环境 + buff + 装备」，
        //                             任何时刻都能从基础值重算（RecomputeStats），
        //                             所以**直接改它们没有意义**（下次重算就没了）；
        //   资源   Hp / Shield     —— 会被消耗（掉血 / 吃盾），**绝不能进重算通路**
        //                             （重算等于回满，是静默的无限治疗）。
        // 于是每个派生属性配一个 BaseXxx：想永久变强就改基础值（GrowBaseXxx），
        // 想临时变强就提供修正量（环境 StrModifier / Buff.StrBonus 等）。
        // 基础值一律 private set，出口只有下面那几个 Grow 方法 ——「直接改派生值」这种写法
        // 编译得过、运行不报错，只会静默失效（金属的 +1 DEF 原先就是这么被冲掉的）。

        /// <summary>
        /// 基础 HP（设计文档 7.3：200）。它是**资源的上限**而不是派生值 ——
        /// 不参与 <see cref="RecomputeStats"/>（那会把已经掉掉的血回满）。
        /// 永久加生命值走 <see cref="GrowBaseHp"/>。
        /// </summary>
        public double BaseHp { get; private set; } = 200;

        /// <summary>
        /// 基础 STR（设计文档 7.3：3）。这是角色的**固有属性**，是「永久成长」的载体
        /// —— 如通关奖励、永久强化道具，将来都走 <see cref="GrowBaseStr"/> 改它。
        ///
        /// 为什么不是 const：const 是编译期常量，谁也改不了，
        /// 那样「永久 +STR 的道具」就无处安放。
        /// 为什么不是 static：每个 Player 实例各自一份。static 会让
        /// 多场对局（或调试玩家 + 正式玩家）共用同一个值而互相干扰。
        /// </summary>
        public double BaseStr { get; private set; } = 3.0;

        /// <summary>基础 ATK（设计文档 7.3：20；攻击力公式 2 + STR × √ATK 里的 ATK）。</summary>
        public double BaseAtk { get; private set; } = 20;

        /// <summary>基础 DEF（设计文档 7.3：5；伤害减免 RealDmg = Basic - 1 × DEF 里的 DEF）。</summary>
        public double BaseDef { get; private set; } = 5;

        // 派生值：由 RecomputeStats 一处写入。
        // Str 连 setter 都不对外开放；Atk / Def 的 setter 仍是 public —— 因为装备的被动加成
        // 走 IEquippable.ApplyPassive(Player p)（道具自己 p.Atk += 加成），收窄成 internal
        // 在同一程序集里也挡不住误用，而改那个接口签名属于「改 public API」，
        // 按 CONVENTIONS 1.2 要先问。真正的纪律写在 RecomputeStats 的注释里。
        public double Atk { get; set; }
        public double Def { get; set; }

        /// <summary>
        /// 当前有效 STR = 基础 STR + 环境修正 + buff 修正（由 <see cref="RecomputeStats"/> 写入）。
        ///
        /// 只读（private set）：外部想改 STR 必须二选一 ——
        ///   临时效果 → 走环境 StrModifier / buff StrBonus；
        ///   永久成长 → 走 GrowBaseStr。
        /// 直接把 Str 设成某个值没有意义：下一次 RecomputeStats 就会把它冲掉
        /// （debug 的 127 曾因此只能生效一回合）。
        ///
        /// 伤害计算读的是这个值，不是 BaseStr。
        /// </summary>
        public double Str { get; private set; }

        /// <summary>
        /// 当前血量（**资源**：会被扣，不参与重算 —— 见本区块开头的说明）。
        /// </summary>
        public double Hp { get; set; }

        /// <summary>
        /// 构造函数：给出资源与派生值的初值。
        ///
        /// 为什么不能写成 `public double Hp { get; set; } = BaseHp;`：
        ///   C# 的属性初始化器不能引用同一个实例上的非静态成员（已实测报 CS0236
        ///   「字段初始值设定项无法引用非静态字段、方法或属性」）。原来能写
        ///   `= BaseAtk` 只是因为那时 BaseAtk 还是 const（编译期常量）。
        ///   基础值改成「可变的实例属性」之后，初值只能改在这里给。
        /// </summary>
        public Player()
        {
            Hp = BaseHp;
            // 派生值先算一次：此时没有装备、没有 buff、环境为 null，
            // 结果就是设计文档的初始值 STR 3 / ATK 20 / DEF 5。
            // （改造前 Str 的初值是 0，要等进战斗第一次重算才变成 3；
            //   0 从来没有被任何显示或计算读到过 —— 状态栏的「力量」只在战斗中显示 ——
            //   所以这次顺手改成了正确的初值，不改变任何可观察行为。）
            RecomputeStats();
        }

        /// <summary>
        /// 永久提升基础 STR（通关奖励 / 永久强化道具用），并立刻重算派生值。
        /// 这是「永久成长」的唯一入口 —— 直接写 Str 会在下次重算时被冲掉。
        /// 传负数即为永久降低。
        /// </summary>
        public void GrowBaseStr(double amount)
        {
            BaseStr += amount;
            RecomputeStats();
        }

        /// <summary>
        /// 永久提升基础 ATK（同 <see cref="GrowBaseStr"/>：改的是基础值，不是派生的 Atk）。
        /// 目前没有调用者（还没有「永久 +ATK」的道具/奖励），与 GrowAccessorySlots 一样
        /// 先把「永久成长」的出口留好，免得将来又有人去直接改 Atk。
        /// </summary>
        public void GrowBaseAtk(double amount)
        {
            BaseAtk += amount;
            RecomputeStats();
        }

        /// <summary>
        /// 永久提升基础 DEF。金属的「消耗 1 块永久 +1 DEF」走这条路（见 <see cref="Items.Metal"/>）——
        /// 改造前它直接写 p.Def += 1，而每回合开头的护盾重置会顺带把 Def 还原成基础值，
        /// 于是那 1 点 DEF 在下一回合开头就没了：不报错、不崩溃，只是静默失效。
        /// </summary>
        public void GrowBaseDef(double amount)
        {
            BaseDef += amount;
            RecomputeStats();
        }

        /// <summary>
        /// 永久提升基础 HP（将来「通关奖励 +生命值」用）。
        /// 顺手把当前血量也抬同样多：BaseHp 是血量上限，只加上限不加当前血量，
        /// 奖励看起来会像没生效（这与 Shield 是同一个「资源不能重算」的道理）。
        /// 目前没有调用者；传负数即为降低上限（当前血量也会跟着降，调用方自己保证别把玩家扣死）。
        /// </summary>
        public void GrowBaseHp(double amount)
        {
            BaseHp += amount;
            Hp += amount;
        }

        /// <summary>直接把基础 STR 设成某个值（调试用，避免写 GrowBaseStr(目标 - BaseStr) 这种别扭算式）。</summary>
        public void SetBaseStr(double value)
        {
            BaseStr = value;
            RecomputeStats();
        }

        public string Name { get; set; }

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

        /// <summary>
        /// 累加跳过回合数（与 <see cref="Monsters.Monster.AddSkipTurn"/> 对称，见 CONVENTIONS 2.3）。
        /// 为什么要有这个方法：<c>SkipTurn</c> 是**累加语义**的量（连中两次「困住」就该被跳两回合），
        /// 而它原先只由调用点裸写 <c>p.SkipTurn = 1</c> —— 那种写法是**赋值**：连中两次也只剩 1，
        /// 而且一旦将来第二个来源也来写，两边会互相覆盖。出口统一成 AddXxx 之后，
        /// 「谁改过它」不再重要，值永远等于各方增量之和。
        /// 战斗结束时由 Tools.RunBattle 的 finally 清零（跨战斗残留是另一个 bug，见那里）。
        /// </summary>
        public void AddSkipTurn(int count) => SkipTurn += count;
        public bool IsDead { get; set; }
        private Room[][] _map;      // 当前地图
        public int PlayerRow { get; private set; }     // 玩家所在行
        public int PlayerCol { get; private set; }     // 玩家所在列

        // 这里原先有一个 private bool _debug —— 它只在 255 里被写、只有 UnlockAllRecipes 读，
        // 与主菜单（GameLobby）里那个同名字段是两份互不相通的状态。
        // 现在调试开关统一为 DebugState.Enabled（见 DebugState 的说明）。

        /// <summary>
        /// 输入流是否已经结束（EOF）。
        ///
        /// 为什么需要它：<c>Console.ReadLine()</c> 在管道关闭 / 重定向读完 / Ctrl+Z 之后
        /// 会一直返回 null。这时任何「重问直到输入合法」的循环都不再读得到输入，
        /// 只会空转刷屏。ReadInt 读到 null 就置位本标志，让上层（战斗循环、开局方向循环）
        /// 能分辨「玩家输错了」（继续重问）和「输入已经没了」（必须收场）。
        /// </summary>
        private bool _inputClosed;

        /// <summary>
        /// 输入流是否已结束（EOF）。见 <see cref="_inputClosed"/>。
        /// 战斗循环用它决定「还要不要继续问玩家要操作」。
        /// </summary>
        public bool InputClosed => _inputClosed;

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
                    RecomputeStats();      // buff 可能改属性，立刻重算
                    RefreshBuffBox();      // 叠加会改伤害/剩余回合，框要立刻跟上
                    return;
                }
            }
            Buffs.Add(b);
            RecomputeStats();              // buff 可能改属性，立刻重算
            RefreshBuffBox();
        }

        /// <summary>
        /// 重算所有派生值：STR / ATK / DEF（CONVENTIONS 2.3 的「重算」语义）。
        ///
        /// 为什么必须「从基础值重算」，而不是谁想改就 -= / += ：
        ///   改这些值的来源有好几个（环境、buff、装备）。用加减法的话，
        ///   每个来源都得自己负责「什么时候加回来」—— buff 有三条移除路径
        ///   （自然到期 / 战斗结束 ClearBuffs / 将来的驱散），漏掉任何一条，
        ///   属性就永久偏移一点，而且不报错、不崩溃，只是「打着打着变弱了」。
        ///   重算没有这些问题：不管谁改了什么，值永远等于「基础 + 当前所有修正之和」。
        ///
        /// ⚠️ 三种修正的**叠加顺序不能改**：装备必须放最后。
        ///   装备的通道是 IEquippable.ApplyPassive(p)，道具自己写 `p.Atk += 加成`
        ///   —— 是累加式，必须在上面把 Atk/Def 赋成基础值**之后**才能加；
        ///   否则打第二场会再叠一次，十场之后 ATK 就爆了。
        ///   环境与 buff 提供的是「修正量」（返回增量，不写结果），谁先谁后只受加法交换律影响，
        ///   放在前面读起来更接近「基础 + 各方修正」这句话。
        ///
        /// ⚠️ 这里**不碰** Hp / Shield：它们是资源，会被消耗（掉血 / 吃盾），
        ///   进来重算等于回满 —— 那是静默的无限治疗。护盾的每回合重置是另一条通路
        ///   （见 <see cref="ResetShieldForTurn"/>）。
        /// </summary>
        public void RecomputeStats()
        {
            // 1. 从基础值重来（「先重置再应用」，见 CONVENTIONS 2.3）
            Str = BaseStr;
            Atk = BaseAtk;
            Def = BaseDef;

            // 2. 环境修正（如熔岩瀑布缺水扣 STR）。
            //    环境目前只声明 STR 的修正量；将来若有环境要改 ATK/DEF，
            //    照 StrModifier 的样子加一个虚属性，在这里读一行即可。
            Environment env = CurrentEnvironment;
            if (env != null) Str += env.StrModifier;

            // 3. buff 修正（如某个增益 +1 STR）。buff 消失时不需要谁去「加回来」：
            //    下次重算就是「基础 + 当前还在的修正」。
            foreach (Buff b in Buffs)
            {
                Str += b.StrBonus;
                Atk += b.AtkBonus;
                Def += b.DefBonus;
            }

            // 4. 装备加成 —— 必须是最后一步，理由见本方法注释
            ForEachEquippedItem(item =>
            {
                if (item is IEquippable eq) eq.ApplyPassive(this);
            });
        }

        /// <summary>
        /// 遍历玩家身上所有「生效中的被动物件」：背包 + 饰品栏
        /// （设计文档 7 节：放在背包或饰品栏里即生效）。
        ///
        /// 为什么抽成一个方法：派生属性加成（IEquippable.ApplyPassive）与护盾加成
        /// （IShieldSource.ApplyShield）**两条通道必须扫同一批道具** —— 各写一遍遍历，
        /// 早晚会漏掉一处（例如忘了饰品栏，那时「放进饰品栏的盔甲不加 DEF」不报错、
        /// 不崩溃，只是安静地少一点属性）。
        /// </summary>
        private void ForEachEquippedItem(Action<Item> action)
        {
            foreach (Item item in Inventory) action(item);
            foreach (Item item in Accessories) action(item);
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

            // 护盾是「本回合」的资源，不跨回合累积 —— 回合一开始先清空，
            // 再由装备被动（盾牌 +5）和本回合的「防御」动作重新攒。
            // 放在 RefreshStatus 之前，状态栏显示的才是重置后的值，而不是上回合残留。
            ResetShieldForTurn();

            // 环境状态（水量、水位）每回合都在变，所以每回合都要重算派生值，
            // 不能只在进战斗时算一次 —— 否则缺水削弱要等到下一场战斗才生效。
            //
            // ⚠️ RecomputeStats 必须在 RefreshStatus **之前**（本次修掉的一个真 bug）：
            //   本回合的环境推进（Tools 里的 env?.OnBattleTurn()）在 OnTurnStart 之前就已经跑完了，
            //   但原先的顺序是 ResetShieldForTurn → RefreshStatus → RecomputeStats ——
            //   状态栏读到的还是**上一回合**的力量/防御，于是熔岩瀑布每回合掉水后
            //   「力量」落后一跳（0.05~0.10），要等玩家挨打触发 TakeDamage 里的刷新才被覆盖。
            //   把重算提到刷新之前，状态栏这一跳就即时了。
            //   （ResetShieldForTurn 仍留在最前：盾是资源、必须最先重置；
            //     RefreshBuffBox 仍在其后 —— 它只画 buff 框，与属性快照无关。）
            RecomputeStats();
            RefreshStatus();               // 战斗中让状态栏显示血量/防御/护盾
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
            if (DebugState.Enabled)
            {
                Console.WriteLine("debug模式操作:");
                Console.WriteLine("127, 超雄模式");
                Console.WriteLine("128, 获得全部合成");
                // 129 是新增的：战斗中当场拿道具（主菜单的「4. 获取道具」发到常驻调试玩家身上，
                // 战斗中由这里拿到正在战斗的这个玩家身上）。编号避开 127/128/255。
                Console.WriteLine("129, 获取道具");
            }
            int option = ReadInt("请选择操作");
            if (option == -1 && _inputClosed)
            {
                // 输入流已结束：本回合没法行动，直接放弃这次行动返回。
                // 不落到下面的 default —— 那句「你输入的类型不正确！」是给手输错的玩家看的，
                // EOF 不是玩家输错。战斗循环会在这一回合之后看到 InputClosed 并收场（见 Tools.RunBattle）。
                return;
            }
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
                    Console.WriteLine("你进行了防御!");
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
                // 127 / 128 / 129 是调试指令，必须**先开开关**（255）才可用。
                //
                // 为什么写成 case ... when 而不是在 case 里 if：
                //   守卫不成立时这条 case 标签就不匹配，switch 会继续往下找，
                //   最后落到 default —— 也就是和「乱输一个数字」走**完全相同**的分支
                //   （同一句「你输入的类型不正确！ 已自动跳过回合」，同样不消耗额外的东西）。
                //   刻意不为这种情况新增任何提示：否则玩家一乱按就能发现「这里藏着东西」，
                //   而改造前它们是无条件生效的（127/128/129 一个守卫生效都没有）。
                case 127 when DebugState.Enabled:
                    AddBuff(new SuperMaleBuff(1,int.MaxValue));
                    Console.WriteLine($"超雄模式：基础 STR 已设为 {BaseStr}（当前 STR {Str}）");
                    break;
                case 128 when DebugState.Enabled:
                    UnlockAllRecipes();
                    break;
                case 129 when DebugState.Enabled:
                    // 定量发放任意道具（ID → 名称 → 数量）。
                    // 复用 DebugMenu 的那张 ItemFactory 表，不在这里再抄一份 ID 列表。
                    DebugMenu.GiveItems(this);
                    break;
                case 255:
                    // 255 只负责开开关（并打印提示）—— 现在它真的有意义了：
                    // 127/128/129 与主菜单的调试项都读同一个 DebugState。
                    DebugState.Enable();
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
                // 输入流结束：没有输入就永远选不出目标，再重问就是 ReadInt 立刻返回 -1 的空转。
                // 本次攻击按取消处理（不消耗回合，也不打错目标），交给战斗循环收场。
                // 注意手输 -1 走不到这里：那时 _inputClosed 为 false，照旧重问，行为不变。
                if (_inputClosed) return;
                Console.WriteLine("您的攻击无效！请重新选择(0-" + (monsters.Count - 1) + "):");
            }

            Monster target = monsters[choice];
            target.TakeDamage(dmg);
            Console.WriteLine("你攻击了" + target + "!");

            // 武器命中特效：遍历身上（背包 + 饰品栏）所有 IOnHit 各触发一次。
            // 原先这里是 `if (target.IsAlive && HasPhosphorusSword()) { AddBuff(BleedBuff) + 播报 }`
            // —— 通用攻击流程里写着具体武器（磷化剑）的名字、数值和文案，加第二把带特效的
            // 武器就得再改这里。现在 Attack 只问「谁有命中特效」，对武器种类一无所知。
            // 目标已被这一击打死就不用挂了（挂上去也没人结算）。
            // 同一个类型只触发一次：原先的判定是「身上有没有这把剑」（HasPhosphorusSword），
            // 那是「有无」判断 —— 身上有两把磷化剑也只挂一次流血，这里保持同一口径。
            if (target.IsAlive)
            {
                List<Type> triggered = [];
                foreach (Item item in Inventory)
                {
                    TriggerOnHit(item, target, triggered);
                }
                foreach (Item item in Accessories)
                {
                    TriggerOnHit(item, target, triggered);
                }
            }
        }

        /// <summary>
        /// 让一件道具的命中特效触发一次（若它有），并保证同一个道具类型在本回合只触发一次。
        /// </summary>
        private void TriggerOnHit(Item item, Monster target, List<Type> triggered)
        {
            if (item is not IOnHit onHit) return;
            if (triggered.Contains(item.GetType())) return;
            triggered.Add(item.GetType());
            onHit.OnHit(this, target);
        }

        /// <summary>
        /// 读一个整数。输入非法时提示并重新读取，绝不抛异常。
        /// 替代原先裸用的 int.Parse(Console.ReadLine()) —— 那样在菜单里输入一个字母就会让整个游戏崩溃。
        /// prompt 会同时显示在右侧日志与输入框里（输入框是 TUI 的光标所在处）。
        ///
        /// 返回 <c>-1</c> 表示**输入流已结束（EOF）**，调用方按「取消」处理；
        /// 同时 <see cref="InputClosed"/> 会被置位，供上层（战斗循环 / 开局方向循环）收场。
        ///
        /// 为什么用 -1 当取消哨兵：各调用点的提示里本来就写着「-1取消」「-1即退出」
        /// （饰品栏、背包），-1 从来不是任何合法编号（编号一律从 0 起），所以不会与真实输入撞车，
        /// 也不需要给 5 个调用点各改一次签名。
        ///
        /// ⚠️ 空行（<c>""</c>）不是 EOF：空行按原来的「输入无效」重问 —— 那是玩家有意不选，
        /// 与「输入流没了」是两件事，不能混为一谈。
        /// </summary>
        private int ReadInt(string prompt)
        {
            while (true)
            {
                // 输入框里也显示一份提示，否则玩家只能盯着滚动日志看要输入什么
                _screen?.SetPrompt(prompt);
                string line = Console.ReadLine();
                if (line == null)
                {
                    // EOF：再循环一次 ReadLine 还是立刻返回 null，重问就是不读输入的 CPU 空转 + 刷屏。
                    // 所以这里不重问，直接把这当成「取消」交回调用方。
                    _inputClosed = true;
                    return -1;
                }
                if (int.TryParse(line, out int value))
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
                // 输入流结束：重问只会空转（ReadInt 每次都立刻返回 -1）。
                // 按本方法既有的「没有可选对象」语义返回 -1，让调用方收场。
                // 手输 -1 时 _inputClosed 为 false，照旧重问，行为不变。
                if (_inputClosed) return -1;
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
                // 输入流结束：菜单再问也读不到输入（ReadInt 会一直立刻返回 -1），
                // 直接退出饰品栏菜单，等价于玩家选了「0.返回」。手输 -1 时 _inputClosed 为 false，
                // 仍按原来的「什么都不做、重画菜单」处理，交互不变。
                if (_inputClosed) return;

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
                    if (pick == -1)
                    {
                        // 输入流结束：不再重画菜单空转，直接退出饰品栏
                        if (_inputClosed) return;
                        continue;
                    }
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
                    if (pick == -1)
                    {
                        // 输入流结束：不再重画菜单空转，直接退出饰品栏
                        if (_inputClosed) return;
                        continue;
                    }
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
        ///
        /// ⚠️ 背包满时**拒绝**卸下，而不是照旧塞进去或静默丢弃（本次修掉的一个真 bug）：
        ///   原先这里直接 <c>Inventory.Add(item)</c>，完全不看容量 ——
        ///   背包 15/15 时卸一个饰品就变成 16 格，容量上限形同虚设
        ///   （<see cref="AddItem"/> 的容量检查只保护了「进包」的另一条路径）。
        ///   为什么选「拒绝」而不是走 AddItem（满了就丢）：
        ///     走 AddItem 的话，玩家手上这件装备会在「卸下」这个动作里被**静默销毁** ——
        ///     卸装备的本意是把它收好，丢掉装备是玩家绝不会预期的结果，
        ///     而且丢失无声无息（只有一行「被丢弃了」混在滚动日志里），比多占一格严重得多。
        ///   为什么也不选「不让卸」的另一种写法（例如卸下时把背包整屏锁住）：
        ///     那样玩家在战斗中等于被永久锁死饰品栏。提示一句、让玩家先腾一格最直观。
        /// </summary>
        public bool UnequipAccessory(int index)
        {
            if (index < 0 || index >= Accessories.Count)
            {
                Console.WriteLine("饰品栏没有这个位置！");
                return false;
            }

            // 容量检查必须在「从 Accessories 移除」之前：否则检查失败时物品已经被摘下来了，
            // 它既不在饰品栏也不在背包里 —— 直接消失。
            if (Inventory.Count >= InventoryCapacity)
            {
                Console.WriteLine($"背包已满（{InventoryCapacity} 格），先腾出一格再卸下 {Accessories[index].Name}。");
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
                    // 输入流结束（EOF）也走这条：同样返回、不消耗回合，不会空转。
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
            // 详见 Items\ICraftable.CreateProduct 与 Tools\Recipe 构造函数的注释。
            Recipes.Add(new Recipe("热硫",
                [[ItemIds.WhitePhosphorus, 1], [ItemIds.Charcoal, 1], [ItemIds.Sulphur, 1]],
                () => new FushionSluphur(1)));
            Recipes.Add(new Recipe("玻璃罐",
                [ [ItemIds.Sand, 3]],
                () => new GlassJar(1)));

            // 水瓶：1 玻璃罐（按你的决定，文档原本写的是「水淹洞穴空房间掉落」）
            Recipes.Add(new Recipe("水瓶",
                [[ItemIds.GlassJar, 1]],
                () => new WaterBottle(1)));
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
        /// 文案由 **buff 自己**决定（<see cref="Buff.Describe"/>），这里只负责加上显示名。
        /// 例：流血 7 点、剩 2 回合 → <c>流血: 每回合7伤害 · 剩余2回合</c>
        ///     超雄综合症          → <c>超雄综合症: 力量 +1000 · 剩余N回合</c>
        ///
        /// 为什么文案下放到子类（这次踩过的坑）：
        ///   这里原先拼的是 <c>b.Name + ": " + b.Stacks + "伤害 · 剩余" + b.RemainingTurns + "回合"</c>，
        ///   其中「伤害」写死了 —— 等于假设「所有 buff 都是每回合造成伤害的」。
        ///   而 Stacks 的语义本来就由各 buff 自己定义（流血 = 每回合伤害，超雄 = 强度）。
        ///   结果第二个 buff 一挂上就显示出「超雄综合症: 每回合2147483647伤害」，它根本不扣血。
        ///
        ///   中途曾改成中性的 <c>"lvl." + b.Stacks</c>，那只是把「错的话」换成「没信息的话」：
        ///   玩家看到「lvl.2147483647」仍然不知道这是什么量。
        ///   根因不是文案不准确，而是**显示层知道了一个具体子类的语义**：
        ///   每当新增一种 buff，这里就得回来加一个 if —— 那正是该用虚方法消除的场合。
        ///   现在显示层只问「你怎么描述你自己」，对 buff 的种类与单位一无所知，
        ///   以后加第 10 个 buff（回蓝、加速、反伤……）本文件一行都不用动。
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
                lines.Add(b.Name + ": " + b.Describe());
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
            // 进/出战斗都要重算派生值：水量的修正、buff 的修正都得反映进来
            RecomputeStats();
            RefreshStatus();
        }

        /// <summary>
        /// 回合开始的护盾重置：护盾是「本回合」的资源，不跨回合累积。
        ///
        /// 为什么是「清零后让有盾的装备各加一次」而不是直接写 Shield = 0：
        ///   否则盾牌（+5）只在开战那一回合有用，之后永远为 0，等于把道具废掉。
        ///
        /// 为什么**不再**复用 ApplyEquipmentPassives（改造前就是那么写的，那是个真 bug）：
        ///   那个方法在「先把 Atk/Def 还原成基础值」的同时会把 Def 也还原掉，于是金属的
        ///   「消耗 1 块永久 +1 DEF」（Items\Metal.cs）会在下一回合开头被**静默**冲掉。
        ///   根因是两类东西被混在了一条通道里：护盾是**资源**（每回合重置，用户明确要的），
        ///   Atk/Def 是**派生值**（只在装备变化 / 开战 / 修正量变化时重算）。
        ///   现在拆开：这里只走 IShieldSource 这条「只算盾」的通道，不碰派生值。
        ///
        /// 语义（与改造前一致）：以前护盾跨回合累积（连续防御两次能攒到 10 盾），
        ///   现在防御给的 5 点只在「本回合内」有效 —— 挡怪物回合那一刀，回合结束就没了。
        /// </summary>
        private void ResetShieldForTurn()
        {
            Shield = 0;
            ForEachEquippedItem(item =>
            {
                if (item is IShieldSource shieldSource) shieldSource.ApplyShield(this);
            });
        }

        /// <summary>
        /// 让「当前装备」的被动效果在玩家身上重新生效一次（开战 / 换装 / 卸装时调用）。
        ///
        /// 关键：先重算派生值（从基础值来），再加装备加成。
        /// 如果直接 Atk += 加成，打第二场战斗时会再叠一次，10 场之后 ATK 就爆了。
        /// 所以第一步就是 RecomputeStats 里的「先还原成基础值」。
        /// 顺序也不能反：RecomputeStats 里的装备加成是 `+=`，必须在赋值之后（见其注释）。
        ///
        /// 第二步把护盾也重来一遍（清零 + 装备的护盾加成）：
        ///   ⚠️ 「换装备 / 开战时护盾清零」是**刻意的游戏设计**（CONVENTIONS 三），
        ///   不是重算派生值的副作用 —— 换装等同重新整理防御姿态，战斗外攒的盾随之失效。
        ///   这条不能挪走。
        /// </summary>
        private void ApplyEquipmentPassives()
        {
            RecomputeStats();
            ResetShieldForTurn();
        }

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
                    string line = Console.ReadLine();
                    if (line == null)
                    {
                        // 输入流结束（EOF）：方向永远读不到了。必须退出游戏循环 ——
                        // 原先下面的 continue 会立刻再读到 null，主循环变成不读输入的 CPU 空转 + 刷屏。
                        // 退出效果与玩家输入 4 相同（同一句结束语）。
                        _inputClosed = true;
                        Console.WriteLine("游戏结束，感谢游玩！");
                        break;
                    }
                    if (!int.TryParse(line, out int direction))
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
