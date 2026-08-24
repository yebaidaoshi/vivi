using UnityEngine;

public class HUDController : MonoBehaviour
{
    [Header("需要在场景中隐藏，运行时显示的底部栏")]
    public GameObject bottomBar; // 拖拽你的 BottomBar 图片到这里

    private void Awake()
    {
        // 只在游戏运行时（Play Mode）执行
        // 确保底部栏在游戏开始前被激活
        if (bottomBar != null)
        {
            bottomBar.SetActive(true);
        }
    }

    // 注意：这里不写 Update，节省性能
}