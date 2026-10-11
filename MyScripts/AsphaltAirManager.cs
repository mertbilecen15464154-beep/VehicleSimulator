using System.Collections;
using UnityEngine;
using TMPro;

public enum ActiveStuntType
{
    None,
    Roll,
    Spin
}

[RequireComponent(typeof(Rigidbody))]
public class AsphaltAirManager : MonoBehaviour
{
    [Header("Ramp Tags")]
    public string rollRampTag = "RollRamp";
    public string stuntRampTag = "StuntRamp";

    [Header("Air Roll Settings")]
    public float rollTorque = 14f;
    public float minSteerForRoll = 0.3f;

    [Header("Air Spin Settings")]
    public float spinTorque = 5f;
    public float minSteerForSpin = 0.3f;

    [Header("Grounded & Alignment Check")]
    public float groundCheckDistance = 1.2f;
    public float autoAlignRayDistance = 5.0f;
    public float smoothRecoverySpeed = 15f;

    [Header("Asphalt 8 Speed Boost Settings")]
    public float landingBoostForce = 25f;
    public float boostDuration = 0.5f;
    public float airForwardPreservation = 5f;

    [Header("UI & Stunt Reward Settings")]
    public TextMeshProUGUI stuntText;
    public float nitroRewardPercent = 0.05f; // Her tam tur için %5 Nitro

    // Internal State Variables
    private Rigidbody rb;
    private RCCP_CarController rccpVehicle;
    private RCCP_Nos rccpNos;

    private bool isGrounded;
    private bool wasGrounded;

    private ActiveStuntType currentStunt = ActiveStuntType.None;
    private ActiveStuntType lastRampType = ActiveStuntType.None;

    private bool isRolling = false;
    private bool isSpinning = false;
    private bool isRecovering = false;

    private float stuntDirection = 0f;

    // Stunt Degree Tracking
    private float totalStuntRotation = 0f;
    private float lastRotationAngle = 0f;
    private Coroutine hideTextCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rccpVehicle = GetComponent<RCCP_CarController>();
        rccpNos = GetComponent<RCCP_Nos>();

