using UnityEngine;

public class TestPlayIdle : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();

        // 参数说明：("状态名", 层级(默认为0), 归一化时间(0代表从头播))
        // 强制直接播放 Idle，无视任何过渡条件
        animator.Play("Idle", 0, 0f);
    }
}