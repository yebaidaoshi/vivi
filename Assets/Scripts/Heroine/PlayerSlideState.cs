using Spine;
using UnityEngine;

public class PlayerSlideState : PlayerState
{
    private const string SlideStartAnim = "Slide";
    private const string SlidingAnim = "Sliding";
    private const string RollAnim = "Roll";
    private const string SlideToIdleAnim = "Slide_To_Idle";

    [SerializeField] private float slideSpeed = 28f;
    [SerializeField] private float deceleration = 0.90f;
    [SerializeField] private float speedThreshold = 1f;

    private int direction;
    private float currentSpeed;
    private bool isSliding = false;
    private bool isRolling = false;
    private bool isTransitioningToIdle = false;
    private bool rollCompleted = false;

    public PlayerSlideState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("进入SlideState");
        player.canCancelAttack = false;

        isSliding = false;
        isRolling = false;
        isTransitioningToIdle = false;
        rollCompleted = false;
        currentSpeed = slideSpeed;

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        if (Mathf.Abs(moveX) > 0.1f)
            direction = (moveX > 0) ? 1 : -1;
        else
            direction = player.facingDirection;

        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event += OnSpineEvent;

        var track = player.skeletonAnim.AnimationState.SetAnimation(0, SlideStartAnim, false);
        track.MixDuration = 0f;
        track.AttachmentThreshold = 0f;
    }

    public override void Update()
    {
        base.Update();

        if (isTransitioningToIdle)
        {
            if (player.inputActions.Player.Crouch.WasPressedThisFrame())
            {
                player.ChangeState(player.CrouchState);
                return;
            }

            var track = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (track != null && track.IsComplete)
            {
                player.ChangeState(player.IdleState);
                isTransitioningToIdle = false;
            }
            return;
        }

        if (isRolling)
        {
            var track = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (track != null && track.IsComplete && !rollCompleted)
            {
                rollCompleted = true;
                isRolling = false;
                if (player.inputActions.Player.Crouch.IsPressed())
                {
                    player.forceCrouching = true;
                }
                player.ChangeState(player.CrouchState);
                Debug.Log("Roll完成 → 进入CrouchState");
            }
            return;
        }

        if (!isSliding)
        {
            var track = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (track != null && track.IsComplete)
            {
                var newTrack = player.skeletonAnim.AnimationState.SetAnimation(0, SlidingAnim, true);
                newTrack.MixDuration = 0f;
                newTrack.AttachmentThreshold = 0f;
                isSliding = true;
            }
            return;
        }

        // ---- 滑铲循环中 ----
        if (isSliding)
        {
            // 松开 S 键 → 立即站起
            if (!player.inputActions.Player.Crouch.IsPressed())
            {
                var track = player.skeletonAnim.AnimationState.SetAnimation(0, SlideToIdleAnim, false);
                track.MixDuration = 0f;
                track.AttachmentThreshold = 0f;
                isTransitioningToIdle = true;
                isSliding = false;
                Debug.Log("滑铲中松开 S → 播放 Slide_To_Idle");
                return;
            }

            // 速度衰减
            currentSpeed *= deceleration;
            if (currentSpeed < 0.01f) currentSpeed = 0f;

            // ★★★ 修改点：使用 Transform.Translate 强制移动，替代不生效的 rb.velocity ★★★
            if (currentSpeed > 0.01f)
            {
                Vector3 moveDelta = new Vector3(direction * currentSpeed * Time.deltaTime, 0f, 0f);
                player.transform.Translate(moveDelta, Space.World);
            }

            // 阈值检测（所有逻辑不变）
            if (currentSpeed <= speedThreshold)
            {
                bool crouchPressed = player.inputActions.Player.Crouch.IsPressed();
                bool movePressed = Mathf.Abs(player.inputActions.Player.Move.ReadValue<Vector2>().x) > 0.1f;

                if (crouchPressed)
                {
                    var track = player.skeletonAnim.AnimationState.SetAnimation(0, RollAnim, false);
                    track.MixDuration = 0f;
                    track.AttachmentThreshold = 0f;
                    isRolling = true;
                    rollCompleted = false;
                    isSliding = false;
                    Debug.Log("滑铲结束 → 播放 Roll");
                    return;
                }
                else if (movePressed)
                {
                    player.skeletonAnim.AnimationState.SetEmptyAnimation(0, 0f);
                    var runTrack = player.skeletonAnim.AnimationState.SetAnimation(0, "Run", true);
                    runTrack.MixDuration = 0f;
                    runTrack.AttachmentThreshold = 0f;
                    float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                    int newDir = (moveX > 0) ? 1 : -1;
                    player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                    player.skeletonAnim.Skeleton.ScaleX = newDir;
                    player.facingDirection = newDir;
                    player.ChangeState(player.RunState);
                    Debug.Log("滑铲结束 → 跑步（瞬切）");
                    return;
                }
                else
                {
                    var track = player.skeletonAnim.AnimationState.SetAnimation(0, SlideToIdleAnim, false);
                    track.MixDuration = 0f;
                    track.AttachmentThreshold = 0f;
                    isTransitioningToIdle = true;
                    isSliding = false;
                    Debug.Log("滑铲结束 → 站立");
                    return;
                }
            }
        }
    }

    private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name != "SendEvent") return;
        string str = e.String;
        Debug.Log($"SlideState 收到事件: {str}");

        if (str == "SlideToIdleEnd" || str == "End")
        {
            if (isTransitioningToIdle)
            {
                isTransitioningToIdle = false;
                player.ChangeState(player.IdleState);
                Debug.Log("Slide_To_Idle完成（事件驱动） → 进入IdleState");
            }
        }
    }

    public override void Exit()
    {
        base.Exit();
        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event -= OnSpineEvent;

        isSliding = false;
        isRolling = false;
        isTransitioningToIdle = false;
        rollCompleted = false;
        player.canCancelAttack = false;
        Debug.Log("退出SlideState");
    }

    public void SetDirection(int dir)
    {
        direction = dir;
    }
}