        if (stuntText != null) stuntText.gameObject.SetActive(false);
    }

    void Update()
    {
        CheckGrounded();

        if (wasGrounded && !isGrounded) OnTakeoff();
        if (!wasGrounded && isGrounded) OnLanding();

        wasGrounded = isGrounded;
    }

    void FixedUpdate()
    {
        // --- 1. RAMPADAYKEN YERÇEKÝMÝ / BASTIRMA KUVVETÝ ---
        if (isGrounded)
        {
            ApplyRampDownforce();
        }

        // --- 2. HAVADAKÝ HAREKETLER VE ÝNÝÞ DÜZELTMELERÝ ---
        if (!isGrounded)
        {
            if (currentStunt == ActiveStuntType.None && !isRecovering)
            {
                TryStartStuntInAir();
            }

            PreserveForwardVelocity();

            Vector3 rayOrigin = transform.position - (transform.up * 0.5f);

#if UNITY_6000_0_OR_NEWER
            bool isFalling = rb.linearVelocity.y < 0.5f;
#else
            bool isFalling = rb.velocity.y < 0.5f;
#endif

            if (isFalling && Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, autoAlignRayDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (currentStunt == ActiveStuntType.Spin)
                {
                    SpinAndAlignForward(hit);
                }
                else
                {
                    isRolling = false;
                    isSpinning = false;
                    isRecovering = true;
                    AlignToGroundSmoothly(hit);
                }
            }
            else if (isRolling)
            {
                HandleAirRoll();
            }
            else if (isSpinning)
            {
                HandleAirSpin();
            }
            else if (isRecovering)
            {
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitRecover, autoAlignRayDistance * 1.5f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    if (currentStunt == ActiveStuntType.Spin)
                        SpinAndAlignForward(hitRecover);
                    else
                        AlignToGroundSmoothly(hitRecover);
                }
                else
                {
                    isRecovering = false;
                    if (currentStunt == ActiveStuntType.Roll) isRolling = true;
                }
            }
        }
        else
        {
            if (isRecovering)
            {
                Vector3 rayOrigin = transform.position - (transform.up * 0.5f);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitGround, autoAlignRayDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    if (currentStunt == ActiveStuntType.Spin)
                        SpinAndAlignForward(hitGround);
                    else
                        AlignToGroundSmoothly(hitGround);
                }
                else
                {
                    FinishRecovery();
                }
            }
        }
    }

    void CheckGrounded()
    {
        Vector3 rayOrigin = transform.position;
        isGrounded = Physics.Raycast(rayOrigin, -transform.up, out RaycastHit hit, groundCheckDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            if (hit.collider.CompareTag(rollRampTag))
            {
                lastRampType = ActiveStuntType.Roll;
            }
            else if (hit.collider.CompareTag(stuntRampTag))
            {
                lastRampType = ActiveStuntType.Spin;
            }
        }
    }

    void OnTakeoff()
    {
        TryStartStuntInAir();
    }

    void TryStartStuntInAir()
    {
        float currentSteer = rccpVehicle != null ? rccpVehicle.steerInput_V : Input.GetAxis("Horizontal");

        if (lastRampType == ActiveStuntType.Roll && Mathf.Abs(currentSteer) > minSteerForRoll)
        {
            currentStunt = ActiveStuntType.Roll;
            isRolling = true;
            isRecovering = false;
            stuntDirection = -Mathf.Sign(currentSteer);
            ResetRotationTracker(transform.eulerAngles.z);
        }
        else if (lastRampType == ActiveStuntType.Spin && Mathf.Abs(currentSteer) > minSteerForSpin)
        {
            currentStunt = ActiveStuntType.Spin;
            isSpinning = true;
            isRecovering = false;
            stuntDirection = Mathf.Sign(currentSteer);
            ResetRotationTracker(transform.eulerAngles.y);
        }
    }

    void ResetRotationTracker(float initialAngle)
    {
        totalStuntRotation = 0f;
        lastRotationAngle = initialAngle;
    }

    void TrackRotation(float currentAngle)
    {
        float delta = Mathf.DeltaAngle(lastRotationAngle, currentAngle);
        totalStuntRotation += Mathf.Abs(delta);
        lastRotationAngle = currentAngle;
    }

    void OnLanding()
    {
        if (isRolling || isSpinning || isRecovering)
        {
            int completedCount = Mathf.FloorToInt(totalStuntRotation / 300f); // 300 derece toleranslý tam tur hesabý

            if (completedCount > 0)
            {
                // UI Güncelleme
                if (stuntText != null)
                {
                    string stuntName = (currentStunt == ActiveStuntType.Roll) ? "BARREL ROLL" : "FLAT SPIN";
                    stuntText.text = $"{completedCount}x {stuntName}!";
                    stuntText.gameObject.SetActive(true);

                    if (hideTextCoroutine != null) StopCoroutine(hideTextCoroutine);
                    hideTextCoroutine = StartCoroutine(HideStuntTextAfterDelay(2f));
                }

                // Nitro Ekleme
                if (rccpNos != null)
                {
                    float addedTimer = rccpNos.durationTime * (nitroRewardPercent * completedCount);
                    rccpNos.timer = Mathf.Clamp(rccpNos.timer + addedTimer, 0f, rccpNos.durationTime);
                }

                // BAÞARILI ÝNÝÞ ÝÇÝN EKSTRA COÞKU/ALKIÞ
                if (RCCP_CrowdManager.Instance != null) RCCP_CrowdManager.Instance.TriggerCrowdCheer();
            }

            isRolling = false;
            isSpinning = false;
            isRecovering = true;

            StopAllCoroutines();
            StartCoroutine(SmoothLandingBoost());
        }

        // Deðiþkenleri ve rotation takibini güvenli sýfýrla
        ResetRotationTracker(0f);
        lastRampType = ActiveStuntType.None;
    }

    IEnumerator HideStuntTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (stuntText != null) stuntText.gameObject.SetActive(false);
    }

    void FinishRecovery()
    {
        isRolling = false;
        isSpinning = false;
        isRecovering = false;
        currentStunt = ActiveStuntType.None;

        rb.angularVelocity = Vector3.zero;
    }

    IEnumerator SmoothLandingBoost()
    {
        float timer = 0f;

        while (timer < boostDuration)
        {
            timer += Time.fixedDeltaTime;
            float progress = timer / boostDuration;
            float smoothFactor = Mathf.Lerp(1f, 0f, progress);

            rb.AddForce(transform.forward * (landingBoostForce * smoothFactor), ForceMode.Acceleration);

            yield return new WaitForFixedUpdate();
        }
    }

    void PreserveForwardVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        Vector3 currentVel = rb.linearVelocity;
