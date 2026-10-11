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
