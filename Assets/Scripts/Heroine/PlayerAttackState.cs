using UnityEngine;
using Spine;
using Spine.Unity;

public class PlayerAttackState : PlayerState
{
    private const string Attack1 = "Attack_BU";
    private const string Attack2 = "Attack2_BU";
    private const string Attack3 = "Attack3_BU";

    [SerializeField] private float comboEndRatio = 0.40f;
    [SerializeField] private float comboTriggerProgress = 0.40f;

    private float timer;
    private float animElapsed;
    private float totalAnimDuration;

    private bool hasPerformedDash;
    private bool bufferedCombo = false;
    private int pendingComboCount;

    private bool bufferedFirstCombo = false;
    private float previousMoveX;

    private bool thirdAttackEnded = false;

    public PlayerAttackState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        player.canCancelAttack = false;

        if (player.currentComboCount == 0)
            player.currentComboCount = 1;

        Debug.Log($"[AttackState] Enter, combo = {player.currentComboCount}");

        bufferedCombo = false;
        bufferedFirstCombo = false;
        pendingComboCount = 0;
        player.currentJumpCount = 0;
        player.rb.velocity = new Vector2(0.1f * player.facingDirection, player.rb.velocity.y);
        player.skeletonAnim.Skeleton.ScaleX = player.facingDirection;

        previousMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;

        PlayAttackAnimation();
        totalAnimDuration = GetCurrentAnimDuration();
        timer = totalAnimDuration;
        animElapsed = 0f;
        hasPerformedDash = false;
        thirdAttackEnded = false;

        // 第三段订阅事件
        if (player.currentComboCount == 3)
        {
            if (player.skeletonAnim != null)
                player.skeletonAnim.AnimationState.Event += OnSpineEvent;
            timer = float.MaxValue; // 禁用超时
            if (totalAnimDuration < 0.1f)
                totalAnimDuration = 1.0f;
            Debug.Log($"[AttackState] 第三段开始，总时长 {totalAnimDuration}，等待 Movable 事件或动画自然结束");
        }

