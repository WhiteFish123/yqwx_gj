// 敌人可以攻击的"目标"。玩家实现它。
//
// ★ 为什么这个接口要放在 WhiteFish.Enemy 程序集里：
//   WhiteFish.Enemy 是独立 asmdef，它看不见 Assembly-CSharp（玩家脚本所在的默认程序集），
//   而 asmdef 只能被 Assembly-CSharp 引用，不能反过来引用它 —— 所以敌人写
//   GetComponent<PlayerHealth>() 会直接编译报错 CS0246。
//
//   解决办法是"面向接口"：
//     - 接口放在敌人这边（敌人自己看得见）
//     - 玩家那边实现它（Assembly-CSharp 引用 WhiteFish.Enemy 是允许的）
//   这样敌人不需要知道玩家的具体类型，两边都不会形成循环依赖。
public interface IEnemyAttackTarget
{
    //被敌人的冲刺/攻击撞到，扣 damage 点血
    void TakeEnemyHit(int damage);
}
