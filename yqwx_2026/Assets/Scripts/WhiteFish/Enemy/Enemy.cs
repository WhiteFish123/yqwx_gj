using UnityEngine;

//敌人基类
public class Enemy : MonoBehaviour
{
    public Animator anim { get; private set; }//动画组件（位于子对象上）
    public StateMachine stateMachine { get; private set; }//状态机实例

    public Enemy_IdleState idleState;//待机状态
    public Enemy_DeadState deadState;//死亡状态

    [Header("Health")]
    [SerializeField] protected float maxHealth = 1f;//最大生命值
    public float currentHealth { get; private set; }//当前生命值
    public bool isDead { get; private set; }//是否已经死亡

    protected virtual void Awake()//获取组件
    {
        anim = GetComponentInChildren<Animator>();
        stateMachine = new StateMachine();
        currentHealth = maxHealth;
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update()
    {
        stateMachine.UpdateActiveState();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.P))//按P直接杀死敌人
            TakeDamage(maxHealth);
#endif
    }

    public virtual void TakeDamage(float damage)//承受伤害
    {
        if (isDead || damage <= 0f)
            return;

        currentHealth -= damage;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    protected virtual void Die()//死亡处理
    {
        if (isDead)
            return;

        if (deadState == null)//没配死亡状态：报错并保持可受伤状态
        {
            Debug.LogError("敌人未配置死亡状态：" + name);
            return;
        }

        isDead = true;

        if (!stateMachine.ChangeState(deadState))//状态机未初始化或已锁死,回滚死亡标记
            isDead = false;
    }

    public void DestroyGameObjectWithDelay(float delay)//延迟销毁自身
    {
        if (!Application.isPlaying)
            return;

        Destroy(gameObject, delay);
    }
}
