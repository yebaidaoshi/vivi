using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public ScreenFader screenFader;
    public string gameSceneName = "GameScene";

    public void StartGame()
    {
        StartCoroutine(LoadGameScene());
    }

    IEnumerator LoadGameScene()
    {
        // 1. 淡出并显示"加载中..."（此时内部会标记 _pendingFadeIn = true）
        yield return StartCoroutine(screenFader.FadeOutWithLoading());

        // 2. 异步加载场景
        AsyncOperation async = SceneManager.LoadSceneAsync(gameSceneName);
        while (!async.isDone)
            yield return null;

        // ★ 不再调用 FadeInAndHideLoading()！
        // ScreenFader 会在 SceneManager.sceneLoaded 事件中自动淡入。
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}