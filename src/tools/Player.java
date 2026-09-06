package tools;
import MapEnvironment.EnvType;
import Rooms.*;
import items.*;
import monsters.*;
import Maps.*;

import javax.security.auth.login.CredentialException;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.Map;
import java.util.Scanner;
public class Player {

    Scanner sc = new Scanner(System.in);
    private double hp = 200;
    private double MaxHp =100;
    private double atk = 15 ;
    private double str = 2;
    private String name;
    private double def = 5;
    public int block = 0;
    private double DOTdmg;
    private double DOTdur;
    private int skipTurn;
    private Room[][] map;      // 当前地图
    private int playerRow;     // 玩家所在行
    private int playerCol;     // 玩家所在列
    private boolean debug = false;

    public int getSkipTurn() {
        return skipTurn;
    }

    public void addItem(Item newItem){//加道具
        if(newItem.isStackable()){
            for(Item existItem:Items){
                if(existItem.getType() == newItem.getType()){
                    existItem.count += newItem.count;
                    if(existItem.count > existItem.getMaxStack()){
                        existItem.count =existItem.getMaxStack();
                    }
                    return;
                }

            }
            Items.add(newItem);
        }else{
            Items.add(newItem);
        }

    }
    public void setDOTdmg(double DOTdmg) {
        this.DOTdmg = DOTdmg;
    }
    public void addDOTdmg(double DOTdmg) {
        this.DOTdmg += DOTdmg;
    }

    public void addDOTdur(double DOTdur) {
        this.DOTdur += DOTdur;
    }

    public void setSkipTurn(int skipTurn) {
        this.skipTurn = skipTurn;
    }

    public void setDeath(boolean death) {
        isDeath = death;
    }

    private boolean isDeath = false;
    //玩家数组初始化
    public ArrayList<Item> Items  = new ArrayList<>();
    public ArrayList<Item> WillAdd  = new ArrayList<>();
    public ArrayList<Craftable> AllRecipe = new ArrayList<>();
    public void onPlayerTurn(ArrayList<Monster> monsters,EnvType envType) {
        if (!isAlive()) {//判定死没死
            System.out.println("你死了！");
            isDeath = true;
            return;
        }
        if (this.DOTdur > 0) {
            this.DOTdur -= 1;
            this.hp -= DOTdmg;
        }
        System.out.println("=====你的回合=====");
        handleChangeableItems(this,envType);
        Items.addAll(WillAdd);
        WillAdd.clear();
        if (skipTurn > 0) {
            skipTurn -= 1;
            System.out.println("跳过回合!");
            return;
        }
        //接下来看看堆叠 没堆叠就删除
    /*
    使用迭代器遍历玩家背包 后面还需要加入buff计算和饰品
    现在暂时不做




    * 后面进行战斗模块
    * */


        Calculator.delay(250);
        System.out.printf("你的血量:%.1f%n", this.hp);
        System.out.printf("你的防御:%.1f%n", this.def);
        System.out.println("你的格挡剩余:" + this.block);
        if(DOTdur>0){
            System.out.println("你剩余的dot时长:"+DOTdur);
            System.out.println("你的dot伤害:"+DOTdmg);
        }
        Calculator.delay(250);
        for (Monster m : monsters) {
            System.out.print(m);
        }
        Calculator.delay(250);
        System.out.println();
        System.out.println("请选择操作");
        Calculator.delay(250);
        System.out.println("1.攻击   2.防御   3.使用背包道具  4.徒手合成");
        if (this.debug) {
            System.out.println("debug模式操作:");
            System.out.println("127, 获得全部合成");
        }
        int option = sc.nextInt();
        switch (option){
            case 1:
                Attack(monsters);
                break;
            case 2:
                block += 1;
                break;
            case 3:
                openItems(monsters);
                break;
            case 4:
                handCraft();
                break;
            case 127:
                System.out.println("此部分尚未开发!");
                break;
            case 128:
                AllRecipe();
                break;
            case 255:
                this.debug = true;
                System.out.println("你已进入debug模式!");
                break;
            default:
                System.out.println("你输入的类型不正确！ 已自动跳过回合");
                break;
        }
    }

    public boolean isAlive(){
        return hp > 0;
    }

    public ArrayList<Item> getItems() {
        return Items;
    }

