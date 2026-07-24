using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    Rigidbody rb;
    PlayerController playerController;
    [SerializeField] private float knockbackForce = 10;
    [SerializeField] private float knockbackDuration = 0.25f;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        rb.angularVelocity = Vector3.zero;

        var opositeDirection = collision.contacts[0].normal;

        opositeDirection.y = 0;
        if (opositeDirection.sqrMagnitude < 0.0001f) return;
        opositeDirection.Normalize();

        playerController.ApplyKnockback(opositeDirection, knockbackForce, knockbackDuration);
    }
}