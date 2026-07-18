using Spine;
using Spine.Unity;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    // 当前状态
    private PlayerState currentState;
    // 所有状态
    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public PlayerAirAttackState AirAttackState { get; private set; }
    public PlayerCrouchState CrouchState { get; private set; }
    public PlayerSlideState SlideState { get; private set; }
    public PlayerCrouchAttackState CrouchAttackState { get; private set; }
    public float previousMoveX;
    public int facingDirectionBeforeJump;
    public Rigidbody2D rb;
    public Moren inputActions;
    public SkeletonAnimation skeletonAnim;
    public bool isRunningJump = false;
    public bool playLandingToRun = false;
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    public int maxJumpCount = 1;
    public int currentJumpCount = 0;
    public int facingDirection = 1;
    public bool forceRunLoop = false;
    public bool skipCrouchTransition = false;
    public bool forceCrouching = false;
    public PlayerDashState DashState { get; private set; }
    public float dashSpeed = 50f;

    // ★ 新增：连击计数（由 AttackState 管理，DashState 可读取）
    public int currentComboCount = 0;

    // 攻击可取消标志（由 Spine 事件控制）
    public bool canCancelAttack = false;

    [Header("地面检测")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float checkRadius = 0.2f;
    public bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
    }

    public void Awake()
    {
        IdleState = new PlayerIdleState(this);
        RunState = new PlayerRunState(this);
        AttackState = new PlayerAttackState(this);
        JumpState = new PlayerJumpState(this);
        inputActions = new Moren();
        DashState = new PlayerDashState(this);
        rb = GetComponent<Rigidbody2D>();
        skeletonAnim = GetComponent<SkeletonAnimation>();
        AirAttackState = new PlayerAirAttackState(this);
        CrouchState = new PlayerCrouchState(this);
        SlideState = new PlayerSlideState(this);
        CrouchAttackState = new PlayerCrouchAttackState(this);
    }

    private void Start()
    {
        maxJumpCount = 1;
        var animStateData = skeletonAnim.AnimationState.Data;
        float mixTime = 0.1f;

        // ...（原有混合设置保持不变）...

        // 默认混合（兜底）
        animStateData.DefaultMix = mixTime;

        ChangeState(IdleState);
    }

    private void Update()
    {
        previousMoveX = inputActions.Player.Move.ReadValue<Vector2>().x;
        currentState?.Update();
    }

    void OnEnable()
    {
        inputActions.Enable();
        if (skeletonAnim != null)
        {
            skeletonAnim.AnimationState.Event += OnSpineEvent;
            skeletonAnim.AnimationState.Start += OnAnimationStart;
        }
    }

    void OnDisable()
    {
        inputActions.Disable();
        if (skeletonAnim != null)
        {
            skeletonAnim.AnimationState.Event -= OnSpineEvent;
            skeletonAnim.AnimationState.Start -= OnAnimationStart;
        }
    }

    private void OnAnimationStart(TrackEntry trackEntry)
    {
        if (trackEntry != null)
            trackEntry.AttachmentThreshold = 0f;
    }

    private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "SendEvent")
        {
            string str = e.String;
            if (str == "Cancelable" || str == "JCancelable" ||
                str == "E_Katana1" || str == "E_Katana2" ||
                str == "E_Katana3" || str == "E_Katana4")
            {
                canCancelAttack = true;
            }
            else if (str == "End")
            {
                canCancelAttack = false;
            }
        }
    }

    public void ChangeState(PlayerState newState)
    {
        if (currentState == newState)
            return;
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
        var track = skeletonAnim.AnimationState.GetCurrent(0);
        if (track != null)
            track.AttachmentThreshold = 0f;
    }

    public float GetAnimationDuration(string animName)
    {
        var anim = skeletonAnim.Skeleton.Data.FindAnimation(animName);
        return anim != null ? anim.Duration : 0.5f;
    }
}