    public void takeDmg(double dmg){
        double realDmg = Calculator.Define(this.def,dmg);
        if(block >0){
            System.out.println("你格挡了伤害!");
            block -=1;
            return;
        }
        hp -= realDmg;
        System.out.printf("你受到%.1f点伤害!%n",realDmg);
        System.out.println();

    }
    public void Attack(ArrayList<Monster> m){
        double dmg = Calculator.Attack(this.str,this.atk);
        System.out.println("=====攻击界面=====");
        Calculator.delay(250);
        int count = 0;
        for (Monster i: m){
            System.out.print(count+"."+ " "+i+"   ");
            count++;
        }
        System.out.println("请选择怪物攻击:");
        int choice = sc.nextInt();
        if(choice < 0 || choice > m.size()-1){
            System.out.println("您的攻击无效！");
        }else{
            Monster target = m.get(choice);
            target.takeDmg(dmg);
            System.out.println("你攻击了"+target+"!");
        }


    }
    public int selectTarget(ArrayList<Monster> m ){
        System.out.println("请选择对象:");
        int count = 0;
        for (Monster i: m){
            System.out.print(count+"."+ " "+i+"   ");
            count++;
        }
        int choice = sc.nextInt();
        if(choice < 0 || choice > m.size()-1) {
            System.out.println("您的投掷无效！");
            return -1;
        }
        return choice;
    }
    private void openItems(ArrayList<Monster> m){
        int count = 0;
        for (Item i: Items){
            if(i instanceof Useable) {
                System.out.println(count+"."+i+"[可使用]");
                count++;
            }else{
                System.out.println(count+"."+i);
                count++;
            }
        }
        System.out.println("请选择道具使用");
        int choice = sc.nextInt();
        if(choice < 0 || choice > Items.size()-1) {
            System.out.println("您的使用无效！");
        }else{
            Item item = Items.get(choice);
            if(item instanceof Useable){
                ((Useable) item).use(this,m);
            }else{
                System.out.println("您的使用无效！");

            }
        }
    }

    // 修改后的 handleChangeableItems 方法（所在类根据实际情况调整）
    private void handleChangeableItems(Player owner, EnvType envType) {
        ArrayList<Item> toAdd = new ArrayList<>();
        Iterator<Item> iter = owner.Items.iterator();
        while (iter.hasNext()) {
            Item item = iter.next();
            if(item instanceof TurnCount){
                // 关键修改：使用 owner 而非 this
                ((TurnCount) item).onBattleStart(owner, envType);
            }
            if (item instanceof Changeable) {
                Changeable c = (Changeable) item;
                if (c.ShouldRemove()) {
                    Item replacement = c.Become();
                    if (replacement != null) {
                        toAdd.add(replacement);
                    }
                    iter.remove();
                }
            }
        }
        for (Item newItem : toAdd) {
            owner.addItem(newItem);
        }
    }

    public int countItemById(int id){
        int total = 0;
        for(Item item:Items){
            if(item.getId() == id){
                total += item.getCount();
            }
        }
        return total;

    }

    // 修改后的 consumeItem 方法（在 Player 类中）
    public void consumeItem(int id, int amount) {
        // 先检查总数量是否足够
        if (countItemById(id) < amount) {
            System.out.println("道具数量不足！");
            return;
        }
        int remaining = amount;
        Iterator<Item> iter = Items.iterator();
        while (iter.hasNext() && remaining > 0) {
            Item item = iter.next();
            if (item.getId() == id) {
                int deduct = Math.min(item.count, remaining);
                item.count -= deduct;
                remaining -= deduct;
                if (item.count <= 0) {
                    iter.remove();
                }
            }
        }
    }

    public String getItemNameById(int id){
        for(Item item : Items){
            if(item.getId() == id){
                return  item.getName();
            }
        }
        return "null";
    }
    public void AllRecipe() {
        if(!debug){
            System.out.println("你不能使用这个命令!");
            return;
        }
        AllRecipe.add(new Recipe("热硫", new int[][]{
                {5, 1},
                {3, 1},
                {1, 1}
        }, new FushionSluphur(1)));
        AllRecipe.add(new Recipe("玻璃罐", new int[][]{
                {5, 1},
                {6, 1}
        }, new GlassJar(1)));
    }

    public void addRecipe0(){
        System.out.println("你成功添加了合成表0!");
        AllRecipe.add(new Recipe("热硫",new int[][]{
                {5,1},
                {3,1},
                {1,1}
        }, new FushionSluphur(1)));
        AllRecipe.add(new Recipe("玻璃罐",new int[][]{
                {5,1},
                {6,1}
        }, new GlassJar(1)));
    }

    public void handCraft(){
        System.out.println("====徒手合成====");
        Calculator.delay(250);
        ArrayList<Craftable> available = new ArrayList<>();

        // 第一步：遍历所有配方，筛选可合成的
        for(Craftable recipe : AllRecipe){
            if(recipe.canCraft(this)){
                available.add(recipe);
            }
        }

        // 第二步：检查是否有可合成的
        if(available.isEmpty()){
            System.out.println("你没有任何道具合成!");
            return;
        }

        // 第三步：展示可合成列表
        for(int i = 0; i < available.size(); i++){
            Craftable r = available.get(i);
            System.out.println(i + ". " + r.getName() + " " + r.getMaterialsStr(this));
        }

        // 第四步：获取选择并执行
        System.out.println("255. 返回");
        System.out.println("请选择..");
        int choice = sc.nextInt();
        if(choice == 255) return;
        if(choice < 0 || choice >= available.size()){
            System.out.println("选择无效!");
            return;
        }
        available.get(choice).craft(this);
        handCraft();
    }
/*
* 后面是地图逻辑
* */
// 手动指定起始坐标进入地图
public void enterMap(Room[][] map, int startRow, int startCol) {
    this.map = map;
    this.playerRow = startRow;
    this.playerCol = startCol;
    // 进入初始房间：先 enterSetup 再 onEnter
    map[playerRow][playerCol].enterSetup(this);
}

