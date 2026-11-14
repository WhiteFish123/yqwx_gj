using UnityEngine;

public class TestBox : MonoBehaviour,IInteractable
{
    private bool open = false;

    public string GetPrompt()
    {
        return open ? "已经什么都没有了" : "打开箱子";
    }

    public void Interact(GameObject interactor)
    {
        if(!open)
        {
            Debug.Log("你打开了箱子");
            open = true;
        }
    }
}
