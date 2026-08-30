using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Player;
using Chronos; // 引入 Chronos 命名空间

public class CutsceneManager : MonoBehaviour
{
    public PlayableDirector director;
    public GameObject gameplayPlayer;
    public GameObject cinematicPlayer;

    private PlayerController playerController;
    private Animator gameplayAnimator;
    private Rigidbody2D gameplayRigidbody;
    private Renderer[] gameplayRenderers;
    private LocalClock localClock; // Chronos Local Clock 引用
    private bool isCutscenePlaying = false;

    void Start()
    {
        gameplayAnimator = gameplayPlayer.GetComponent<Animator>();
        gameplayRigidbody = gameplayPlayer.GetComponent<Rigidbody2D>();
        playerController = gameplayPlayer.GetComponent<PlayerController>();
        gameplayRenderers = gameplayPlayer.GetComponentsInChildren<Renderer>();

        // 获取 Local Clock 组件
        localClock = gameplayPlayer.GetComponent<LocalClock>();
        if (localClock == null)
            Debug.LogWarning("未找到 LocalClock，Chronos 暂停功能将不可用。");

        if (playerController == null)
            Debug.LogError("未找到 PlayerController！");

        SetGameplayVisible(true);
        cinematicPlayer.SetActive(false);
        director.stopped += OnCutsceneStopped;
    }

    public void TriggerCutscene()
    {
        if (playerController == null) return;

        playerController.cutsceneMode = true;
        isCutscenePlaying = true;

        // ★ 暂停 Chronos Local Clock：将时间流速设为 0
        if (localClock != null)
            localClock.localTimeScale = 0f;

        // 清零物理速度
        if (gameplayRigidbody != null)
        {
            gameplayRigidbody.velocity = Vector2.zero;
            gameplayRigidbody.angularVelocity = 0f;
            // 保险：关闭物理模拟，防止任何残留物理
            gameplayRigidbody.simulated = false;
        }

        StartCoroutine(WaitForIdleAndSwitch());
    }

    IEnumerator WaitForIdleAndSwitch()
    {
        while (true)
        {
            if (gameplayAnimator == null) yield break;
            AnimatorStateInfo stateInfo = gameplayAnimator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Idle")) break;
            yield return null;
        }

        // 同步初始位置
        cinematicPlayer.transform.position = gameplayPlayer.transform.position;
        cinematicPlayer.transform.rotation = gameplayPlayer.transform.rotation;
        cinematicPlayer.transform.localScale = gameplayPlayer.transform.localScale;

        // ★ 关键：不隐藏游戏体，只禁用渲染
        SetGameplayVisible(false);
        cinematicPlayer.SetActive(true);
        director.Play();

        StartCoroutine(SyncPositionDuringCutscene());
    }

    IEnumerator SyncPositionDuringCutscene()
    {
        while (isCutscenePlaying)
        {
            // 游戏体仍处于激活状态，Transform 可正常修改
            gameplayPlayer.transform.position = cinematicPlayer.transform.position;
            gameplayPlayer.transform.rotation = cinematicPlayer.transform.rotation;
            gameplayPlayer.transform.localScale = cinematicPlayer.transform.localScale;

            if (gameplayRigidbody != null)
            {
                gameplayRigidbody.position = cinematicPlayer.transform.position;
                gameplayRigidbody.rotation = cinematicPlayer.transform.rotation.eulerAngles.z;
                gameplayRigidbody.velocity = Vector2.zero;
                gameplayRigidbody.angularVelocity = 0f;
            }

            yield return null;
        }
    }

    private void SetGameplayVisible(bool visible)
    {
        foreach (var r in gameplayRenderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }

    private void OnCutsceneStopped(PlayableDirector obj)
    {
        isCutscenePlaying = false;

        // 最终位置同步
        Vector3 finalPos = cinematicPlayer.transform.position;
        Quaternion finalRot = cinematicPlayer.transform.rotation;
        Vector3 finalScale = cinematicPlayer.transform.localScale;

        gameplayPlayer.transform.position = finalPos;
        gameplayPlayer.transform.rotation = finalRot;
        gameplayPlayer.transform.localScale = finalScale;

        if (gameplayRigidbody != null)
        {
            gameplayRigidbody.position = finalPos;
            gameplayRigidbody.rotation = finalRot.eulerAngles.z;
            gameplayRigidbody.velocity = Vector2.zero;
            gameplayRigidbody.angularVelocity = 0f;
            // 恢复物理模拟
            gameplayRigidbody.simulated = true;
            Physics2D.SyncTransforms();
        }

        // 恢复渲染
        SetGameplayVisible(true);
        cinematicPlayer.SetActive(false);

        // ★ 恢复 Chronos Local Clock：将时间流速设回 1
        if (localClock != null)
            localClock.localTimeScale = 1f;

        // 恢复输入
        if (playerController != null)
            playerController.cutsceneMode = false;

        director.stopped -= OnCutsceneStopped;
    }
}