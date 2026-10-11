using System.Text;
using LostCompound.Items;

namespace LostCompound.Tools
{
    public class Recipe : ICraftable
    {
        private readonly int[][] _materials;
        private readonly Func<Item> _productFactory;

        /// <summary>
        /// 建一条配方：材料表 + **产物工厂**（不是产物实例）。
        ///
        /// ⚠️ 为什么收 <see cref="Func{TResult}"/> 而不是 <c>Item</c>（这是本项目踩过的坑）：
        ///   配方**不能**持有一个产物实例 —— 它会被 <c>Player.AddItem</c> 直接放进背包，
        ///   于是配方和背包指着同一个对象。那会导致
        ///     ① 该对象被消耗后 <c>Count</c> 归零，配方再也造不出东西
        ///        （材料照扣、静默无产物，连「你获得了」都不打印）；
        ///     ② <c>Count &gt; 0</c> 时 <c>AddItem</c> 读的是它自己的 <c>Count</c>、
        ///        又把自己当已有堆叠加一遍，数量翻倍（1 → 2 → 4 → 8）。
        ///   收工厂就绕开了整个问题：配方手里没有实例，每次合成现造一个新的。
        ///   调用点写成 <c>() =&gt; new Xxx(1)</c>，与原来「构造时 new 一次」的初始值逐字一致。
        /// </summary>
        public Recipe(string name, int[][] materials, Func<Item> productFactory)
        {
            Name = name;
            _materials = materials;
            _productFactory = productFactory;
        }

        public string Name { get; }

        public int[][] Materials => _materials;

        /// <summary>造一个全新的产物实例。理由见构造函数注释（以及 <see cref="ICraftable.CreateProduct"/>）。</summary>
        public Item CreateProduct() => _productFactory();

        public bool CanCraft(Player player)
        {
            foreach (int[] mat in _materials)
            {
                int id = mat[0];
                int required = mat[1];
                int owned = player.CountItemById(id);
                if (owned < required) return false;
            }
            return true;
        }

        public void Craft(Player player)
        {
            if (!CanCraft(player))
            {
                Console.WriteLine("材料不足 无法合成" + Name + "!");
                return;
            }
            foreach (int[] mat in _materials)
            {
                int id = mat[0];
                int amount = mat[1];
                player.ConsumeItem(id, amount);
            }
            player.AddItem(CreateProduct());
            // 获得播报已统一由 Player.AddItem 负责，这里不再重复打印「合成X成功」

        }

        public string DescribeMaterials(Player player)
        {
            StringBuilder sb = new StringBuilder("(");
            for (int i = 0; i < _materials.Length; i++)
            {
                int id = _materials[i][0];
                int count = _materials[i][1];
                string itemName = player.GetItemNameById(id);
                sb.Append(itemName).Append("x").Append(count);
                if (i < _materials.Length - 1)
                {
                    sb.Append(" + ");
                }
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
}
