//这是一个描述玩家持有与使用道具的脚本
using UnityEngine;
using UnityEngine.InputSystem;

public class HeldItemSlot : MonoBehaviour
{
    [Header("设置")]
    public KeyCode useKey = KeyCode.J;
    [Header("测试用")]
    public GameObject startItem;//开局手上拿的什么

    private PlayerMove move;
    private IUsable current;   //手上的道具的行为
    private GameObject holderInstance;//当前持有的道具的实例
    private GameObject heldPrefab;//当前拿的是什么

    public bool HasItem => current != null;
    public GameObject Heldprefab => heldPrefab;
    public string HeldItemName => heldPrefab != null ? heldPrefab.name : "";

    void Awake()//获取玩家移动脚本
    {
        move = GetComponent<PlayerMove>();
    }
    void Start()//开局手上拿的什么
    {
        if (startItem != null)
        {
            Equip(startItem);
        }
    }
    void Update()//每帧检测是否按下使用键
    {
        if (current == null) return;
        bool pressd =Input.GetKeyDown(useKey);

        if (pressd)
        {
            current.Use(transform.position,Facing);
        }
    }
    Vector2 Facing => move != null ? move.facing : Vector2.down;
    public void Equip(GameObject itemPrefab)//从地上捡起或从背包拿以装备道具，原本手上的道具(若有)会通过Unequip()返回背包
    {
        Unequip();
        if (itemPrefab == null) return;
        holderInstance = Instantiate(itemPrefab, transform);
        holderInstance.transform.localPosition = Vector3.zero;

        heldPrefab = itemPrefab;
        current = holderInstance.GetComponent<IUsable>();

        if (current == null)
            Debug.LogWarning("没实现 IUsable，拿在手上时无法使用：" + itemPrefab.name, itemPrefab);
    }
    public GameObject Unequip()//从手上卸下道具到背包，没有判断背包是否放的下，或许可以试试放不下直接扔在地上？
    {
        GameObject result = heldPrefab;

        if (holderInstance != null) Destroy(holderInstance);
        holderInstance = null;
        current = null;
        heldPrefab = null;

        return result;
    }
}
