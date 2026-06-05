using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{ 
    
    //人物的输入系统
    public Moren yidong;
    //人物的刚体组件
    private Rigidbody2D sd;
    //人物是否在地面上
    private PhysicsCheck ph;
    //人物移动的输入值
    public Vector2 yidongdh;
    [Header("基本参数")]
    //人物移动的速度
    public float shudu;
    //人物跳跃的力度
    public float Jumpyue;
    public float hurtForce;
    public bool isHurt;
    public bool isDead;

    //Awake先于其他
    private void Awake()
    {
        yidong = new Moren();
        sd = GetComponent<Rigidbody2D>();
        ph = GetComponent<PhysicsCheck>();


        //+=是时间注册 意思是把后面的函数方法添加到按键按下的那一刻来执行
        //started 按下那一刻执行 且按一次只执行一次
        yidong.Player.Jump.started += Jump;

    }
    private void OnEnable()
    {
        yidong.Enable();
    }
    private void OnDisable()
    {
        yidong.Disable();
    }

    private void Update() {
        yidongdh = yidong.Player.Move.ReadValue<Vector2>();

    }
    private void FixedUpdate()
    {
        if(!isHurt)
            Move();
    }
    public void Move()
    {    //人物移动
        sd.velocity = new Vector2(yidongdh.x * shudu * Time.deltaTime, sd.velocity.y);

        int faceDir = (int)transform.localScale.x;
        //
        if (yidongdh.x > 0)
            faceDir = 1;
        if (yidongdh.x < 0)
            faceDir = -1;

        //人物反转
        transform.localScale = new Vector3(faceDir, 1, 1);
    }

    // 将 Jump 事件处理器实现为接受 CallbackContext 的方法

    public void Jump(InputAction.CallbackContext context)
    {

        //Debug.Log("JUMP");
        if (ph.onGround)
            sd.AddForce(transform.up * Jumpyue, ForceMode2D.Impulse);
    }

    public void GetHurt(Transform attacker)
    {
        isHurt = true;
        sd.velocity = Vector2.zero; 
    }
    public void PlayerDead()
    {
        isDead = true;
        yidong.Player.Disable();
    }

}
