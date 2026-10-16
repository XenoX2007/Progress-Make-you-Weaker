using UnityEngine;

public class HeadTurn : MonoBehaviour
{
    [SerializeField] Rigidbody rb;
    [SerializeField] float turnSpeed = 8f;

    void Update()
    {
        Vector3 velocity = rb.linearVelocity;

        velocity.y = 0f;

        if (velocity.magnitude > 0.1f)
        {
            Quaternion target =
                Quaternion.LookRotation(velocity);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target,
                turnSpeed * Time.deltaTime);
        }
    }
}