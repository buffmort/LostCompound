using LostCompound.Items;
using LostCompound.Tools;
using LostCompound.Buffs;

namespace LostCompound.Monsters
{
    public abstract class Monster
    {
        public double Hp { get; set; }
        public double Atk { get; set; }
        public double Str { get; set; }
        public double Def { get; set; }
        public string Name { get; set; }
        // Java 的 protected 同时允许同包访问；C# 的 protected 不允许通过其它子类实例访问，
        // 故此处用 protected internal（= 派生类 or 同一程序集），等价于 Java 语义。
        /// <summary>
        /// 护盾值（本次改动：整套替换掉原来的「格挡次数」）。
        ///
        /// 语义：护盾 = 「额外的血量」。受到伤害时**先扣盾，盾不够扣的部分才扣血**。
        /// 与旧的 Block（次数制）的本质区别：旧机制 1 点 Block 能完全免疫一次任意大的伤害
        /// （连 999 点也能全免），太超模；现在盾是按点数消耗的，5 点盾就是最多帮你扛 5 点伤害。
        ///
        /// 为什么是 double：与 Hp / Def / Atk 一致，方便将来做「按比例给盾」的装备。
        /// 为什么是 protected internal set 而不是 public set：与原来的 Block 保持同一可见性，
        /// 派生类（如 TwistedPairP2 给兄弟加盾）和同程序集调用点都能写，外部只能读。
        /// </summary>
        public double Shield { get; protected internal set; }

        /// <summary>增加护盾（与 Player.AddShield 同名同义）。按 CONVENTIONS 2.3 用累加方法而不是属性赋值。</summary>
        public void AddShield(double amount) => Shield += amount;

        public int SkipTurn { get; set; }

        public void AddSkipTurn(int count) => SkipTurn += count;

        // 从 Java 移植时枚举是引用类型，未赋值即为 null；C# 枚举是值类型，默认值是 0
        // （= Dragon），会改变掉落行为，所以这里用可空 MonsterType?。
        // 现在所有怪物子类都在构造时显式赋了 Type，实际不会为 null。
        public MonsterType? Type { get; set; }

        /// <summary>
        /// 本怪物的档位（普通 / 精英 / Boss）。来源：设计文档 §6 的「类型」列 + §6.1 的 Boss，
        /// 含义与设计理由见 <see cref="MonsterRank"/>。
        ///
        /// 为什么要给所有怪物引入这一个概念：「毒气弹(26) 10% 对非 Boss 敌人造成 int.MAX_VALUE 伤害」
        /// 需要一个「是不是 Boss」的判据，而在此之前代码里**没有任何表达**
        /// （<see cref="MonsterType"/> 只是种类、也没有 <c>IsBoss</c>、<c>RoomType.Boss</c> 只标房间
        /// 且道具的 <c>Use(Player, List&lt;Monster&gt;)</c> 拿不到房间）。用户裁定：用 enum 表达。
        ///
        /// 为什么是 abstract 而不是「默认 Normal」：漏声明的后果不对称 —— 新怪忘了写档位，
        /// 默认 Normal 会让上面的秒杀效果对一只 Boss 生效（不报错、只是数值不对）。
        /// abstract 让漏声明变成**编译错误**。详见 <see cref="MonsterRank"/> 的注释。
        ///
        /// ⚠️ 注意：目前只加「档位」这一个概念，**不要**顺手给怪物加别的属性
        /// （用户明确要求：用 enum 标记 Boss 时不要顺带给所有怪物加新属性）。
        /// </summary>
        public abstract MonsterRank Rank { get; }

        public abstract void Attack(Player p);
        public abstract void TakeDamage(double dmg);
        public abstract void OnMonsterTurn(Player p);
        protected abstract int RollIndex(int count);

