using UnityEngine;

public class Movement : MonoBehaviour
{
    [SerializeField] float moveForce = 30f;
    [SerializeField] float rotationSpeed = 5f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>()asdsdffsdg}
    // This is to make the player feel rotate towards the direction of movement
       if(input.magnitude > 0.1f)
{
    Quaternion target =
        Quaternion.LookRotation(input);

    transform.rotation =
        Quaternion.Slerp(
            transform.rotation,
            target,fsdgsdfgsd
            rotationSpeed* Time.deltaTime);
}
// This is to make the player feel upright
Vector3 uprightTorque =
    Vector3.Cross(
        transform.up,
        Vector3.up);

rb.AddTorque(
    uprightTorque * 100f);
// This is to make the player feel like it has some drag
void FixedUpdate()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(h, 0f, v).normalized;

        rb.AddForce(input * moveForce, ForceMode.Acceleration);
    }
 
}
