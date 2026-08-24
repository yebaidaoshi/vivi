using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public ScreenFader screenFader;   // 在 Inspector 中拖入 ScreenFader 对象
    public string gameSceneName = "GameScene"; // 你的游戏场景名称

    // 点击“开始游戏”按钮时调用
    public void StartGame()
    {
        StartCoroutine(LoadGameScene());
    }

    IEnumerator LoadGameScene()
    {
        // 1. 淡出并显示“加载中...”
        yield return StartCoroutine(screenFader.FadeOutWithLoading());

        // 2. 异步加载场景
        AsyncOperation async = SceneManager.LoadSceneAsync(gameSceneName);
        // 等待加载完成（可选：你可以加一个最小显示时间，让玩家看到“加载中”）
        while (!async.isDone)
        {
            yield return null;
        }

        // 3. 场景加载完成后，隐藏加载文字并淡入
        yield return StartCoroutine(screenFader.FadeInAndHideLoading());
    }

    // 点击“退出游戏”按钮时调用
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}