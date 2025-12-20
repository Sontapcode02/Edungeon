using UnityEngine;
using Cinemachine;
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Animator anim;
    private Rigidbody2D rb;
    private Vector2 movement;

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        CinemachineVirtualCamera vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.Follow = transform; 
        }
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        if (movement.x != 0 || movement.y != 0)
        {
            anim.speed = 1;
            anim.SetFloat("InputX", movement.x);
            anim.SetFloat("InputY", movement.y);
            anim.SetBool("IsMoving", true);
        }
        else
        {
            anim.speed = 0;
            anim.SetBool("IsMoving", false);
        }
    }

    void FixedUpdate()
    {
        // 3. Di chuyển vật lý (Tốt cho mạng TCP sau này)
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}