using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 主菜单键盘操作：
//   ① 一进来什么都不选中；按下任意键盘键，"开始"被选中（只是点亮，不当确认）
//   ② W / 上 = 往上一个，S / 下 = 往下一个，到头绕回另一端
//   ③ 空格 = 真的点一下当前选中的按钮（走 Button 自己的 OnSubmit，等价于鼠标点击）
//
// 为什么不用 Unity 自带的导航：
//   InputSystemUIInputModule 的 Navigate 其实已经绑了 W/S（UI 动作表里就有），
//   但自带导航是按"屏幕空间位置"自动找邻居的 —— 按钮挪一下位置、或者多加一个，
//   选中的顺序就会变。这里用显式数组定顺序，顺序 = Inspector 里的排列，和布局无关。
//   代价是要把每个按钮的 Navigation 关掉，否则自带导航和本脚本会各走一次，
//   按一下 S 跳两格。
public class MenuKeyboardNavigation : MonoBehaviour
{
    [Header("按显示顺序排列（从上到下）")]
    [SerializeField] private Button[] buttons;

    [Header("第一次按键时点亮的按钮，留空则用列表第一个")]
    [SerializeField] private Button firstButton;

    private void Awake()
    {
        if (buttons == null)
            return;

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            // ① 关掉自带导航，选中完全由本脚本控制
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            // ② SpriteSwap 的"选中态"默认没有贴图 —— 被键盘选中时画面上跟没选一样。
            //    借悬停贴图过来，键盘选中和鼠标悬停就是同一个视觉。
            SpriteState spriteState = button.spriteState;
            if (spriteState.selectedSprite == null && spriteState.highlightedSprite != null)
            {
                spriteState.selectedSprite = spriteState.highlightedSprite;
                button.spriteState = spriteState;
            }
        }
    }

    private void OnDisable()
    {
        // 切场景时清掉选中，免得下次回到菜单还是亮的
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (buttons == null || buttons.Length == 0)
            return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        int current = IndexOf(eventSystem.currentSelectedGameObject);

        // 还没有选中项（刚进场景，或被鼠标点空白取消了）
        if (current < 0)
        {
            if (!AnyKeyboardKeyDown())
                return;

            // 这一帧只负责点亮，不当作确认：
            // 否则一进游戏按空格就直接读取存档了（空格既是"任意键"又是"确认"）
            Select(eventSystem, StartIndex());
            return;
        }

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            Select(eventSystem, Wrap(current - 1));
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            Select(eventSystem, Wrap(current + 1));

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Submit(eventSystem, eventSystem.currentSelectedGameObject);
    }

    private void Select(EventSystem eventSystem, int index)
    {
        if (index < 0 || index >= buttons.Length)
            return;

        Button button = buttons[index];
        if (button == null || !button.interactable)
            return;

        eventSystem.SetSelectedGameObject(button.gameObject);
    }

    // 真正点一下：走 Button 的 OnSubmit → onClick，和鼠标点击同一条路
    private static void Submit(EventSystem eventSystem, GameObject target)
    {
        if (target == null)
            return;

        ExecuteEvents.Execute(target, new PointerEventData(eventSystem), ExecuteEvents.submitHandler);
    }

    private int StartIndex()
    {
        int index = IndexOf(firstButton);
        return index >= 0 ? index : 0;
    }

    private int Wrap(int index)
    {
        int count = buttons.Length;
        return ((index % count) + count) % count;
    }

    private int IndexOf(Button button)
    {
        return button == null ? -1 : IndexOf(button.gameObject);
    }

    private int IndexOf(GameObject target)
    {
        if (target == null)
            return -1;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].gameObject == target)
                return i;
        }

        return -1;
    }

    // Input.anyKeyDown 连鼠标键也算，这里排除掉：
    // 鼠标点按钮本身就会选中它，不需要再"按任意键点亮"
    private static bool AnyKeyboardKeyDown()
    {
        if (!Input.anyKeyDown)
            return false;

        return !Input.GetMouseButtonDown(0)
            && !Input.GetMouseButtonDown(1)
            && !Input.GetMouseButtonDown(2);
    }
}
