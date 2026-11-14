//这是一个接口，定义了可拿在手上使用的物品的行为。实现该接口的类需要提供具体的使用逻辑。
using UnityEngine;

public interface IUsable
{
    // 返回后摇时长（秒）：0 = 这次没打出去（冷却中），>0 = 打出去了并且要后摇这么久
    float Use(Vector2 origin, Vector2 facing);
}