        Attack();
    }

    public override void Update()
    {
        base.Update();

        // 蹲下检测（第三段不可取消）
        if (player.canCancelAttack && player.currentComboCount != 3 && player.inputActions.Player.Crouch.WasPressedThisFrame())
        {
            player.currentComboCount = 0;
            bufferedCombo = false;
            bufferedFirstCombo = false;
            player.ChangeState(player.CrouchState);
            return;
        }

        // 跳跃检测（第三段不可取消）
        if (player.canCancelAttack && player.currentComboCount != 3 &&
            player.inputActions.Player.Jump.WasPressedThisFrame() &&
            player.currentJumpCount < player.maxJumpCount &&
            player.IsGrounded())
        {
            player.currentComboCount = 0;
            bufferedCombo = false;
            bufferedFirstCombo = false;
            player.facingDirectionBeforeJump = player.facingDirection;
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            player.isRunningJump = Mathf.Abs(moveX) > 0.1f;
            player.ChangeState(player.JumpState);
            return;
        }

        // 更新 timer 和 progress（用于第一二段）
        timer -= Time.deltaTime;
        animElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(animElapsed / totalAnimDuration);

        // ---- 冲刺检测（仅第一二段可触发，且需要 canCancelAttack） ----
        if (player.canCancelAttack && player.currentComboCount < 3 &&
            !hasPerformedDash && player.currentComboCount < 3)
        {
            float currentMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            bool isMovePressed = (Mathf.Abs(currentMoveX) > 0.1f) && (Mathf.Abs(previousMoveX) <= 0.1f);
            if (isMovePressed)
            {
                int dir = (currentMoveX > 0) ? 1 : -1;
                player.DashState.SetDirection(dir);
                player.rb.velocity = Vector2.zero;
                bufferedCombo = false;
                bufferedFirstCombo = false;
                previousMoveX = currentMoveX;
                player.ChangeState(player.DashState);
                return;
            }
            previousMoveX = currentMoveX;
        }

        // ---- 第一段攻击 ----
        if (player.currentComboCount == 1)
        {
            if (progress <= comboEndRatio)
            {
                if (player.inputActions.Player.Attack.WasPressedThisFrame())
                {
                    player.currentComboCount = 2;
                    bufferedFirstCombo = false;
                    Debug.Log($"[AttackState] 第一段 -> 第二段，combo = 2");
                    PlayAttackAnimation();
                    totalAnimDuration = GetCurrentAnimDuration();
                    timer = totalAnimDuration;
                    animElapsed = 0f;
                    hasPerformedDash = false;
                    Attack();
                    return;
                }
            }
            else
            {
                if (player.inputActions.Player.Attack.WasPressedThisFrame())
                {
                    bufferedFirstCombo = true;
                    Debug.Log($"[AttackState] 第一段缓冲，等待窗口");
                }
            }
        }

        // ---- 第一段缓冲触发 ----
        if (player.currentComboCount == 1 && bufferedFirstCombo && progress <= comboEndRatio)
        {
            player.currentComboCount = 2;
            bufferedFirstCombo = false;
            Debug.Log($"[AttackState] 缓冲触发，第一段 -> 第二段，combo = 2");
            PlayAttackAnimation();
            totalAnimDuration = GetCurrentAnimDuration();
            timer = totalAnimDuration;
            animElapsed = 0f;
            hasPerformedDash = false;
            Attack();
            return;
        }

        // ---- 第二段攻击缓冲 ----
        if (player.currentComboCount == 2 && !bufferedCombo && progress <= comboEndRatio)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                bufferedCombo = true;
                pendingComboCount = 3;
                Debug.Log($"[AttackState] 第二段缓冲，pending = 3");
            }
        }

        // ---- 触发第二段缓冲（进入第三段） ----
        if (bufferedCombo && player.currentComboCount == 2 && progress >= comboTriggerProgress)
        {
            player.currentComboCount = pendingComboCount;
            bufferedCombo = false;
            pendingComboCount = 0;
            Debug.Log($"[AttackState] 缓冲触发，combo = {player.currentComboCount}");
            PlayAttackAnimation();
            totalAnimDuration = GetCurrentAnimDuration();
            timer = float.MaxValue;
            animElapsed = 0f;
            hasPerformedDash = false;
            thirdAttackEnded = false;
            if (player.currentComboCount == 3)
            {
                if (player.skeletonAnim != null)
                    player.skeletonAnim.AnimationState.Event += OnSpineEvent;
                if (totalAnimDuration < 0.1f)
                    totalAnimDuration = 1.0f;
                Debug.Log($"[AttackState] 进入第三段，总时长 {totalAnimDuration}，等待 Movable 事件或动画自然完成");
            }
            Attack();
            return;
        }

        // ---- ★ 第三段攻击：由 Movable 事件或动画自然完成（基于时间） ---- ★
        if (player.currentComboCount == 3)
        {
            // 动画自然完成条件：播放时间 >= 总时长 - 小缓冲
            bool animFinished = (animElapsed >= totalAnimDuration - 0.05f);

            if (thirdAttackEnded || animFinished)
            {
                if (!thirdAttackEnded && animFinished)
                {
                    Debug.Log("[AttackState] 第三段动画播放完成（基于时间），触发结束");
                }
                ExecuteThirdAttackEnd();
                return;
            }
        }
        else
        {
            // ---- 第一、二段动画结束 ----
            if (timer <= 0f)
            {
                bool isThirdAttack = (player.currentComboCount == 3);
                player.currentComboCount = 0;
                bufferedCombo = false;
                bufferedFirstCombo = false;
                pendingComboCount = 0;
                Debug.Log($"[AttackState] 动画结束，重置连击，isThird = {isThirdAttack}");

                if (isThirdAttack) // 不会进入
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
    }

    private void ExecuteThirdAttackEnd()
    {
        player.currentComboCount = 0;
        bufferedCombo = false;
        bufferedFirstCombo = false;
        pendingComboCount = 0;
        thirdAttackEnded = false;

        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event -= OnSpineEvent;

        Debug.Log($"[AttackState] 第三段结束，重置连击");

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        if (Mathf.Abs(moveX) > 0.01f)
        {
            player.skeletonAnim.Skeleton.ScaleX = (moveX > 0) ? 1 : -1;
            player.facingDirection = (moveX > 0) ? 1 : -1;
            player.forceRunLoop = true;
            player.ChangeState(player.RunState);
        }
        else
        {
            player.ChangeState(player.IdleState);
        }
    }

    // ★ 事件处理：第三段响应 Movable 事件（提前结束），End 事件也可作为结束信号 ★
    private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "SendEvent")
        {
            string str = e.String;
            Debug.Log($"[AttackState] 收到事件: {str}");

            if (player.currentComboCount == 3)
            {
                // Movable 事件触发提前结束，End 事件也作为结束信号
                if (str == "Movable" || str == "End")
                {
                    thirdAttackEnded = true;
                    Debug.Log($"[AttackState] 第三段收到事件 {str}，触发结束");
                }
                // 其他事件忽略
            }
        }
    }

    public override void Exit()
    {
        base.Exit();
        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event -= OnSpineEvent;

        bufferedCombo = false;
        bufferedFirstCombo = false;
        pendingComboCount = 0;
        hasPerformedDash = false;
        player.canCancelAttack = false;
        thirdAttackEnded = false;
        Debug.Log($"[AttackState] Exit, combo = {player.currentComboCount}");
    }

    private void PlayAttackAnimation()
    {
        string animName = GetAnimNameByCount();
        if (!string.IsNullOrEmpty(animName))
        {
            Debug.Log($"[AttackState] 播放动画: {animName}");
            player.skeletonAnim.AnimationState.SetAnimation(0, animName, false);
        }
    }

    private string GetAnimNameByCount()
    {
        switch (player.currentComboCount)
        {
            case 1: return Attack1;
            case 2: return Attack2;
            case 3: return Attack3;
            default: return Attack1;
        }
    }

    private float GetCurrentAnimDuration()
    {
        string animName = GetAnimNameByCount();
        float dur = player.GetAnimationDuration(animName);
        if (dur < 0.1f)
            dur = 1.0f;
        return dur;
    }

    private void Attack()
    {
        // 攻击判定由事件触发
    }
}