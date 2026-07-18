using Spine;
using UnityEngine;

public class PlayerCrouchAttackState : PlayerState
{
    private const string CrouchAttackAnim = "Crouch/Crouch_Attack";
    private const string CrouchToIdleAnim = "Crouch/Crouch_To_Idle";

    private float timer;
    private float duration;
    private bool hasHit;
    private bool isStandingUp; // 是否正在播放站起动画

    public PlayerCrouchAttackState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("进入蹲下攻击状态");
        player.canCancelAttack = false;
        hasHit = false;
        isStandingUp = false;

        PlayAttackAnimation();
    }

    public override void Update()
    {
        base.Update();

        // ★ 如果正在播放站起动画，等待结束
        if (isStandingUp)
        {
            var track = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (track != null && track.IsComplete)
            {
                player.ChangeState(player.IdleState);
                isStandingUp = false;
            }
            return;
        }

        // ★ 检测 S 键是否松开 → 立即起立（播放 Crouch_To_Idle）
        if (!player.inputActions.Player.Crouch.IsPressed())
        {
            // 播放站起动画
            player.skeletonAnim.AnimationState.SetAnimation(0, CrouchToIdleAnim, false);
            isStandingUp = true;
            // 取消事件订阅（避免在站起动画中触发事件）
            if (player.skeletonAnim != null)
                player.skeletonAnim.AnimationState.Event -= OnSpineEvent;
            Debug.Log("蹲下攻击中松开 S → 开始站起");
            return;
        }

        // ★ 仅在可取消窗口内允许重置攻击（从头播放）
        if (player.inputActions.Player.Attack.WasPressedThisFrame() && timer > 0f && player.canCancelAttack)
        {
            PlayAttackAnimation();
            Debug.Log("蹲下攻击重置，从头播放（可取消窗口内）");
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            // 攻击动画结束，回到蹲下状态
            // 如果 S 键仍按住，设置强制标志，让 CrouchState 直接进入循环（跳过过渡）
            if (player.inputActions.Player.Crouch.IsPressed())
            {
                player.forceCrouching = true;
            }
            player.ChangeState(player.CrouchState);
        }
    }

    // 处理 Spine 事件
    private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name != "SendEvent") return;
        string str = e.String;
        Debug.Log($"CrouchAttackState 收到事件: {str}");

        switch (str)
        {
            case "Attackable":
                if (!hasHit)
                {
                    hasHit = true;
                    PerformAttack();
                }
                break;
            case "Cancelable":
                player.canCancelAttack = true;
                break;
            case "End":
                player.canCancelAttack = false;
                break;
            case "E_SlideKatana":
            case "Movable":
            case "SE_Noutou":
            case "SE_Noutou2":
                break;
            default:
                Debug.Log($"CrouchAttackState 未知事件: {str}");
                break;
        }
    }

    private void PerformAttack()
    {
        Debug.Log($"💥 蹲下攻击！");
        // TODO: 添加攻击判定代码
    }

    private void PlayAttackAnimation()
    {
        // 重新订阅事件（避免重复）
        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event -= OnSpineEvent;
        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event += OnSpineEvent;

        var track = player.skeletonAnim.AnimationState.SetAnimation(0, CrouchAttackAnim, false);
        duration = track != null && track.Animation != null ? track.Animation.Duration : 0.5f;
        timer = duration;
        hasHit = false;
        player.canCancelAttack = false;
        isStandingUp = false;
    }

    public override void Exit()
    {
        base.Exit();
        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event -= OnSpineEvent;
        player.canCancelAttack = false;
        isStandingUp = false;
        Debug.Log("退出蹲下攻击状态");
    }
}