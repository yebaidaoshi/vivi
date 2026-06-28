/*
 * 这个脚本是用来检测角色是否在地面上的。它使用了一个圆形检测范围来判断角色是否接触地面，并且将结果存储在 onGround 变量中。这个变量可以被其他脚本用来限制跳跃次数等功能。此外，脚本还会更新动画参数，以便根据角色的状态切换动画。
 */

using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PhysicsCheck : MonoBehaviour
{ 
    [Header("地面状态")]
    public bool onGround;

    [Header("检测地面相关参数")]
    public LayerMask groundLayer;
    public float checkRadius;
    public Vector2 bo;

    private Rigidbody2D rb;
    private Animator anim;

    void Start()
    {
        anim = transform.Find("Heroine").GetComponentInChildren<Animator>();  // 获取Idle模型的Animator组件
        rb = GetComponent<Rigidbody2D>();  // 获取刚体
    }

    private void Update()
    {
        CheckOnGround();  // 更新 onGround
        float horizontalSpeed = Mathf.Abs(rb.velocity.x);
        //anim.SetFloat("velocity", horizontalSpeed);  // 该参数目前没有被使用，但可以在动画中根据水平速度切换动画状态
    }

    public void CheckOnGround()
    {
        //检测地面
        onGround = Physics2D.OverlapCircle((Vector2)transform.position + bo, checkRadius, groundLayer);
        if (anim != null)
            anim.SetBool("onGround", onGround);
    }

    //当物体被选中时在场景视图中绘制一个圆形来表示检测范围
    private void OnDrawGizmosSelected() => Gizmos.DrawWireSphere((Vector2)transform.position + bo, checkRadius);
}