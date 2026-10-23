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

        /// <summary>
        /// 本局累计的战斗次数（设计：每进入一场战斗 +1）。
        ///
        /// 用途：<see cref="Rooms.MobSpawner"/> 按它提高「这一战多出几只怪」的概率
        /// （0.1 + 0.01 × FightCount，Boss 战再 ×0.5）。
        ///
        /// 语义与生命周期：
        ///   · **累加**语义（CONVENTIONS 2.3），所以出口是 <see cref="AddFightCount"/>，
        ///     不在外部直接赋值；
        ///   · 每局新建 <see cref="Player"/> 时从 0 开始，**不持久化**（没有存档系统）；
        ///   · 计数点在 <see cref="SetInBattle(bool)"/> 的 `true` 分支 ——
        ///     Tools.RunBattle 每场战斗只调用一次 SetInBattle(true)（见那里的 try/finally），
        ///     所以不会重复计数；调试战斗同样算一场。
        ///   · 水淹洞穴的假人战斗也计数：它就是一场真实战斗，只是怪是占位实现。
        /// </summary>
        public int FightCount { get; private set; }

        /// <summary>战斗计数 +1，见 <see cref="FightCount"/>（累加语义用方法，CONVENTIONS 2.3）。</summary>
        public void AddFightCount() => FightCount++;

        /// <summary>
        /// 玩家受伤。
        ///
        /// 结算顺序（本次裁定）：**先 DEF 减免 →（装备减伤）→ 再吃盾**。
        /// 括号里那一步是后加的 <see cref="IDamageReduction"/> 通道（斗篷 20% 免伤），
        /// 为什么排在这里见方法体里的注释；**「DEF 先于盾」这条既有顺序没有改动**。
        /// 理由：若反过来「先吃盾再 DEF 减免」，盾会被防御白吃一部分 ——
        ///   例：打来 10 点、DEF = 5（系数 0.5，即减免 2.5）、盾 = 5。
        ///     先 DEF：10 - 2.5 = 7.5 最终伤害 → 盾抵 5 → 掉血 2.5（盾刚好用完）
        ///     先吃盾：盾 5 全吃掉 10 里的 5 → 剩 5 再过 DEF → 掉血 2.5
        ///   两者这里看似一样，但换成「盾 = 20」就露馅了：
        ///     先 DEF：10 - 2.5 = 7.5，盾剩 12.5，这刀盾只花了 7.5
        ///     先吃盾：盾按原始伤害吃满 10（盾剩 10），DEF 那 2.5 完全没轮到 ——
        ///            等于盾替 DEF 垫了钱，盾越多、DEF 越白拿
        ///   即「先吃盾」时 DEF 的减免会被盾挡住而作废；
        ///   「先 DEF」则让盾按最终伤害 1:1 抵扣，数值直观、可预期。
        ///
        /// 盾与血的关系：盾就是额外血量，扣完盾才动 Hp（见下面 AbsorbWithShield 的逻辑）。
        /// </summary>
        public void TakeDamage(double dmg)
        {
            double realDmg = Calculator.ApplyDefense(Def, dmg);

            // ── 装备减伤（IDamageReduction，如斗篷 20%）──
            // 位置：**DEF 之后、盾之前**，即它是「算最终伤害」的最后一步，和 DEF 同一段。
            // 为什么必须排在盾前面：§7.1 的口径是「盾按**最终伤害** 1:1 抵扣」——
            //   盾的每一层都不该被别的减免再削一次。减伤既然也在减少最终伤害，
            //   就必须和 DEF 一样排在盾之前，否则「打来 10 点、盾 20」时盾白丢 2 点价值。
            // 为什么不能排在 DEF 之前：DEF 走的是减法公式（RealDmg = max(dmg - 0.5 × DEF, 1)，
            //   系数 0.5 见 Calculator.DefCoefficient）。
            //   先乘 0.8 再减 DEF 会让 DEF 也少挨一份，而且小伤害会被最低 1 夹得更狠：
            //   打来 4 点、DEF = 5（减免 2.5）、20% 减伤 → 正确顺序 max(4 - 2.5, 1) = 1.5，
            //   再 ×0.8 = 1.2；先减伤则是 4 × 0.8 = 3.2 → max(3.2 - 2.5, 1) = 1 ——
            //   比该有的 1.2 还低，等于多减了不该减的那一份。
            // 这一步只做乘法，不碰 Shield、不碰 DEF、不改「DEF 先于盾」的顺序。
            realDmg *= DamageReductionMultiplier();

            if (Shield >= realDmg)
            {
                // 盾够厚：完全挡住，血量一点不掉
                Shield -= realDmg;
                Console.WriteLine("你的护盾挡下了伤害!");
                // ⚠️ 这条提前 return 上**不**通知装备「受伤了」（IOnDamaged）：本项目的口径是
                //   「真的受伤 = 血被扣了」，被盾全挡下的一跳并没有打到身上，斗篷的耐久
                //   （受伤 50 次后破损）不该因为盾厚而被消耗。realDmg 为 0（如 100% 减伤）也走这里。
                RefreshStatus();           // 护盾变化后立刻更新状态栏
                return;
            }

            // 盾不够（或没盾）：先清空盾，剩下的才扣血
            double remaining = realDmg - Shield;
            Shield = 0;
            Hp -= remaining;
            Console.WriteLine("你受到{0:F1}点伤害!", remaining);
            Console.WriteLine();

            // ── 装备的「受伤」通知（IOnDamaged，如斗篷按次数记耐久）──
            // 为什么只写在这一条分支里：这是「血真的掉了」唯一的分支 ——
            //   上面盾全挡下的那条提前 return 不发通知（口径见那里的注释）。
            //   于是「受伤一次」= 「Hp 被扣一次」，与这一跳多少点、来源是什么
            //   （普攻 / 流血 / 环境扣血 / 毒气弹自伤…）都无关：凡是最终扣到血的都算一次。
            // 为什么走能力位接口而不是 `is Cloak`：与 IDamageReduction / IShieldSource 同一思路，
            //   将来别的「挨打 N 次后爆掉 / 变身」的道具实现同一个接口即可，Player 不认识具体道具类。
            // 为什么排在 RefreshStatus 之前：通知可能让斗篷破损并被收走（装备少一件、DEF 会变），
            //   刷新放在它后面，状态栏显示的才是收走之后的防御值。
            NotifyDamaged();

            RefreshStatus();               // 受伤后立刻更新状态栏
            // 玩家身上可能有 buff 在结算中（如流血每回合走 TakeDamage），
            // 这里顺手刷新一次 Buff 框，保证「结算完的那一跳」马上反映到框里。
            RefreshBuffBox();
        }

        /// <summary>
        /// 通知所有生效中的装备「玩家这一跳真的受伤了（`Hp` 已被扣）」——
        /// 由 <see cref="TakeDamage"/> 在扣血分支里调用一次；被盾全挡下的那一跳不调用。
        ///
        /// 为什么需要这条通道：另外两条装备通道都拿不到「又挨了一下」这个**事件** ——
        ///   <see cref="IEquippable"/> 只在重算派生值时被调用（次数与挨打无关），
        ///   <see cref="IDamageReduction"/> 只回答「这一跳打几折」（可以被读很多次却一次伤害都没发生）。
        /// 斗篷(27) 的「受伤 50 次后破损」要的是次数，只能由受伤入口显式通知，见 <see cref="IOnDamaged"/>。
        ///
        /// 遍历哪批道具：走 <see cref="ForEachEquippedItem"/>（**背包 + 饰品栏**）——
        ///   与 IEquippable / IShieldSource / IDamageReduction 扫同一批，各写一遍早晚漏一处。
        ///
        /// 通知与收尸分两步：通知可能让道具把自己用坏（斗篷记满 50 次），
        ///   但**不能在遍历中改 Inventory / Accessories** —— 所以这里只记「有没有人坏掉」，
        ///   遍历结束再统一走 <see cref="RemoveBrokenEquippedItems"/>
        ///   （判定仍旧只走 IChangeable.ShouldRemove，与另外两条收尸通路同源）。
        /// </summary>
        private void NotifyDamaged()
        {
            bool broke = false;
            ForEachEquippedItem(item =>
            {
                if (item is not IOnDamaged listener) return;
                listener.OnDamaged(this);
                if (item is IChangeable c && c.ShouldRemove()) broke = true;
            });

            if (broke) RemoveBrokenEquippedItems();
        }

        /// <summary>
        /// 玩家身上所有装备提供的**总减伤系数**（0.8 = 这一跳只吃 80%），
        /// 由 <see cref="TakeDamage"/> 在 DEF 之后、盾之前乘上去。
        ///
        /// 遍历哪批道具：走 <see cref="ForEachEquippedItem"/>（**背包 + 饰品栏**）——
        ///   与 IEquippable / IShieldSource 扫同一批。各写一遍遍历早晚会漏掉一处
        ///   （漏了饰品栏时「放进饰品栏的斗篷不减免」不报错、不崩溃，只是安静地失效）。
        ///
        /// 多件怎么叠加：**连乘** —— 系数 = Π(1 - ratio)。
        ///   项目里没有同类先例（怪物那边 <c>Monster.ApplyDamageImmunity</c> 是「开打前写进怪物的
        ///   单个免疫比例」，环境只给一份，压根不存在叠加问题），所以这条是本次新定的：
        ///     · 相加：2 件斗篷 = 40%，5 件 = 100% —— 直接无敌，一件饰品就能把数值打穿；
        ///     · 连乘：2 件 = 36%，5 件 = 67%，永远到不了 100%，且「第二件收益递减」符合直觉，
        ///       不必再额外加一个上限规则兜底。
        /// 每件的比例先夹到 0..1：道具误写越界值只会被当作 0% / 100%，不会变成负系数（加伤）。
        /// </summary>
        private double DamageReductionMultiplier()
        {
            double multiplier = 1.0;
            ForEachEquippedItem(item =>
            {
                if (item is IDamageReduction reduction)
                {
                    multiplier *= 1.0 - Math.Clamp(reduction.DamageReductionRatio, 0.0, 1.0);
                }
            });
            return multiplier;
        }

        /// <summary>
        /// 周期性伤害（DOT）的受伤入口 —— **只有明确的「每回合持续扣血」结算才走这里**。
        /// 目前唯一的调用点是 <see cref="LostCompound.Buffs.BleedBuff"/> 的玩家侧 OnTurnTick。
        ///
        /// 「怎么区分 DOT 和普通攻击伤害」就是本方法存在的全部理由，三种做法的取舍：
        ///   · **另开入口（本做法）**：信息由结算点自己声明 —— 流血知道自己是一跳 DOT。
        ///     普通攻击那条路一个字都不用改；将来漏了某条 DOT，最坏结果是「那个效果没被吸收」，
        ///     不会反过来污染普通伤害的判定。
        ///   · `TakeDamage(dmg, bool isDot = false)`：默认 false 意味着**任何漏改的调用点都静默
        ///     变成「非 DOT」**。现在 p.TakeDamage 有二十来处（6 只怪物的攻击、环境判负、
        ///     毒气弹自伤、放射性矿物…），而且怪物侧还得跟着改签名 —— 风险明显更大。
        ///   · 在 Player 上放「正在结算 DOT」的开关：隐式状态，嵌套调用
        ///     （如放射性矿物在回合头扣血）会把标记串台，事后极难查。
        ///
        /// 结算顺序：**先由 IDotAbsorber 吃，剩下的才走 <see cref="TakeDamage"/>**
        /// （DEF → 装备减伤 → 盾 → 血）。被吸收掉的部分不过 DEF、不动盾 ——
        /// 「吸收」的语义就是这些伤害根本没打到身上。
        /// 吸收判定用的是**原始 DOT 值**（还没过 DEF），理由：
        ///   DEF / 盾都在 TakeDamage 内部，想先算最终伤害就得把它整套拆出来；
        ///   而这条通道的账本记的是「DOT 这一跳打来多少点」，这个口径最直观、也最好向玩家解释。
        ///
        /// ⚠️ 现状：**目前没有任何道具实现 <see cref="IDotAbsorber"/>** ——
        ///   斗篷(27) 的破损机制已按用户新裁定从「累计吸收 20 点 DOT」改成「受伤 50 次」
        ///   （走 <see cref="IOnDamaged"/>，那是**结算完之后**的事件通道，与本方法
        ///   「结算之前吃掉伤害」的口径正交，没有合并）。
        ///   所以本方法现在是一条**纯转发**（没有吸收者时 remaining 恒等于 dmg）。
        ///   它与 IDotAbsorber 的去留（保留作扩展点 / 整体删除）**待用户裁定，先原样保留**。
        /// </summary>
        public void TakeDotDamage(double dmg)
        {
            double remaining = dmg;
            bool absorberBroke = false;

            ForEachEquippedItem(item =>
            {
                if (remaining <= 0) return;                       // 已经被前面的吸收者吃光
                if (item is not IDotAbsorber absorber) return;

                // 夹进 [0, remaining]：道具误返回越界值时既不会凭空多吸收、也不会把伤害变成负数
                double absorbed = Math.Clamp(absorber.AbsorbDot(this, remaining), 0.0, remaining);
                remaining -= absorbed;

                // 吸收过程中可能把自己用坏（道具自己的 ShouldRemove 说了算）。
                // 这里只记录、遍历结束再收走 —— 边遍历边改 Inventory / Accessories 会漏项。
                if (item is IChangeable c && c.ShouldRemove()) absorberBroke = true;
            });

            if (absorberBroke) RemoveBrokenEquippedItems();

            // 全被吸收时不用再走受伤管线：不扣血、不弹「你受到 X 点伤害」
            if (remaining > 0) TakeDamage(remaining);
        }

        public void Attack(List<Monster> monsters)
        {
            double dmg = Calculator.CalculateAttack(Str, Atk);
            Console.WriteLine("=====攻击界面=====");

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

        /// <summary>
        /// 回合开始的护盾重置：护盾是「本回合」的资源，不跨回合累积。
        ///
        /// 为什么是「清零后让有盾的装备各加一次」而不是直接写 Shield = 0：
        ///   否则盾牌（+5）只在开战那一回合有用，之后永远为 0，等于把道具废掉。
        ///
        /// 为什么**不再**复用 ApplyEquipmentPassives（改造前就是那么写的，那是个真 bug）：
        ///   那个方法在「先把 Atk/Def 还原成基础值」的同时会把 Def 也还原掉，于是金属的
        ///   「消耗 1 块永久 +1 DEF」（Items\Materials\Metal.cs）会在下一回合开头被**静默**冲掉。
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
    }
}
