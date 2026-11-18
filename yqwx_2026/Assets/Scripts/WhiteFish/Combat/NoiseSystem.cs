using System.Collections.Generic;
using UnityEngine;

//能被噪音吸引的对象（敌人）。定义在 Combat 程序集里，
//这样 Chest 之类的噪音源不必引用 WhiteFish.Enemy。
public interface INoiseListener
{
    //当前世界坐标，用来做半径判断
    Vector3 NoisePosition { get; }

    //收到噪音，自己去处理（例如进入调查状态）
    void OnHeardNoise(Vector3 worldPosition);
}

//噪音广播：噪音源（燃烧的箱子、枪声、砸东西）只需要调 Broadcast，
//由注册表挑出半径内的听众，避免每次 FindObjectsByType 分配数组。
public static class NoiseSystem
{
    private static readonly List<INoiseListener> listeners = new List<INoiseListener>();

    public static void Register(INoiseListener listener)
    {
        if (listener == null || listeners.Contains(listener))
            return;

        listeners.Add(listener);
    }

    public static void Unregister(INoiseListener listener)
    {
        if (listener == null)
            return;

        listeners.Remove(listener);
    }

    //让 radius 内的所有听众收到噪音；穿墙，不做视线检查（符合"听觉"直觉）
    public static void Broadcast(Vector3 position, float radius)
    {
        if (radius <= 0f)
            return;

        float sqrRadius = radius * radius;

        //倒序遍历：OnHeardNoise 里万一注销了自己也不会打乱索引
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            INoiseListener listener = listeners[i];

            if (listener == null)
            {
                listeners.RemoveAt(i);
                continue;
            }

            if ((listener.NoisePosition - position).sqrMagnitude > sqrRadius)
                continue;

            listener.OnHeardNoise(position);
        }
    }
}
