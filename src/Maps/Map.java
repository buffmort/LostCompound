//package Maps;
//
//import Rooms.Room;
//import Rooms.StartRoom;
//
//public class Map {
//    protected Room[][] grid;
//    public int size;
//    protected double density;
//    public Map(int size,double density){
//        this.size = size;
//        this.grid = MapGenerator.generate(size,(int)(density * 100),System.currentTimeMillis());
//        this.density = density;
//    }
//    public Map(){
//        this(13,0.6);
//    }
//    public void printMap(){
//        for(int y = 0;y<size;y++){
//            for(int x = 0; x<size;x++){
//                Room r =grid[y][x];
//                if(r == null){
//                    System.out.print(". ");
//                } else if (r instanceof StartRoom) {
//                    System.out.print("S ");
//
//                }else{
//                    System.out.print("# ");
//                }
//
//            }
//            System.out.println();
//        }
//    }
//}