        /// <summary>
        /// 写入「环境赋予本怪物的伤害免疫比例」（0~1），战斗开始时由
        /// <see cref="Tools.ApplyEnvironmentMonsterTraits"/> 按当前环境调用一次。
        ///
        /// 默认什么也不做：只有具备免疫机制的怪物才需要这个槽位。
        /// 为什么把它做成基类上的虚方法、而不是让战斗循环去判断怪物类型：
        ///   原先循环里写的是 `if (m is LavaSlime slime) slime.SetHighTempImmunity(...)`，
        ///   以后每多一种「按环境改抗性」的怪，通用循环就要多一个 is。
        ///   现在循环只负责「把环境算出来的比例交给怪物」，怪物自己决定要不要收。
        /// </summary>
        public virtual void ApplyDamageImmunity(double ratio) { }

        /// <summary>
        /// 被泼水时的反应。返回 true = 这一瓶水用在这只身上了
        /// （保持原来「一瓶水只泼一只」的语义：第一个接下的怪用掉这瓶水）。
        /// 默认无反应（返回 false，水继续找下一只）。
        ///
        /// 为什么把「被泼水会怎样」放到怪物自己身上：
        ///   原先 <see cref="Items.WaterBottle"/> 里写的是 `if (mon is LavaSlime &amp;&amp; mon.IsAlive)`
        ///   并直接读 <c>LavaSlime.WaterBottleHpLossRatio</c> —— 每加一种「怕水的怪」
        ///   （水元素、火系怪……）都要回头改这个道具，而且道具还得认识每只怪的比例常量。
        ///   现在道具只问「你被泼水会怎样」，反应内容（扣多少、播报什么）由怪物自己声明，
        ///   与 <see cref="ApplyDamageImmunity"/>、「环境能力位」是同一套分工：
        ///   通用流程只提问，具体规则归拥有者。
        ///
        /// 实现注意：把自己身上的效果结算掉（含扣血、播报），返回 true 表示水被用掉了。
        /// 已经死掉的怪应该返回 false —— 它不该再吃一瓶水（原来由道具侧的 mon.IsAlive 负责）。
        /// </summary>
        public virtual bool OnWaterPoured() => false;

        // 原 6 个子类各自重写的 isAlive() 实现完全相同（都是 hp > 0），
        // 故合并为基类上的一个计算属性，子类不再重写。
        public bool IsAlive => Hp > 0;

        /// <summary>
        /// 「先扣盾、再扣血」的统一结算（护盾值制）。
        ///
        /// 算法（用户裁定：盾 = 额外血量）：
        ///   设 dmg 为**已过 DEF 减免/抗性**的最终伤害：
        ///     若 Shield >= dmg → Shield -= dmg，Hp 不变，返回 0
        ///     否则             → 剩余 = dmg - Shield; Shield = 0; Hp -= 剩余；返回 剩余
        /// 返回值 = 实际扣掉的血量，供调用方决定要不要打「今天没掉血」的提示。
        ///
        /// 抽到基类的理由（CONVENTIONS 2.5）：6 个子类的 TakeDamage 结构高度相似
        /// （先算 realDmg，再 Hp -= realDmg），扣盾扣血这段逻辑一字不差地要写 6 遍，
        /// 正是「同一段逻辑在 ≥2 个子类里各写一遍就该提基类」的典型。
        /// 各子类只保留「自己那份行文独有的控制台输出」，输出文案因此逐字不变。
        /// </summary>
        /// <param name="finalDmg">已经算完 DEF 减免（以及岩浆怪的高温抗性）的最终伤害</param>
        protected double AbsorbWithShield(double finalDmg)
        {
            if (Shield >= finalDmg)
            {
                // 盾足够厚：这刀完全被盾吃掉，血量一点不掉
                Shield -= finalDmg;
                return 0;
            }

            // 盾不够：先清空盾，剩下的才扣血
            double remaining = finalDmg - Shield;
            Shield = 0;
            Hp -= remaining;
            return remaining;
        }

