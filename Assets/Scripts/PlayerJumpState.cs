using Spine;
using UnityEngine;

public class PlayerJumpState : PlayerState
{
    private const string JumpStart = "Jump/Jump";
    private const string JumpForward = "Jump/Jump_Forward";
    private const string JumpBackward = "Jump/Jump_Backward";
    private const string AirIdle = "Jump/Jump_OnAir";
    private const string AirForward = "Jump/Jump_OnAir_Forward";
    private const string AirBackward = "Jump/Jump_OnAir_Backward";
    private const string Landing = "Landing";

    private const float AirMoveSpeed = 25f;
    private const float SwitchCooldown = 0.12f;

    private bool isLanding = false;
    private float landingTimer = 0f;
    private float lastSwitchTime = -10f;
    private string currentDirectionAnim = AirIdle;

    public PlayerJumpState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("进入Jump状态");
        player.currentJumpCount++;

        isLanding = false;
        landingTimer = 0f;
        lastSwitchTime = -10f;

        player.skeletonAnim.Skeleton.ScaleX = player.facingDirection;
        Jump();

        string start = JumpStart;
        string follow = AirIdle;

        bool isRunningJump = player.isRunningJump;

        if (isRunningJump)
        {
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            float relative = moveX * player.facingDirection;
            if (Mathf.Abs(moveX) > 0.01f)
            {
                if (relative > 0)
                {
                    start = JumpForward;
                    follow = AirForward;
                }
                else
                {
                    start = JumpBackward;
                    follow = AirBackward;
                }
                player.isRunningJump = false;
            }
            else
            {
                player.isRunningJump = false;
            }
        }

        // ★★★ 关键：捕获 TrackEntry 并设置混合时间 ★★★
        var track1 = player.skeletonAnim.AnimationState.SetAnimation(0, start, false);
        var track2 = player.skeletonAnim.AnimationState.AddAnimation(0, follow, true, 0f);
        if (isRunningJump)
        {
            track1.MixDuration = 0f;
            track2.MixDuration = 0f;
        }

        currentDirectionAnim = follow;
        Debug.Log($"起跳: 开头={start}, 后续循环={follow}");
    }

    public override void Update()
    {
        base.Update();

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        bool isGrounded = player.IsGrounded();

        // 攻击 / 跳跃（绝对优先）
        if (player.inputActions.Player.Attack.WasPressedThisFrame())
        {
            player.ChangeState(player.AttackState);
            return;
        }

        // 允许在地面或落地动画中按空格起跳
        if (player.inputActions.Player.Jump.WasPressedThisFrame() && (isLanding || isGrounded))
        {
            player.currentJumpCount = 0; // ★ 重置跳跃计数
            Debug.Log("落地时按空格起跳");
            player.ChangeState(player.JumpState);
            return;
        }

        // 落地动画处理
        if (isLanding)
        {
            if (Mathf.Abs(moveX) > 0.01f)
            {
                player.playLandingToRun = true;
                player.ChangeState(player.RunState);
                return;
            }
            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0f)
            {
                player.ChangeState(player.IdleState);
                return;
            }
            return;
        }

        // 刚落地检测
        if (player.rb.velocity.y <= 0 && isGrounded)
        {
            if (Mathf.Abs(moveX) > 0.01f)
            {
                player.playLandingToRun = true;
                player.ChangeState(player.RunState);
                return;
            }
            else
            {
                player.skeletonAnim.AnimationState.SetAnimation(0, Landing, false);
                isLanding = true;
                landingTimer = player.GetAnimationDuration(Landing);
                Debug.Log($"播放落地动画，时长 {landingTimer}");
                return;
            }
        }

        // 地面稳定后进入跑步
        if (isGrounded && Mathf.Abs(moveX) > 0.01f && player.rb.velocity.y <= 0.1f)
        {
            player.ChangeState(player.RunState);
            return;
        }

        // 空中移动与切换
        player.rb.velocity = new Vector2(moveX * AirMoveSpeed, player.rb.velocity.y);

        string targetLoop = AirIdle;
        if (Mathf.Abs(moveX) > 0.01f)
        {
            float relative = moveX * player.facingDirection;
            targetLoop = (relative > 0f) ? AirForward : AirBackward;
        }

        if (targetLoop == currentDirectionAnim)
            return;

        if (Time.time - lastSwitchTime < SwitchCooldown) return;

        bool playStart = false;
        string startAnim = "";
        if ((currentDirectionAnim == AirForward && targetLoop == AirBackward) ||
            (currentDirectionAnim == AirBackward && targetLoop == AirForward))
        {
            playStart = true;
            startAnim = (targetLoop == AirForward) ? JumpForward : JumpBackward;
        }

        if (playStart)
        {
            player.skeletonAnim.AnimationState.SetAnimation(0, startAnim, false);
            player.skeletonAnim.AnimationState.AddAnimation(0, targetLoop, true, 0f);
            Debug.Log($"★ 空中转向：{startAnim} → {targetLoop}");
        }
        else
        {
            player.skeletonAnim.AnimationState.SetAnimation(0, targetLoop, true);
            Debug.Log($"○ 空中切换循环：{targetLoop}");
        }

        currentDirectionAnim = targetLoop;
        lastSwitchTime = Time.time;
    }

    public override void Exit()
    {
        base.Exit();
        isLanding = false;
        landingTimer = 0f;
    }

    private void Jump()
    {
        player.rb.velocity = new Vector2(player.rb.velocity.x, player.jumpForce);
    }
}