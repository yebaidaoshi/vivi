using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator anim;

    private Rigidbody2D sd;

    private PlayerController playerController;   // 添加这一行

    private PhysicsCheck physicCheck;

    private void Awake()
    {
        anim= GetComponent<Animator>();
        sd = GetComponent<Rigidbody2D>();
        physicCheck = GetComponent<PhysicsCheck>();
        playerController = GetComponent<PlayerController>();
    }
    private void Update()
    {
        SetAnimation();
    }
    public void SetAnimation()
    {
        anim.SetFloat("isMove",Mathf.Abs( sd.velocity.x));
        anim.SetFloat("valocityY",sd.velocity.y);
        anim.SetBool("onGround", physicCheck.onGround);
        anim.SetBool("isDead", playerController.isDead);

    }
    public void PlayHurt()
    {
        anim.SetTrigger("hurt");
    }

}
