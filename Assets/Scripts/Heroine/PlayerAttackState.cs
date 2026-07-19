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

    // 第三段专用
    private bool isThirdAttack = false;
    private bool hasReceivedMovable = false;

    // 第三段结束前缓冲（仅在动画播完且收到 Movable 后启用）
    private bool thirdAnimFinished = false;
    private float thirdEndTimeout = 0.3f;   // 动画结束后给 0.3 秒操作时间

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
        isThirdAttack = false;
        hasReceivedMovable = false;
        thirdAnimFinished = false;
        player.currentJumpCount = 0;
        player.rb.velocity = new Vector2(0.1f * player.facingDirection, player.rb.velocity.y);
        player.skeletonAnim.Skeleton.ScaleX = player.facingDirection;

        previousMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;

        if (player.currentComboCount == 3)
        {
            isThirdAttack = true;
            player.isInThirdAttack = true;
            if (player.skeletonAnim != null)
                player.skeletonAnim.AnimationState.Event += OnSpineEvent;
            Debug.Log("[AttackState] 第三段开始，等待 Movable 事件");
        }

        PlayAttackAnimation();
        totalAnimDuration = GetCurrentAnimDuration();
        timer = totalAnimDuration;
        animElapsed = 0f;
        hasPerformedDash = false;

        Attack();
    }

    public override void Update()
    {
        base.Update();

        timer -= Time.deltaTime;
        animElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(animElapsed / totalAnimDuration);

        // ============ 第一、二段自动开启取消窗口 ============
        if (!isThirdAttack && !player.canCancelAttack && progress >= comboEndRatio)
        {
            player.canCancelAttack = true;
            Debug.Log($"[AttackState] 第一/二段进度 {progress:F2} 自动开启 canCancelAttack");
        }

        // ============ 第三段独立分支 ============
        if (isThirdAttack)
        {
            // 蹲下/跳跃始终可取消
            if (player.inputActions.Player.Crouch.WasPressedThisFrame())
            {
                ResetComboAndBuffers();
                Debug.Log("[AttackState] 第三段中蹲下取消");
                player.ChangeState(player.CrouchState);
                return;
            }

            if (player.inputActions.Player.Jump.WasPressedThisFrame() &&
                player.currentJumpCount < player.maxJumpCount &&
                player.IsGrounded())
            {
                ResetComboAndBuffers();
                player.facingDirectionBeforeJump = player.facingDirection;
                float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                player.isRunningJump = Mathf.Abs(moveX) > 0.1f;
                Debug.Log("[AttackState] 第三段中跳跃取消");
                player.ChangeState(player.JumpState);
                return;
            }

            // 当收到 Movable 后，攻击/移动可随时取消，不再自动结束
            if (hasReceivedMovable)
            {
                // 移动取消 → 跑步
                float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                if (Mathf.Abs(moveX) > 0.01f)
                {
                    ResetComboAndBuffers();
                    player.skeletonAnim.Skeleton.ScaleX = (moveX > 0) ? 1 : -1;
                    player.facingDirection = (moveX > 0) ? 1 : -1;
                    player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                    player.lastThirdAttackExitTime = Time.time;
                    Debug.Log("[AttackState] 第三段中移动取消 → Run");
                    player.ChangeState(player.RunState);
                    return;
                }

                // 攻击取消 → 重新从第一段开始
                if (player.inputActions.Player.Attack.WasPressedThisFrame())
                {
                    ResetComboAndBuffers();
                    player.currentComboCount = 1;
                    Debug.Log("[AttackState] 第三段中攻击取消 → 重新从第一段");
                    player.ChangeState(player.AttackState);
                    return;
                }

                // 如果动画已播完，进入超时等待，防止永久卡住
                var currentTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
                if ((timer <= 0f || (currentTrack != null && currentTrack.IsComplete)) && !thirdAnimFinished)
                {
                    thirdAnimFinished = true;
                    thirdEndTimeout = 0.3f;
                    Debug.Log("[AttackState] 第三段动画结束，进入操作等待...");
                }

                if (thirdAnimFinished)
                {
                    thirdEndTimeout -= Time.deltaTime;
                    if (thirdEndTimeout <= 0f)
                    {
                        // 超时没操作，自然结束
                        EndThirdAttack();
                        return;
                    }
                }

                return;   // 保持第三段，不往下走
            }

            // 未收到 Movable：动画结束就强制结束
            var track = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (timer <= 0f || (track != null && track.IsComplete))
            {
                EndThirdAttack();
                return;
            }

            return;
        }

        // ============ 第一、二段原有逻辑 ============
        // 取消（蹲/跳）
        if (player.canCancelAttack)
        {
            if (player.inputActions.Player.Crouch.WasPressedThisFrame())
            {
                ResetComboAndBuffers();
                player.ChangeState(player.CrouchState);
                return;
            }
            if (player.inputActions.Player.Jump.WasPressedThisFrame() &&
                player.currentJumpCount < player.maxJumpCount &&
                player.IsGrounded())
            {
                ResetComboAndBuffers();
                player.facingDirectionBeforeJump = player.facingDirection;
                float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                player.isRunningJump = Mathf.Abs(moveX) > 0.1f;
                player.ChangeState(player.JumpState);
                return;
            }
        }

        // 冲刺（第一、二段） ★ 保留连击计数，不清零！
        if (player.canCancelAttack && player.currentComboCount < 3 && !hasPerformedDash)
        {
            float currentMoveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            bool isMovePressed = (Mathf.Abs(currentMoveX) > 0.1f) && (Mathf.Abs(previousMoveX) <= 0.1f);
            if (isMovePressed)
            {
                int dir = (currentMoveX > 0) ? 1 : -1;
                player.DashState.SetDirection(dir);
                player.rb.velocity = Vector2.zero;

                // ★ 只清理攻击缓冲，保留 currentComboCount
                bufferedCombo = false;
                bufferedFirstCombo = false;
                pendingComboCount = 0;
                hasPerformedDash = true;          // 防止本段重复冲刺

                previousMoveX = currentMoveX;
                player.ChangeState(player.DashState);
                return;
            }
            previousMoveX = currentMoveX;
        }

        // 第一段连击
        if (player.currentComboCount == 1)
        {
            if (progress <= comboEndRatio)
            {
                if (player.inputActions.Player.Attack.WasPressedThisFrame())
                {
                    player.currentComboCount = 2;
                    bufferedFirstCombo = false;
                    Debug.Log("[AttackState] 第一段 -> 第二段");
                    RefreshAttackState();
                    Attack();
                    return;
                }
            }
            else
            {
                if (player.inputActions.Player.Attack.WasPressedThisFrame())
                {
                    bufferedFirstCombo = true;
                    Debug.Log("[AttackState] 第一段缓冲");
                }
            }
        }

        // 第一段缓冲触发
        if (player.currentComboCount == 1 && bufferedFirstCombo && progress <= comboEndRatio)
        {
            player.currentComboCount = 2;
            bufferedFirstCombo = false;
            Debug.Log("[AttackState] 缓冲触发，第一段->第二段");
            RefreshAttackState();
            Attack();
            return;
        }

        // 第二段缓冲
        if (player.currentComboCount == 2 && !bufferedCombo && progress <= comboEndRatio)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                bufferedCombo = true;
                pendingComboCount = 3;
                Debug.Log("[AttackState] 第二段缓冲");
            }
        }

        // 触发第三段
        if (bufferedCombo && player.currentComboCount == 2 && progress >= comboTriggerProgress)
        {
            player.currentComboCount = pendingComboCount;
            bufferedCombo = false;
            pendingComboCount = 0;
            Debug.Log("[AttackState] 缓冲触发，进入第三段");
            RefreshAttackState();
            if (player.currentComboCount == 3)
            {
                isThirdAttack = true;
                player.isInThirdAttack = true;
                hasReceivedMovable = false;
                if (player.skeletonAnim != null)
                    player.skeletonAnim.AnimationState.Event += OnSpineEvent;
                Debug.Log("[AttackState] 进入第三段");
            }
            Attack();
            return;
        }

        // 动画结束回 Idle
        if (timer <= 0f)
        {
            ResetComboAndBuffers();
            Debug.Log("[AttackState] 第一/二段结束");
            player.ChangeState(player.IdleState);
        }
    }

    private void EndThirdAttack()
    {
        player.lastThirdAttackExitTime = Time.time;
        ResetComboAndBuffers();
        Debug.Log("[AttackState] ★ 第三段自然结束，连击重置为0 ★");

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        if (Mathf.Abs(moveX) > 0.01f)
        {
            player.skeletonAnim.Skeleton.ScaleX = (moveX > 0) ? 1 : -1;
            player.facingDirection = (moveX > 0) ? 1 : -1;
            player.forceRunLoop = false;
            player.ChangeState(player.RunState);
        }
        else
        {
            player.ChangeState(player.IdleState);
        }
    }

    private void ResetComboAndBuffers()
    {
        player.currentComboCount = 0;
        bufferedCombo = false;
        bufferedFirstCombo = false;
        pendingComboCount = 0;
        isThirdAttack = false;
        hasReceivedMovable = false;
        thirdAnimFinished = false;
        player.isInThirdAttack = false;
        player.canCancelAttack = false;

        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event -= OnSpineEvent;
    }

    private void RefreshAttackState()
    {
        PlayAttackAnimation();
        totalAnimDuration = GetCurrentAnimDuration();
        timer = totalAnimDuration;
        animElapsed = 0f;
        hasPerformedDash = false;
    }

    private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "SendEvent")
        {
            string str = e.String;
            Debug.Log($"[AttackState] 收到事件: {str}");

            if (isThirdAttack)
            {
                if (str == "Movable")
                {
                    hasReceivedMovable = true;
                    Debug.Log("[AttackState] 第三段收到 Movable，允许取消");
                }
                // 第三段忽略其他事件
            }
            else
            {
                // 第一、二段只响应 Cancelable 相关事件
                if (str == "Cancelable" || str == "JCancelable" ||
                    str == "E_Katana1" || str == "E_Katana2" ||
                    str == "E_Katana3" || str == "E_Katana4")
                {
                    player.canCancelAttack = true;
                    Debug.Log($"[AttackState] 事件 {str} 开启 canCancelAttack");
                }
                else if (str == "End")
                {
                    player.canCancelAttack = false;
                    Debug.Log("[AttackState] 事件 End 关闭 canCancelAttack");
                }
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
        isThirdAttack = false;
        hasReceivedMovable = false;
        thirdAnimFinished = false;
        player.isInThirdAttack = false;
        Debug.Log($"[AttackState] Exit, combo={player.currentComboCount}");
    }

    private void PlayAttackAnimation()
    {
        string animName = GetAnimNameByCount();
        if (!string.IsNullOrEmpty(animName))
        {
            var track = player.skeletonAnim.AnimationState.SetAnimation(0, animName, false);
            track.MixDuration = 0f;
            track.AttachmentThreshold = 0f;
            Debug.Log($"[AttackState] 播放动画: {animName}");
        }
    }

    private string GetAnimNameByCount()
    {
        return player.currentComboCount switch
        {
            1 => Attack1,
            2 => Attack2,
            3 => Attack3,
            _ => Attack1
        };
    }

    private float GetCurrentAnimDuration()
    {
        float dur = player.GetAnimationDuration(GetAnimNameByCount());
        return dur < 0.1f ? 1.0f : dur;
    }

    private void Attack()
    {
        // 攻击判定由事件触发
    }
}