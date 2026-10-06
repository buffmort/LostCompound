using System.Text;
using LostCompound.Items;

namespace LostCompound.Tools
{
    public class Recipe : Craftable
    {
        private string name;
        private int[][] materials;
        private Item product;

        public Recipe(string name, int[][] materials, Item product)
        {
            this.name = name;
            this.materials = materials;
            this.product = product;
        }

        public string getName()
        {
            return name;
        }

        public int[][] getMaterials()
        {
            return materials;
        }

        public Item getProduct()
        {
            return product;
        }

        public bool canCraft(Player player)
        {
            Console.WriteLine("检查配方: " + name);
            foreach (int[] mat in materials)
            {
                int id = mat[0];
                int required = mat[1];
                int owned = player.countItemById(id);
                Console.WriteLine("需要 ID=" + id + " 数量=" + required + "，拥有=" + owned);
                if (owned < required) return false;
            }
            return true;
        }

        public void craft(Player player)
        {
            if (!canCraft(player))
            {
                Console.WriteLine("材料不足 无法合成" + name + "!");
                return;
            }
            foreach (int[] mat in materials)
            {
                int id = mat[0];
                int amount = mat[1];
                player.consumeItem(id, amount);
            }
            player.addItem(product);
            Console.WriteLine("合成" + product.getName() + "成功!");

        }

        public string getMaterialsStr(Player player)
        {
            StringBuilder sb = new StringBuilder("(");
            for (int i = 0; i < materials.Length; i++)
            {
                int id = materials[i][0];
                int count = materials[i][1];
                string itemName = player.getItemNameById(id);
                sb.Append(itemName).Append("x").Append(count);
                if (i < materials.Length - 1)
                {
                    sb.Append(" + ");
                }
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
}
