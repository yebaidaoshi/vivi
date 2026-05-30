using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator anim;

    private Rigidbody2D sd;

    private PlayerController playerController;   // 添加这一行

    private void Awake()
    {
        anim= GetComponent<Animator>();
        sd = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();
    }
    private void Update()
    {
        SetAnimation();
    }
    public void SetAnimation()
    {
        anim.SetFloat("isMove",Mathf.Abs( sd.velocity.x));
        anim.SetBool("isDead", playerController.isDead);

    }
    public void PlayHurt()
    {
        anim.SetTrigger("hurt");
    }

}
