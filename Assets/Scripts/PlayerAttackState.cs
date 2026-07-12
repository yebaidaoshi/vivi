using UnityEngine;

public class PlayerAttackState : PlayerState
{
    private const string Attack1 = "Attack_BU";
    private const string Attack2 = "Attack2_BU";
    private const string Attack3 = "Attack3_BU";

    // ★ 可调参数（Inspector 中可调）
    [SerializeField] private float comboEndRatio = 0.40f;     // 连击输入窗口结束比例（前 40%）
    [SerializeField] private float comboTriggerProgress = 0.40f; // 实际触发下一段的进度（40%）
    [SerializeField] private float dashStartRatio = 0.10f;     // 冲刺窗口开始比例

    private float timer;
    private float animElapsed;
    private float totalAnimDuration;
    private int comboCount;

    private float previousMoveX;
    private bool hasPerformedDash;

    // ---- 缓冲连击（仅用于第二段→第三段） ----
    private bool bufferedCombo = false;
    private int pendingComboCount; // 下一段段数（comboCount+1）

    public PlayerAttackState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("进入Attack状态");

        comboCount = 1;
        bufferedCombo = false;
        pendingComboCount = 0;
        player.currentJumpCount = 0;
        player.rb.velocity = new Vector2(0.1f * player.facingDirection, player.rb.velocity.y);
        player.skeletonAnim.Skeleton.ScaleX = player.facingDirection;

        PlayAttackAnimation(comboCount);
        totalAnimDuration = GetCurrentAnimDuration();
        timer = totalAnimDuration;
        animElapsed = 0f;
        hasPerformedDash = false;
        previousMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;

        Attack();
    }

    public override void Update()
    {
        base.Update();

        timer -= Time.deltaTime;
        animElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(animElapsed / totalAnimDuration);

        // ---- 1. 第一段攻击：直接连击（无缓冲） ----
        if (comboCount == 1 && progress <= comboEndRatio)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                // 立即进入第二段
                comboCount = 2;
                PlayAttackAnimation(comboCount);
                totalAnimDuration = GetCurrentAnimDuration();
                timer = totalAnimDuration;
                animElapsed = 0f;
                hasPerformedDash = false;
                previousMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                Attack();
                Debug.Log("第一段直接连击 -> 第二段");
                return;
            }
        }

        // ---- 2. 第二段攻击：缓冲连击（窗口内按下，40%触发） ----
        if (comboCount == 2 && !bufferedCombo && progress <= comboEndRatio)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                bufferedCombo = true;
                pendingComboCount = 3;
                Debug.Log("缓冲第二段连击输入，目标第三段");
            }
        }

        // ---- 3. 触发缓冲（第二段→第三段） ----
        if (bufferedCombo && comboCount == 2 && progress >= comboTriggerProgress)
        {
            comboCount = pendingComboCount;
            bufferedCombo = false;
            pendingComboCount = 0;

            PlayAttackAnimation(comboCount);
            totalAnimDuration = GetCurrentAnimDuration();
            timer = totalAnimDuration;
            animElapsed = 0f;
            hasPerformedDash = false;
            previousMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            Attack();
            Debug.Log("触发缓冲 -> 第三段");
            return;
        }

        // ---- 4. 冲刺输入（窗口：dashStartRatio ~ 动画结束，仅前两段） ----
        if (!hasPerformedDash && comboCount < 3 && progress >= dashStartRatio)
        {
            float currentMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            bool isMovePressed = (Mathf.Abs(currentMoveX) > 0.1f) && (Mathf.Abs(previousMoveX) <= 0.1f);
            previousMoveX = currentMoveX;

            if (isMovePressed)
            {
                int dir = (currentMoveX > 0) ? 1 : -1;
                player.DashState.SetDirection(dir);
                player.rb.velocity = Vector2.zero;
                player.ChangeState(player.DashState);
                return;
            }
        }

        // ---- 5. 动画结束，切回 Idle 或 Run（第三段特殊处理） ----
        if (timer <= 0f)
        {
            bool isThirdAttack = (comboCount == 3);
            comboCount = 0;
            bufferedCombo = false;
            pendingComboCount = 0;

            if (isThirdAttack)
            {
                float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                if (Mathf.Abs(moveX) > 0.01f)
                {
                    player.skeletonAnim.Skeleton.ScaleX = (moveX > 0) ? 1 : -1;
                    player.facingDirection = (moveX > 0) ? 1 : -1;
                    player.forceRunLoop = true; // ★ 设置标志，告诉 RunState 跳过 Run_Start
                    player.ChangeState(player.RunState);
                    return;
                }
            }
            player.ChangeState(player.IdleState);
        }
    }

    public override void Exit()
    {
        base.Exit();
        comboCount = 0;
        bufferedCombo = false;
        pendingComboCount = 0;
        Debug.Log("退出Attack状态，连击已重置");
    }

    // ---------- 辅助方法 ----------
    private void PlayAttackAnimation(int count)
    {
        string animName = GetAnimNameByCount(count);
        if (!string.IsNullOrEmpty(animName))
        {
            player.skeletonAnim.AnimationState.SetAnimation(0, animName, false);
            Debug.Log($"播放攻击动画: {animName} (段数 {count})");
        }
    }

    private string GetAnimNameByCount(int count)
    {
        switch (count)
        {
            case 1: return Attack1;
            case 2: return Attack2;
            case 3: return Attack3;
            default: return Attack1;
        }
    }

    private float GetCurrentAnimDuration()
    {
        string animName = GetAnimNameByCount(comboCount);
        return player.GetAnimationDuration(animName);
    }

    private void Attack()
    {
        Debug.Log($"💥 发动攻击！连击段数: {comboCount}");
    }
}