        /// <summary>
        /// 子类 <c>TakeDamage</c> 的公共骨架：<b>预处理 → DEF 减免 → 吃盾 → 播报</b>。
        ///
        /// 抽到基类的理由（CONVENTIONS 2.5）：Dragon / Zombie / Dummy / LavaSlime
        /// 四个子类的这段逻辑逐字相同，只有**播报文案**和 LavaSlime 的**高温抗性预处理**不同。
        /// 各子类现在只需要声明三段文案（<see cref="LogShieldAbsorbed"/> /
        /// <see cref="LogDamageTaken"/>）与必要的预处理（<see cref="ProcessIncomingDamage"/>）。
        ///
        /// ⚠️ 判定顺序与返回值**逐字不变**：先 <see cref="ProcessIncomingDamage"/>（默认恒等，
        /// 只有岩浆怪覆写成高温免疫），再 <see cref="Calculator.ApplyDefense"/>，
        /// 最后 <see cref="AbsorbWithShield"/>；<c>hpLoss &lt;= 0</c> 走「被盾挡下」那条、
        /// 且那里**不**再播报受伤。任何顺序调换都会改变数值，属于 bug。
        /// </summary>
        /// <param name="incomingDamage">玩家打过来的原始伤害（还没过任何减免）</param>
        protected void TakeDamageWithDefaultLog(double incomingDamage)
        {
            // 1. 预处理（岩浆怪在此削掉高温免疫的那 30%；其余怪物用默认的恒等）
            double preprocessed = ProcessIncomingDamage(incomingDamage);

            // 2. DEF 减免
            double realDmg = Calculator.ApplyDefense(Def, preprocessed);

            // 3. 先扣盾、盾不够再扣血；hpLoss = 实际扣掉的血量
            double hpLoss = AbsorbWithShield(realDmg);

            // 4. 播报：伤害全被盾吃下时血量没掉，口径也必须跟着换，
            //    否则玩家会看到「受到 X 点伤害」但血条纹丝不动。
            if (hpLoss <= 0)
            {
                LogShieldAbsorbed();
                return;
            }

            LogDamageTaken(hpLoss);
        }

        /// <summary>
        /// 伤害进入「DEF 减免」之前的一次预处理。默认恒等（原样返回）。
        /// 目前只有岩浆怪覆写它，用来先削掉高温免疫的那 30%（设计文档六）。
        /// </summary>
        protected virtual double ProcessIncomingDamage(double incomingDamage) => incomingDamage;

        /// <summary>
        /// 伤害**全部被护盾吸收**（血量没掉）时的播报。
        /// 只负责打印。
        /// 文案由各子类原样提供，必须与重构前逐字一致。
        ///
        /// 为什么是 virtual 而不是 abstract：不是所有怪物都走这条带播报的骨架。
        /// 作孽双子（TwistedPairP1 / TwistedPairP2）的 TakeDamage 是**故意短一截**的
        /// —— 原实现就是「不过 DEF、也不播报」，只 <see cref="AbsorbWithShield"/>（见那两个类的注释）。
        /// 它们若不覆写本方法，就自动继承「什么都不打印」的默认行为，正好与原实现一致；
        /// 若写成 abstract，就会被迫在两个「本来就没有播报」的类里写空实现，
        /// 那反而像是「这里本该播报、只是懒得写」。
        /// </summary>
        protected virtual void LogShieldAbsorbed() { }

        /// <summary>
        /// 实际扣血时的播报，<paramref name="hpLoss"/> = 真正扣掉的血量。
        /// 同样只负责打印。文案必须与重构前逐字一致
        /// （注意各怪的写法本来就不同：龙是 "{0}受到 {1:F1} 点伤害！"，
        /// 其余三只是 Name + "受到{0:F1}点伤害!"，不要「顺手统一」）。
        /// virtual 而非 abstract 的理由同 <see cref="LogShieldAbsorbed"/>。
        /// </summary>
        protected virtual void LogDamageTaken(double hpLoss) { }

        public override string ToString() => $"{Name}({Math.Max(Hp, 0):F1}) ";

        private bool _lootDropped;

        public List<Item> Die(Player player)
        {
            if (IsAlive || _lootDropped)
            {
                return [];
            }
            _lootDropped = true;

            List<Item> loots = MonsterLootTable.Drop(Type);
            Console.WriteLine(Name + " 死了。");
            foreach (Item item in loots)
            {
                // 掉落播报已统一由 Player.AddItem 负责，这里不再重复打印
                player.AddItem(item);
            }
            return loots;
        }

