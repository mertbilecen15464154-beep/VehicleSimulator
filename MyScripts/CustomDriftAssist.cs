using UnityEngine;

public class CustomDriftAssist : MonoBehaviour
{

    private RCCP_CarController carController;
    private Rigidbody rb;

    [Header("Drift Assist Settings")]
    public float driftAngleThreshold = 10f; // Drift algýlama açýsý
    public float forwardThrustForce = 5000f; // Drifttelerken verilecek ekstra ileri itiþ
    public float counterSteerStabilizer = 1500f; // Spin atmayý önleyen tork desteði

    void Start()
    {
        carController = GetComponent<RCCP_CarController>();
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (carController == null || rb == null) return;

        // Aracýn gittiði yön ile baktýðý yön arasýndaki açý (Side-slip angle)
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float driftAngle = Mathf.Atan2(localVelocity.x, localVelocity.z) * Mathf.Rad2Deg;

        // Belli bir hýzýn üstünde ve yanlama açýsýndaysak
        if (carController.absoluteSpeed > 15f && Mathf.Abs(driftAngle) > driftAngleThreshold)
        {

            // 1. ÝLERÝ ÝTÝÞ (Drift atarken hýzýn düþmesini engeller, ivme kazandýrýr)
            if (carController.throttleInput_P > 0.1f)
            {
                // Araca kendi ileri yönünde ek kuvvet uygula
                rb.AddForce(carController.throttleInput_P * forwardThrustForce * transform.forward, ForceMode.Force);
            }

            // 2. DENGE / SPIN ENGELLEME (Aracýn kontrolden çýkmasýný engeller)
            // Sürücünün direksiyon kýrdýðý yön ile drift yönü uyuþuyorsa aracý stabilize et
            float steerInput = carController.steerInput_P;

            // Eðer counter-steer yapýlýyorsa (ters direksiyon) aracý dönmek istediði açýda sabitle
            if ((driftAngle > 0 && steerInput < 0) || (driftAngle < 0 && steerInput > 0))
            {
                rb.AddTorque(counterSteerStabilizer * steerInput * transform.up, ForceMode.Force);
            }
        }
    }
}