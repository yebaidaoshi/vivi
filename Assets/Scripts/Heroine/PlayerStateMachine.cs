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
    public PlayerDashState DashState { get; private set; }
    public float dashSpeed = 50f;

    // ★ 攻击可取消标志（由 Spine 事件控制）
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
    }

    private void Start()
    {
        maxJumpCount = 1;
        var animStateData = skeletonAnim.AnimationState.Data;
        float mixTime = 0.1f;

        animStateData.SetMix("Run", "Jump/Jump_Forward", mixTime);
        animStateData.SetMix("Run", "Jump/Jump_Backward", mixTime);
        animStateData.SetMix("Jump/Jump_Forward", "Run", mixTime);
        animStateData.SetMix("Jump/Jump_Backward", "Run", mixTime);

        animStateData.SetMix("Run", "Landing", mixTime);
        animStateData.SetMix("Landing", "Run", mixTime);
        animStateData.SetMix("Landing", "Idle", mixTime);
        animStateData.SetMix("Run", "Idle", mixTime);

        animStateData.SetMix("Jump/Jump_OnAir_Forward", "Run", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Backward", "Run", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Forward", "Landing", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Backward", "Landing", mixTime);

        animStateData.SetMix("Jump/Jump", "Jump/Jump_OnAir", mixTime);
        animStateData.SetMix("Jump/Jump_Forward", "Jump/Jump_OnAir_Forward", mixTime);
        animStateData.SetMix("Jump/Jump_Backward", "Jump/Jump_OnAir_Backward", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Forward", "Jump/Jump_OnAir_Backward", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Forward", "Jump/Jump_OnAir", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Backward", "Jump/Jump_OnAir", mixTime);

        animStateData.SetMix("Run_Start", "Run", mixTime);
        animStateData.SetMix("Run", "Run_Start", mixTime);

        animStateData.SetMix("Run_Turning", "Run", mixTime);
        animStateData.SetMix("Run_Turning", "Run_Start", mixTime);

        animStateData.SetMix("Attack3_BU", "Run", 0f);
        animStateData.SetMix("Run", "Attack3_BU", 0f);

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
            skeletonAnim.AnimationState.Event += OnSpineEvent;
    }

    void OnDisable()
    {
        inputActions.Disable();
        if (skeletonAnim != null)
            skeletonAnim.AnimationState.Event -= OnSpineEvent;
    }

    // ★ Spine 事件处理函数（完全由事件驱动取消窗口）
    private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "SendEvent")
        {
            string str = e.String;
            // 开启取消窗口的事件
            if (str == "Cancelable" || str == "JCancelable" ||
                str == "E_Katana1" || str == "E_Katana2" ||
                str == "E_Katana3" || str == "E_Katana4")
            {
                canCancelAttack = true;
                // 可选：打印日志便于调试
                // Debug.Log($"可取消窗口开启: {str}");
            }
            // 关闭取消窗口的事件
            else if (str == "End")
            {
                canCancelAttack = false;
                // Debug.Log("可取消窗口关闭");
            }
            // 其他事件忽略（不影响取消标志）
        }
    }

    public void ChangeState(PlayerState newState)
    {
        if (currentState == newState)
            return;
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public float GetAnimationDuration(string animName)
    {
        var anim = skeletonAnim.Skeleton.Data.FindAnimation(animName);
        return anim != null ? anim.Duration : 0.5f;
    }
}