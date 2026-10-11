using UnityEngine;

public class SimpleAnchor : MonoBehaviour
{
    public bool isDropped = false;
    public float dragForce = 5000f;
    public float zeroForceRadius = 2f;

    private Rigidbody rb;
    private Vector3 anchorPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (!isDropped) return;

        Vector3 currentPos = transform.position;
        Vector3 distance = anchorPosition - currentPos;
        float distMag = distance.magnitude;

        if (distMag > zeroForceRadius)
        {
            // Çapa ipi gerildiðinde uygulanacak geri çekme kuvveti
            Vector3 force = (distMag / zeroForceRadius) * dragForce * distance.normalized;
            rb.AddForce(force, ForceMode.Force);
        }
        else
        {
            // Halat gevþekse akýntýyla hafif salýným için sönümleme yap
            rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 2f);
        }
    }

    public void Drop()
    {
        isDropped = true;
        anchorPosition = transform.position;
    }

    public void Weigh()
    {
        isDropped = false;
    }
}