//这个是掌控交互系统的脚本，挂在玩家身上
using UnityEngine;
using TMPro;

public class PlayerInteractor : MonoBehaviour
{
    [Header("交互设置")]
    public KeyCode interactKey = KeyCode.E;
    public float interactRange = 1.5f;

    public LayerMask interactableLayers = ~0;

    [Header("UI")]
    public TextMeshProUGUI promptText;
    private IInteractable nearest;// 记录最近的可交互对象

    void Update()
    {
        nearest = FindNearest();

        if (promptText != null)
        {
            if(nearest!=null)
            {
                promptText.text = "[" + nearest.GetPrompt() + "]";
                promptText.gameObject.SetActive(true);
            }
            else
            {
                promptText.gameObject.SetActive(false);
            }
        }

        if (nearest != null && Input.GetKeyDown(interactKey))
        {
            nearest.Interact(gameObject);// 调用交互方法
        }
    }

    // 查找最近的可交互对象
    IInteractable FindNearest()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange, interactableLayers);// 获取范围内的所有碰撞体
        IInteractable best = null;
        float bestDistance = float.MaxValue;
        foreach (Collider2D hit in hits)
        {
            IInteractable target = hit.GetComponent<IInteractable>();
            if (target == null) continue;
            float distance = Vector2.Distance(transform.position, hit.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = target;
            }
        }
        return best;
    }
}