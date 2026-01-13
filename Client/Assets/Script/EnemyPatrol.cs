using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class EnemyPatrol : MonoBehaviour
{
    [Header("Cấu hình di chuyển")]
    public float speed = 2f;
    public float walkDistance = 3f;
    [Tooltip("Tích vào để đi ngang, bỏ tích để đi dọc")]
    public bool patrolHorizontal = true;

    private Vector3 startPos;
    private bool movingPositive = true; // True: Phải/Lên, False: Trái/Xuống
    private Animator anim;

    void Start()
    {
        startPos = transform.position;
        anim = GetComponent<Animator>();

        // [FIX THÊM] Setup hướng mặt ngay từ đầu game để không bị ngược lúc mới spawn
        if (patrolHorizontal)
        {
            // Nếu ban đầu đi phải (movingPositive = true) -> Không lật (SetFlip false)
            // Nếu ban đầu đi trái -> Lật (SetFlip true)
            SetFlip(!movingPositive);
        }
    }

    void Update()
    {
        float currentInputX = 0;
        float currentInputY = 0;

        if (patrolHorizontal)
        {
            // --- LOGIC ĐI NGANG ---
            currentInputY = 0;

            if (movingPositive)
            {
                // Đang đi PHẢI
                transform.Translate(Vector2.right * speed * Time.deltaTime);
                currentInputX = 1;

                // Chạm biên phải -> Quay đầu sang TRÁI
                if (transform.position.x > startPos.x + walkDistance)
                {
                    movingPositive = false;
                    // [ĐÃ SỬA] Chuẩn bị đi Trái thì phải Lật mặt (Flip = true)
                    SetFlip(true);
                }
            }
            else
            {
                // Đang đi TRÁI
                transform.Translate(Vector2.left * speed * Time.deltaTime);
                currentInputX = 1;

                // Chạm biên trái -> Quay đầu sang PHẢI
                if (transform.position.x < startPos.x - walkDistance)
                {
                    movingPositive = true;
                    // [ĐÃ SỬA] Chuẩn bị đi Phải thì Reset mặt (Flip = false)
                    SetFlip(false);
                }
            }
        }
        else
        {
            // --- LOGIC ĐI DỌC ---
            currentInputX = 0;

            if (movingPositive)
            {
                // Đang đi LÊN
                transform.Translate(Vector2.up * speed * Time.deltaTime);
                currentInputY = 1; // Animation Up

                if (transform.position.y > startPos.y + walkDistance)
                {
                    movingPositive = false; // Đổi chiều xuống
                }
            }
            else
            {
                // Đang đi XUỐNG
                transform.Translate(Vector2.down * speed * Time.deltaTime);
                currentInputY = -1; // Animation Down

                if (transform.position.y < startPos.y - walkDistance)
                {
                    movingPositive = true; // Đổi chiều lên
                }
            }
        }

        // Cập nhật Animator
        anim.SetFloat("InputX", currentInputX);
        anim.SetFloat("InputY", currentInputY);
    }

    void SetFlip(bool isFlippedLeft)
    {
        Vector3 scale = transform.localScale;
        // isFlippedLeft = true -> Scale Âm (Nhìn trái)
        // isFlippedLeft = false -> Scale Dương (Nhìn phải)
        scale.x = isFlippedLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        transform.localScale = scale;
    }
}