    // 自动寻找 StartRoom 进入地图
    public void enterMap(Room[][] map) {
        this.map = map;
        for (int y = 0; y < map.length; y++) {
            for (int x = 0; x < map[0].length; x++) {
                if (map[y][x] instanceof StartRoom) {
                    playerRow = y;
                    playerCol = x;
                    map[y][x].enterSetup(this);
                    return;
                }
            }
        }
        throw new IllegalStateException("地图中没有起始房间！");
    }

    public int getPlayerRow() { return playerRow; }
    public int getPlayerCol() { return playerCol; }

    public void checkDirections() { //检查可以走的方向
        System.out.println("可行动方向：");
        // 上
        if (playerRow - 1 >= 0 &&
                map[playerRow][playerCol].isDoor(0) &&
                map[playerRow - 1][playerCol] != null) {
            System.out.println("0.上");
        }
        // 下
        if (playerRow + 1 < map.length &&
                map[playerRow][playerCol].isDoor(1) &&
                map[playerRow + 1][playerCol] != null) {
            System.out.println("1.下");
        }
        // 左
        if (playerCol - 1 >= 0 &&
                map[playerRow][playerCol].isDoor(2) &&
                map[playerRow][playerCol - 1] != null) {
            System.out.println("2.左");
        }
        // 右
        if (playerCol + 1 < map[0].length &&
                map[playerRow][playerCol].isDoor(3) &&
                map[playerRow][playerCol + 1] != null) {
            System.out.println("3.右");
        }
    }

    public boolean move(int direction) {
        if (direction < 0 || direction > 3) {
            System.out.println("无效方向！");
            return false;
        }
        Room current = map[playerRow][playerCol];
        if (!current.isDoor(direction)) {
            System.out.println("这个方向没有门！");
            return false;
        }

        int newRow = playerRow, newCol = playerCol;
        switch (direction) {
            case 0: newRow--; break; // 上
            case 1: newRow++; break; // 下
            case 2: newCol--; break; // 左
            case 3: newCol++; break; // 右
        }

        // 边界检查
        if (newRow < 0 || newRow >= map.length || newCol < 0 || newCol >= map[0].length) {
            System.out.println("撞墙了！");
            return false;
        }

        // 离开旧房间（清除玩家标记，触发 onLeave）
        current.leaveSetup(this);

        // 更新坐标
        playerRow = newRow;
        playerCol = newCol;

        // 进入新房间（设置玩家标记，触发 onEnter）
        Room newRoom = map[playerRow][playerCol];
        newRoom.enterSetup(this);
        System.out.println("你移动到了新房间。");

        // 如果 Player 持有 maze 引用，移动后打印地图
        // maze.printMap();

        return true;
    }
    // ========== 加入地图（自动寻找 StartRoom） ==========
    public void JoinMap(Room[][] map) {
        this.map = map;
        for (int y = 0; y < map.length; y++) {
            for (int x = 0; x < map[0].length; x++) {
                if (map[y][x] instanceof StartRoom) {
                    playerRow = y;
                    playerCol = x;
                    map[y][x].enterSetup(this);
                    return;
                }
            }
        }
        throw new IllegalStateException("地图中没有起始房间！");
    }
    void startGame(Room[][] map, TreeMazeGenerator tmg) {
        JoinMap(map);
        Scanner scanner = new Scanner(System.in);
        System.out.println(tmg.printMapToString());
        System.out.println("游戏开始！输入方向探索地图。");

        while (this.isAlive()) {
            // 清屏（可选）
            System.out.print("\033[H\033[2J");
            System.out.println(tmg.printMapToString());
            System.out.println("\n当前位置: (" + playerRow + ", " + playerCol + ")");
            System.out.println("当前房间:" + map[playerRow][playerCol].getRoomType());
            checkDirections();

            int direction;
            try {
                direction = scanner.nextInt();
            } catch (Exception e) {
                scanner.nextLine();
                System.out.println("输入无效，请输入数字。");
                continue;
            }
            if (direction == 4) {
                System.out.println("游戏结束，感谢游玩！");
                break;
            }
            System.out.print("\033[H\033[2J");
            move(direction);
            System.out.println("\n当前位置: (" + playerRow + ", " + playerCol + ")");
            System.out.println("当前房间:" + map[playerRow][playerCol].getRoomType());

            // 3. 执行进入房间的逻辑（战斗、事件等）

        }
    }
}