        // DOT 递减（原 setDOTdur 是 += 累加，不能当递减用）与 ClearDot 已随 DOT 机制整体删除。
        // 周期性伤害与「剩余回合 -1」现在都由 buff（BleedBuff）自己负责。

        // ==================== 状态效果（buff，如流血） ====================
        // 注意：DotDamage / DotDuration 字段已按你的要求删除（DOT 改为流血 buff）。
        /// <summary>挂在怪物身上的状态效果。结算时机见 Tools.ApplyBuffs（大回合开场）。</summary>
        public List<Buff> Buffs { get; } = [];

        /// <summary>
        /// 给怪物挂一个状态效果。同类 buff 叠加到已有对象上（与 Player.AddBuff 同一口径），
        /// 避免同一只怪身上出现两个「流血」互相独立结算。
        /// 叠加规则（伤害取 min、剩余回合相加）写在 Buff.MergeFrom 里，玩家/怪物共用一份。
        ///
        /// ⚠️ 怪物**不吃** buff 的 <see cref="Buff.StrBonus"/> / <see cref="Buff.AtkBonus"/> /
        /// <see cref="Buff.DefBonus"/>（用户裁定，取「明确写死」而不是给怪物加一套重算）。
        /// 也就是说：一个覆写了那三个属性的 buff 挂在怪物身上时，**只有结算部分（如每回合扣血）生效，
        /// 属性修正会被静默忽略**。之所以要在这里写死，是因为这属于「不报错、不崩溃、只是数值不对」
        /// 的那类坑 —— 与当年 `Metal` 里 `p.Def += 1` 被下次 RecomputeStats 冲掉同型。
        ///
        /// 为什么不给 Monster 也加一套 RecomputeStats（方案 a）：
        ///   1. 没有基础值可回退。怪物的 Atk/Def/Str 是在各子类构造器里**直接赋**的
        ///      （Zombie / Dragon / Dummy / LavaSlime 各写各的），Monster 上没有
        ///      对应 Player 的 BaseAtk/BaseDef/BaseStr，也没有 GrowBaseXxx。
        ///      「从基础值重算」第一步就无处取基础值，只能再加一组 BaseXxx 字段并回头改每个子类构造器。
        ///   2. 重算的触发点比玩家侧多一倍。玩家只有「加 buff / 移 buff」两个入口；
        ///      怪物的 Atk/Def 还会在自己的 OnMonsterTurn 里被直接改（Dragon 的防御注释即是此意），
        ///      任何一次这样的赋值都会被下一次重算冲掉 —— 静默失效比现在「根本不读」更难查。
        ///   3. 现状收益为零：目前挂在怪物身上的 buff 只有一例 —— BleedBuff（每回合扣血），
        ///      它不覆写那三个属性；唯一覆写了 StrBonus 的 SuperMaleBuff 只可能挂在玩家身上。
        ///      为 0 个使用者引入 4 个子类的基础值字段与重算通路，风险大于收益。
        ///   → 因此按 (b)：明确写死「怪物不吃属性修正」，并在三处（Buff 的三个属性、
        ///     本方法、Tools.ClearBuffs 里「怪物只清列表、不重算」那一行）说明，
        ///     后来者不必再踩一遍。
        /// </summary>
        public void AddBuff(Buff b)
        {
            if (b == null) return;
            foreach (Buff exist in Buffs)
            {
                if (exist.GetType() == b.GetType())
                {
                    exist.MergeFrom(b);
                    return;
                }
            }
            Buffs.Add(b);
        }

        // 跳过回合：返回"这次是否被跳过"，顺手扣一次计数
        public bool TryConsumeSkipTurn()
        {
            if (SkipTurn <= 0) return false;
            SkipTurn--;
            Console.WriteLine(Name + " 被跳过回合！");
            return true;
        }
    }
}