#else
        Vector3 currentVel = rb.velocity;
#endif
        Vector3 horizontalVel = new(currentVel.x, 0f, currentVel.z);

        if (horizontalVel.sqrMagnitude > 1f)
        {
            Vector3 preservedVel = horizontalVel.normalized * (horizontalVel.magnitude + (airForwardPreservation * Time.fixedDeltaTime));

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector3(preservedVel.x, rb.linearVelocity.y, preservedVel.z);
#else
            rb.velocity = new Vector3(preservedVel.x, rb.velocity.y, preservedVel.z);
#endif
        }
    }

    void HandleAirRoll()
    {
        rb.angularVelocity = transform.forward * (stuntDirection * rollTorque);
        TrackRotation(transform.eulerAngles.z);
    }

    void HandleAirSpin()
    {
        Vector3 currentEuler = transform.eulerAngles;
        Quaternion flatRotation = Quaternion.Euler(0f, currentEuler.y, 0f);

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, flatRotation, Time.fixedDeltaTime * 12f));
        rb.angularVelocity = new Vector3(0f, stuntDirection * spinTorque, 0f);
        TrackRotation(transform.eulerAngles.y);
    }

    void AlignToGroundSmoothly(RaycastHit hit)
    {
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);

#if UNITY_6000_0_OR_NEWER
        Vector3 moveDir = new(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
#else
        Vector3 moveDir = new(rb.velocity.x, 0f, rb.velocity.z);
#endif
        if (moveDir.sqrMagnitude < 0.5f) moveDir = transform.forward;

        Vector3 projectedForward = Vector3.ProjectOnPlane(moveDir.normalized, hit.normal);
        Quaternion targetRotation = Quaternion.LookRotation(projectedForward, hit.normal);

        float distanceFactor = Mathf.Clamp01(1f - (hit.distance / autoAlignRayDistance));
        float blendSpeed = Mathf.Lerp(smoothRecoverySpeed, smoothRecoverySpeed * 2.5f, distanceFactor);

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * blendSpeed));

        if (Quaternion.Angle(rb.rotation, targetRotation) < 3.0f)
        {
            FinishRecovery();
        }
    }

    void SpinAndAlignForward(RaycastHit hit)
    {
#if UNITY_6000_0_OR_NEWER
        Vector3 moveDir = new(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
#else
        Vector3 moveDir = new(rb.velocity.x, 0f, rb.velocity.z);
#endif
        if (moveDir.sqrMagnitude < 0.5f) moveDir = transform.forward;

        Vector3 projectedForward = Vector3.ProjectOnPlane(moveDir.normalized, hit.normal);
        Quaternion targetRotation = Quaternion.LookRotation(projectedForward, hit.normal);

        // Yaw açýsý hizalamasýnda MoveRotation kullanarak fiziði daha stabil tutalým
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * smoothRecoverySpeed));

        if (Quaternion.Angle(rb.rotation, targetRotation) < 5.0f)
        {
            FinishRecovery();
        }
    }

    void ApplyRampDownforce()
    {
        if (Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, 3f))
        {
            if (hit.collider.CompareTag("LoopRamp"))
            {
                rb.AddForce(-hit.normal * 15000f, ForceMode.Force);

                Quaternion targetRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * 10f));
            }
        }
    }
}