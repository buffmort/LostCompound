using System.Text;
using LostCompound.Items;

namespace LostCompound.Tools
{
    public class Recipe : Craftable
    {
        private readonly int[][] _materials;

        public Recipe(string name, int[][] materials, Item product)
        {
            Name = name;
            _materials = materials;
            Product = product;
        }

        public string Name { get; }

        public int[][] Materials => _materials;

        public Item Product { get; }

        public bool CanCraft(Player player)
        {
            Console.WriteLine("检查配方: " + Name);
            foreach (int[] mat in _materials)
            {
                int id = mat[0];
                int required = mat[1];
                int owned = player.CountItemById(id);
                Console.WriteLine("需要 ID=" + id + " 数量=" + required + "，拥有=" + owned);
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
            player.AddItem(Product);
            Console.WriteLine("合成" + Product.Name + "成功!");

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
