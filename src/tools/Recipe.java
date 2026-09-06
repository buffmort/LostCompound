package tools;


import items.Craftable;
import items.Item;


public class Recipe implements Craftable {
    private String name;
    private int[][] materials;
    private Item product;

    public Recipe(String name, int[][] materials , Item product){
        this.name = name;
        this.materials = materials;
        this.product = product;
    }


    @Override
    public String getName() {
        return name;
    }

    @Override
    public int[][] getMaterials() {
        return materials;
    }

    @Override
    public Item getProduct() {
        return product;
    }

    @Override
    public boolean canCraft(Player player) {
        for (int[] mat : materials) {
            int id = mat[0];
            int required = mat[1];
            int owned = player.countItemById(id);
            if (owned < required) {
                return false;
            }
        }

        return true;
    }

    @Override
    public void craft(Player player) {
        if(!canCraft(player)){
            System.out.println("材料不足 无法合成"+name+"!");
        }
        for(int[] mat : materials){
            int id=mat[0];
            int amount = mat[1];
            player.consumeItem(id,amount);
        }
        player.addItem(product);
        System.out.println("合成"+product.getName()+"成功!");

    }

    @Override
    public String getMaterialsStr(Player player) {
        StringBuilder sb = new StringBuilder("(");
        for(int i =0; i<materials.length;i++){
            int id = materials[i][0];
            int count = materials[i][1];
            String itemName = player.getItemNameById(id);
            sb.append(itemName).append("x").append(count);
            if( i < materials.length - 1){
                sb.append(" + ");
            }
        }
        sb.append(")");
        return sb.toString();
    }
}
