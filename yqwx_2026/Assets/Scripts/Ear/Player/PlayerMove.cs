using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float accel = 200f;//加速度
    private Rigidbody2D rb;
    private float prevX, prevY;
    private bool horizontalWins = true;   //都按着时以后按为主，true为左右，false为上下
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        bool justX = x != 0f && prevX == 0f;
        bool justY = y != 0f && prevY == 0f;
        if (justX&&!justY)
        {
            horizontalWins = true;
        }
        else if (justY&& !justX)
        {
            horizontalWins = false;
        }
        else if (justX && justY)
        {
            horizontalWins = true;
        }
        rb.linearVelocity = new Vector2(x, y).normalized * moveSpeed;
        float finalX = x;
        float finalY = y;
        if (x != 0f && y != 0f)//判断是向上下还是左右移动
        {
            if (horizontalWins)
            {
                finalY = 0f;
            }
            else
            {
                finalX = 0f;
            }
        }

        Vector2 target = new Vector2(finalX, finalY) * moveSpeed;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, accel * Time.fixedDeltaTime);

        prevX = x;  
        prevY = y;
    }
}
