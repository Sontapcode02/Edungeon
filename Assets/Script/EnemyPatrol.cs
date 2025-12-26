using UnityEngine;

public class EnemyPatrol : MonoBehaviour
{
    [Header("Cấu hình di chuyển")]
    public float speed = 2f;
    public float walkDistance = 3f;
    public bool patrolHorizontal = true; // True: Đi ngang, False: Đi dọc

    [Header("Cấu hình hình ảnh")]
    public Sprite upSprite;   // Kéo ảnh quái đi LÊN (nhìn lưng) vào đây
    public Sprite downSprite; // Kéo ảnh quái đi XUỐNG (nhìn mặt) vào đây

    private Vector3 startPos;
    private bool movingPositive = true; // True: Phải/Lên, False: Trái/Xuống
    private SpriteRenderer sr;

    void Start()
    {
        startPos = transform.position;
        sr = GetComponent<SpriteRenderer>();

        // Set hình ban đầu cho đúng hướng
        UpdateSpriteDirection();
    }

    void Update()
    {
        if (patrolHorizontal)
        {
            // --- LOGIC ĐI NGANG (Giữ nguyên Flip) ---
            if (movingPositive)
            {
                transform.Translate(Vector2.right * speed * Time.deltaTime);
                if (transform.position.x > startPos.x + walkDistance)
                {
                    movingPositive = false; // Đổi hướng sang Trái
                    Flip();
                }
            }
            else
            {
                transform.Translate(Vector2.left * speed * Time.deltaTime);
                if (transform.position.x < startPos.x - walkDistance)
                {
                    movingPositive = true; // Đổi hướng sang Phải
                    Flip();
                }
            }
        }
        else
        {
            // --- LOGIC ĐI DỌC (Dùng Swap Sprite) ---
            if (movingPositive) // Đang đi LÊN
            {
                transform.Translate(Vector2.up * speed * Time.deltaTime);

                // Nếu đi quá xa -> Quay đầu xuống dưới
                if (transform.position.y > startPos.y + walkDistance)
                {
                    movingPositive = false;
                    UpdateSpriteDirection(); // Đổi hình
                }
            }
            else // Đang đi XUỐNG
            {
                transform.Translate(Vector2.down * speed * Time.deltaTime);

                // Nếu đi quá xa -> Quay đầu lên trên
                if (transform.position.y < startPos.y - walkDistance)
                {
                    movingPositive = true;
                    UpdateSpriteDirection(); // Đổi hình
                }
            }
        }
    }

    // Hàm lật mặt cho đi ngang
    void Flip()
    {
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // Hàm đổi hình cho đi dọc
    void UpdateSpriteDirection()
    {
        // Nếu đang đi dọc thì mới đổi hình
        if (!patrolHorizontal && sr != null)
        {
            if (movingPositive)
            {
                // Đang đi LÊN -> Hiện hình lưng
                if (upSprite != null) sr.sprite = upSprite;
            }
            else
            {
                // Đang đi XUỐNG -> Hiện hình mặt
                if (downSprite != null) sr.sprite = downSprite;
            }
        }
    }
}