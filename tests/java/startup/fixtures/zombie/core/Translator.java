package zombie.core;

/** Real transformed fixture with localized labels and the game's varargs API. */
public final class Translator {
    public static boolean chinese;
    public static int calls;
    public static String getText(String key,Object... arguments){
        calls++;
        if(key.equals("throws"))throw new IllegalArgumentException("original translation failure");
        return switch(key){
            case "UI_Loading_Scripts" -> chinese?"正在读取脚本":"スクリプトを読み込み中";
            case "UI_Loading_Clothing" -> chinese?"正在读取服装":"衣服を準備中";
            case "UI_Loading_ModelsAnimations" -> chinese?"正在读取模型与动画":"モデルとアニメーションを準備中";
            case "UI_Loading_OnGameBoot" -> chinese?"正在启动游戏事件":"ゲーム起動イベントを実行中";
            case "UI_Loading_Texturepack" -> (chinese?"正在读取材质包：":"テクスチャを準備中：")+arguments[0];
            case "UI_Loading_Lua" -> chinese?"正在准备用户界面":"インターフェースを準備中";
            default -> "未关联界面："+key;
        };
    }
    public static String getText(String key){return getText(key,new Object[0]);}
    public static int getText(int value){return value+1;}
}
