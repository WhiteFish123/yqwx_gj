//这是一个接口，表示可以被攻击的对象。它定义了一个方法TakeHit，用于处理受到攻击时的逻辑。实现这个接口的类需要提供具体的实现，以便在受到攻击时做出相应的反应，比如减少生命值、播放受击动画等。
using UnityEngine;

// 一次命中的信息
public struct HitInfo
{
    public float damage;        // 伤害值
    public HitType type;        // 攻击到的种类
}

public enum HitType
{
    knife,    // 近战
    gun       // 远程
}

// 能被攻击的接口
public interface IDamageable
{
    void TakeHit(HitInfo info);
}