//此处定义了一个交互接口，所有可交互的物体都需要实现这个接口，以便玩家可以与它们进行交互。
using UnityEngine;

public interface IInteractable
{
    //提示文字，用于显示在屏幕上
    string GetPrompt();
    //被交互时调用的方法
    void Interact(GameObject interactor);
}