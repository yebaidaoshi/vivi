using UnityEngine;

public class PlayerAttackState : PlayerState
{
    private const string Attack1 = "Attack_BU";
    private const string Attack2 = "Attack2_BU";
    private const string Attack3 = "Attack3_BU";

    [SerializeField] private float comboEndRatio = 0.40f;
    [SerializeField] private float comboTriggerProgress = 0.40f;
    [SerializeField] private float dashStartRatio = 0.10f;

    private float timer;
    private float animElapsed;
    private float totalAnimDuration;
    private int comboCount;

    private float previousMoveX;
    private bool hasPerformedDash;

    private bool bufferedCombo = false;
    private int pendingComboCount;

    public PlayerAttackState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        
        // ★ 重置取消标志，由 Spine 事件控制开启
        player.canCancelAttack = false;

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

        // ---- ★ 跳跃检测（由 canCancelAttack 控制） ----
        if (player.canCancelAttack &&
            player.inputActions.Player.Jump.WasPressedThisFrame() &&
            player.currentJumpCount < player.maxJumpCount &&
            player.IsGrounded())
        {
            player.facingDirectionBeforeJump = player.facingDirection;
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            player.isRunningJump = Mathf.Abs(moveX) > 0.1f;
            player.ChangeState(player.JumpState);
            return;
        }

        timer -= Time.deltaTime;
        animElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(animElapsed / totalAnimDuration);

        // ---- ★ 冲刺检测（由 canCancelAttack 控制） ----
        if (player.canCancelAttack &&
            !hasPerformedDash && comboCount < 3 && progress >= dashStartRatio)
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

        // ---- 1. 第一段攻击：直接连击 ----
        if (comboCount == 1 && progress <= comboEndRatio)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                comboCount = 2;
                PlayAttackAnimation(comboCount);
                totalAnimDuration = GetCurrentAnimDuration();
                timer = totalAnimDuration;
                animElapsed = 0f;
                hasPerformedDash = false;
                previousMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                Attack();
               
                return;
            }
        }

        // ---- 2. 第二段攻击：缓冲连击 ----
        if (comboCount == 2 && !bufferedCombo && progress <= comboEndRatio)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                bufferedCombo = true;
                pendingComboCount = 3;
                
            }
        }

        // ---- 3. 触发缓冲 ----
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
           
            return;
        }

        // ---- 4. 动画结束 ----
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
                    player.forceRunLoop = true;
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
        // ★ 退出时重置取消标志
        player.canCancelAttack = false;
       
    }

    // ---------- 辅助方法 ----------
    private void PlayAttackAnimation(int count)
    {
        string animName = GetAnimNameByCount(count);
        if (!string.IsNullOrEmpty(animName))
        {
            player.skeletonAnim.AnimationState.SetAnimation(0, animName, false);
           
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
       
    }
}