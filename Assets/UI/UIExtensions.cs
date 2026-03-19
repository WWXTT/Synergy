using UnityEngine.UI;
/// <summary>
/// UI工具类
/// </summary>
public static class UIExtensions
{
    /// <summary>
    /// 添加点击事件（支持多次绑定）
    /// </summary>
    public static void AddClickListener(this Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick.AddListener(action);
    }

    /// <summary>
    /// 移除所有点击事件
    /// </summary>
    public static void RemoveAllListeners(this Button button)
    {
        button.onClick.RemoveAllListeners();
    }
}
