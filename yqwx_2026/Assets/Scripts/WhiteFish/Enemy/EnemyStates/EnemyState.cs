//敌人状态基类
public class EnemyState : EntityState
{
    //注入所属敌人与动画组件
    protected Enemy enemy;

    public EnemyState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(stateMachine, animBoolName, enemy.anim)
    {
        this.enemy = enemy;
    }
}
