//敌人状态基类
public class EnemyState : EntityState
{
    //注入所属敌人与动画组件
    protected Enemy enemy;

    public EnemyState(Enemy enemy, StateMachine stateMachine, string animBoolName, string animPlayName = null)
        : base(stateMachine, animBoolName, enemy.anim, animPlayName)
    {
        this.enemy = enemy;
    }

    //动画名交给敌人自己解析：不同朝向可以换不同的 clip
    protected override string ResolveAnimName(string name) => enemy.ResolveAnimName(name);
}
