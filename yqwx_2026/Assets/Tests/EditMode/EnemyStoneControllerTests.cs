using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;

public class EnemyStoneControllerTests
{
    private const string controllerPath = "Assets/Animations/AnimatorControllers/Enemy_Stone.controller";

    private static readonly string[] expectedStates =
    {
        "Stone_Idle",
        "Stone_Move",
        "Stone_Attack",
        "Stone_Dead",
        "Stone_Stunned"
    };

    [Test]
    public void Controller_HasExpectedStates()//石像鬼靠字符串 anim.Play 切状态，状态名必须对得上
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        Assert.IsNotNull(controller, controllerPath + " 不存在");

        foreach (string expected in expectedStates)
        {
            bool found = false;

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (ChildAnimatorState state in layer.stateMachine.states)
                {
                    if (state.state.name == expected)
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(found, controllerPath + " 缺少状态：" + expected);
        }
    }

    [Test]
    public void Controller_HasNoParameters()//有参数说明动画切换方式变了，代码里的 anim.Play 会失效
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        Assert.IsNotNull(controller, controllerPath + " 不存在");

        Assert.AreEqual(0, controller.parameters.Length,
            "石像鬼靠 anim.Play 切状态，Controller 不应有参数");
    }
}
