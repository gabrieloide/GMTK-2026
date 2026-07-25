using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarObstacle : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 15f;

    private Rigidbody rb;
    private float timer;

    public void Init(float moveSpeed)
    {
        speed = moveSpeed;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + transform.forward * speed * Time.fixedDeltaTime);

        timer += Time.fixedDeltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
