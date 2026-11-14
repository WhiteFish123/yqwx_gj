using UnityEngine;

//敌人基类
public class Enemy : MonoBehaviour
{
    public Animator anim { get; private set; }//动画组件（位于子对象上）
    public StateMachine stateMachine { get; private set; }//状态机实例

    public Enemy_IdleState idleState;//待机状态

    protected virtual void Awake()//获取组件
    {
        anim = GetComponentInChildren<Animator>();
        stateMachine = new StateMachine();
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update()
    {
        stateMachine.UpdateActiveState();
    }
}
