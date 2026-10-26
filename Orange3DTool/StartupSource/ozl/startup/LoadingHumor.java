package ozl.startup;

/** Decorative waiting captions. This clock never changes observed loading state. */
final class LoadingHumor {
    private static final String[] PHRASES={
        "狗正在跨越栅栏","正在摧毁模组","正在添加bug","正在鞭策浣熊","正在给僵尸系鞋带","正在说服直升机绕路",
        "正在把罐头藏到最后一格","正在检查烤箱有没有关","正在教僵尸礼貌敲门","正在给撬棍办理退休返聘","正在寻找那把十毫米扳手",
        "正在把发电机搬离卧室","正在给窗帘安排夜班","正在劝角色别喝漂白水","正在把消防斧借给伐木工","正在寻找最后一瓶番茄酱",
        "正在为水龙头预留最后一滴水","正在让警报器保持冷静","正在往后备箱塞第五张椅子","正在给薯片计算保质期","正在向树枝解释车辆保险",
        "正在为安全屋补上第七把锁","正在把袜子配成一对","正在说服僵尸排队领号","正在给乌鸦更新导航","正在确认这口蘑菇能不能吃",
        "正在把书签夹回木工第二卷","正在给棒球棍补一颗钉子","正在装作没听见远处的枪声","正在为下次停电练习摸黑"
    };
    record Caption(String text,int opacity) {}
    static Caption at(double seconds,long operation,LoadingModel.Context context,boolean reduced){
        int seed=Math.floorMod(operation*31+context.ordinal()*17,PHRASES.length);
        if(reduced)return new Caption(PHRASES[seed%PHRASES.length],155);
        if(!Double.isFinite(seconds))return new Caption("",0);
        double elapsed=Math.max(0,seconds)-(6+seed%2);
        if(elapsed<0)return new Caption("",0);
        // 13 is coprime with 30: one deterministic shuffled tour visits every phrase before repeating.
        long block=(long)Math.floor(elapsed/22.2);elapsed%=22.2;int slot=0;
        double[] periods={7.0,7.4,7.8};while(slot<2&&elapsed>=periods[slot])elapsed-=periods[slot++];
        if(elapsed>=5)return new Caption("",0);
        double fade=Math.min(1,Math.min(elapsed/.32,(5-elapsed)/.32));
        int opacity=(int)Math.round(fade*12)*13;
        if(opacity==0)return new Caption("",0);
        return new Caption(PHRASES[Math.floorMod(seed+(block*3+slot)*13,PHRASES.length)],opacity);
    }
}
