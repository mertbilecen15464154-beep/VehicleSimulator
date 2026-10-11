using BNG;
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class HorseControllerVR : MonoBehaviour
{
    [Header("VR Rider Settings")]
    public bool isRiding = false;
    public Transform saddleSeatPoint;
    public Transform exitPoint;
    public float interactionDistance = 3f;

    [Header("Movement Speeds")]
    public float walkSpeed = 3f;
    public float trotSpeed = 6f;
    public float gallopSpeed = 12f;
    public float backwardSpeed = 1.5f;
    public float rotateSpeed = 50f;
    public float brakingDeceleration = 8f;
    public float gravity = 20f;
    public float jumpForce = 8f;

    [Header("VR Reins (Dizgin) Settings")]
    public Grabbable leftReinGrabbable;
    public Grabbable rightReinGrabbable;
    public Transform reinsAnchorPoint;

    [Header("Realistic Control Thresholds")]
    public float reinWhipVelocityThreshold = 2.2f;
    public float speedDuration = 4f;
    public float stopPullDistance = 0.45f;
    public float turnSideDistance = 0.2f;

    [Header("VR Seat Stabilization")]
    public Transform saddleTargetBone; // Atýn Pelvis veya Saddle kemiðini buraya sürükle
    public float followSpeed = 10f;    // Pozisyon yumuþatma hýzý (Daha az sallantý için 5-10 arasý)
    public float rotationSmooth = 5f;  // Dönüþ yumuþatma hýzý

    [Header("Petting System")]
    public Transform headPettingZone;
    public Collider[] allowedHandColliders;
    public AudioClip[] pettingSounds;
    public ParticleSystem pettingParticles;
    public float minHandVelocityForPet = 0.12f;
    public float pettingCooldown = 2.5f;

    private float lastPetTime = -99f;
    private Vector3 lastLeftHandPetPos;
    private Vector3 lastRightHandPetPos;

    [Header("Animation Settings")]
    public Animator horseAnimator;
    public float idleTransitionSpeed = 2f;

    [Header("Audio Settings - General")]
    public AudioSource horseAudioSource;
    public AudioSource footstepAudioSource;
    public AudioClip whipSound;
    public AudioClip jumpSound;
    public AudioClip[] rearUpSounds;
    public AudioClip[] backAttackSounds;
    public AudioClip[] footstepSounds;
    public AudioClip[] idleBreaths;

    private CharacterController controller;
    private BNGPlayerController bngPlayer;
    private InputBridge _input;
    private Vector3 moveDirection = Vector3.zero;

    private float targetSpeed = 0f;
    private float currentSpeed = 0f;
    private float speedTimer = 0f;
    private int whipCount = 0;

    private float currentTurnAnim = 0f;
    private float currentSpeedAnim = 0f;
    private float targetIdleIndex = 0f;
    private float currentIdleIndexAnim = 0f;
    private float rawTurnInput = 0f;
    private bool isBraking = false;

    private Vector3 lastLeftHandPos;
    private Vector3 lastRightHandPos;

    private float whipCooldownTimer = 0f;
    private const float WHIP_COOLDOWN = 0.6f;

    private Vector3 initialLeftReinLocalPos;
    private Vector3 initialRightReinLocalPos;

    private bool isWaitingInMainIdle = true;
    private bool hasPlayedIdleSound = false;
    private float lastFootstepTime = 0f;
    private const float FOOTSTEP_DEBOUNCE_TIME = 0.15f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        _input = InputBridge.Instance;
        bngPlayer = FindFirstObjectByType<BNGPlayerController>();

        if (leftReinGrabbable != null) initialLeftReinLocalPos = leftReinGrabbable.transform.localPosition;
        if (rightReinGrabbable != null) initialRightReinLocalPos = rightReinGrabbable.transform.localPosition;

        targetIdleIndex = 0f;
        currentIdleIndexAnim = 0f;

        if (horseAudioSource == null) horseAudioSource = gameObject.AddComponent<AudioSource>();
        if (footstepAudioSource == null) footstepAudioSource = gameObject.AddComponent<AudioSource>();

        horseAudioSource.spatialBlend = 1f;
        footstepAudioSource.spatialBlend = 1f;
    }

    void Update()
    {
        if (isRiding)
        {
            HandleInsideInteraction();

            if (!IsCustomAnimationActive())
            {
                ProcessRealisticReinControls();
                HandleJump();
                HandleRearUpInput();
                HandleBackAttackInput();
            }
            else
            {
                currentSpeed = 0f;
                targetSpeed = 0f;
                whipCount = 0;
                rawTurnInput = 0f;
            }

            ApplyMovement();
            UpdateAnimations();
            ResetReins();
        }
        else
        {
            HandleOutsideInteraction();
            ResetReins();
            ApplyGravityOnly();
            UpdateAnimations();
        }

        // HER ÝKÝ DURUMDA DA (Ata bindiðinde veya inmediðinde) Idle mantýðýný çalýþtýr:
        UpdateIdleSystems();
    }

    void LateUpdate()
    {
        // Kamera ve koltuk pozisyonunu Update yerine LateUpdate'te yumuþatmak titreþimi engeller
        if (saddleSeatPoint != null && saddleTargetBone != null)
        {
            // Y eksenindeki (dikey) ani sallantýlarý sönümleyerek takip et
            Vector3 targetPos = saddleTargetBone.position;

            // Pozisyonu yumuþatýlmýþ biçimde aktar
            
            // Rotasyon sallantýlarýný yumuþat
            saddleSeatPoint.SetPositionAndRotation(Vector3.Lerp(saddleSeatPoint.position, targetPos, Time.deltaTime * followSpeed), Quaternion.Slerp(saddleSeatPoint.rotation, saddleTargetBone.rotation, Time.deltaTime * rotationSmooth));
        }
    }

    private void HandleBackAttackInput()
    {
        bool keyboardBackAttack = Input.GetKeyDown(KeyCode.F);
        bool vrBackAttack = _input != null && _input.BButtonDown;

        if ((keyboardBackAttack || vrBackAttack) && Mathf.Abs(currentSpeed) < 0.1f)
        {
            if (horseAnimator != null)
            {
                horseAnimator.SetTrigger("BackAttack");
            }

            if (backAttackSounds != null && backAttackSounds.Length > 0 && horseAudioSource != null)
            {
                int randomIndex = UnityEngine.Random.Range(0, backAttackSounds.Length);
                if (backAttackSounds[randomIndex] != null)
                {
                    horseAudioSource.PlayOneShot(backAttackSounds[randomIndex], 1f);
                }
            }
        }
    }

    private bool IsCustomAnimationActive()
    {
        if (horseAnimator == null) return false;

        AnimatorStateInfo stateInfo = horseAnimator.GetCurrentAnimatorStateInfo(0);

        bool isRearUp = stateInfo.IsName("RearUp") || stateInfo.IsTag("RearUp") || (horseAnimator.IsInTransition(0) && horseAnimator.GetNextAnimatorStateInfo(0).IsName("RearUp"));
        bool isBackAttack = stateInfo.IsName("BackAttack") || stateInfo.IsTag("BackAttack") || (transform && horseAnimator.IsInTransition(0) && horseAnimator.GetNextAnimatorStateInfo(0).IsName("BackAttack"));

        return isRearUp || isBackAttack;
    }

    private void HandleRearUpInput()
    {
        bool keyboardRearUp = Input.GetKeyDown(KeyCode.R);
        bool vrRearUp = _input != null && _input.YButtonDown;

        if ((keyboardRearUp || vrRearUp) && Mathf.Abs(currentSpeed) < 0.1f)
        {
            if (horseAnimator != null)
            {
                horseAnimator.SetTrigger("RearUp");
            }

            if (rearUpSounds != null && rearUpSounds.Length > 0 && horseAudioSource != null)
            {
                int randomIndex = UnityEngine.Random.Range(0, rearUpSounds.Length);
                if (rearUpSounds[randomIndex] != null)
                {
                    horseAudioSource.PlayOneShot(rearUpSounds[randomIndex], 1f);
                }
            }
        }
    }

    private void ProcessRealisticReinControls()
    {
        if (whipCooldownTimer > 0f) whipCooldownTimer -= Time.deltaTime;

        // VR Dizgin Tespiti
        bool vrWippedLeft = false;
        bool vrWippedRight = false;
        bool vrBrakingPull = false;
        float vrTurnInput = 0f;

        if (reinsAnchorPoint != null)
        {
            Vector3 leftHandPos = (leftReinGrabbable != null) ? leftReinGrabbable.transform.position : reinsAnchorPoint.position;
            Vector3 rightHandPos = (rightReinGrabbable != null) ? rightReinGrabbable.transform.position : reinsAnchorPoint.position;

            float leftDeltaY = leftHandPos.y - lastLeftHandPos.y;
            float rightDeltaY = rightHandPos.y - lastRightHandPos.y;

            Vector3 leftLocalPos = reinsAnchorPoint.InverseTransformPoint(leftHandPos);
            Vector3 rightLocalPos = reinsAnchorPoint.InverseTransformPoint(rightHandPos);

            float leftBackwardPull = -leftLocalPos.z;
            float rightBackwardPull = -rightLocalPos.z;

            float leftLocalX = leftLocalPos.x;
            float rightLocalX = rightLocalPos.x;

            bool leftHeld = leftReinGrabbable != null && leftReinGrabbable.BeingHeld;
            bool rightHeld = rightReinGrabbable != null && rightReinGrabbable.BeingHeld;

            float leftDownwardsVelocity = -leftDeltaY / Time.deltaTime;
            float rightDownwardsVelocity = -rightDeltaY / Time.deltaTime;

            vrWippedLeft = controller.isGrounded && leftHeld && leftDownwardsVelocity > reinWhipVelocityThreshold && whipCooldownTimer <= 0f;
            vrWippedRight = controller.isGrounded && rightHeld && rightDownwardsVelocity > reinWhipVelocityThreshold && whipCooldownTimer <= 0f;

            bool isWhippingRightNowVR = vrWippedLeft || vrWippedRight || leftDeltaY > 0.02f || rightDeltaY > 0.02f;
            vrBrakingPull = leftHeld && rightHeld && leftBackwardPull > stopPullDistance && rightBackwardPull > stopPullDistance && !isWhippingRightNowVR;

            if (rightHeld && rightLocalX > turnSideDistance) vrTurnInput = (rightLocalX - turnSideDistance) * 2f;
            else if (leftHeld && leftLocalX < -turnSideDistance) vrTurnInput = (leftLocalX + turnSideDistance) * 2f;

            lastLeftHandPos = leftHandPos;
            lastRightHandPos = rightHandPos;
        }

        // Klavye Girdileri
        bool keyboardWhip = (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.W)) && whipCooldownTimer <= 0f;
        bool keyboardBraking = Input.GetKey(KeyCode.S);

        float keyboardTurnInput = 0f;
        if (Input.GetKey(KeyCode.D)) keyboardTurnInput = 1f;
        else if (Input.GetKey(KeyCode.A)) keyboardTurnInput = -1f;

        // VR ve Klavye Girdilerini Birleþtir
        bool wipped = (vrWippedLeft || vrWippedRight || keyboardWhip) && controller.isGrounded;
        bool isBrakingInput = vrBrakingPull || keyboardBraking;

        rawTurnInput = Mathf.Abs(keyboardTurnInput) > 0.01f ? keyboardTurnInput : vrTurnInput;

        if (isBrakingInput)
        {
            whipCount = 0;
            speedTimer = 0f;
            targetSpeed = 0f;

            if (currentSpeed > 0.1f)
            {
                isBraking = true;
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, Time.deltaTime * brakingDeceleration);
            }
            else
            {
                isBraking = false;
                currentSpeed = Mathf.MoveTowards(currentSpeed, -backwardSpeed, Time.deltaTime * 2f);
            }
        }
        else
        {
            isBraking = false;

            if (wipped)
            {
                if (currentSpeed < 0f) currentSpeed = 0f;

                speedTimer = speedDuration;
                whipCount = Mathf.Clamp(whipCount + 1, 1, 3);

                if (whipCount == 1) targetSpeed = walkSpeed;
                else if (whipCount == 2) targetSpeed = trotSpeed;
                else if (whipCount == 3) targetSpeed = gallopSpeed;

                whipCooldownTimer = WHIP_COOLDOWN;

                PlayWhipSound();
            }

            if (whipCount > 0)
            {
                if (speedTimer > 0f)
                {
                    speedTimer -= Time.deltaTime;
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime * 3f);
                }
                else
                {
                    whipCount--;

                    if (whipCount == 2) { targetSpeed = trotSpeed; speedTimer = speedDuration; }
                    else if (whipCount == 1) { targetSpeed = walkSpeed; speedTimer = speedDuration; }
                    else { targetSpeed = 0f; }
                }
            }
            else
            {
                if (currentSpeed > 0f)
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, Time.deltaTime * 2f);
                else if (currentSpeed < 0f)
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, Time.deltaTime * 4f);
            }
        }

        if (Mathf.Abs(currentSpeed) > 0.3f && Mathf.Abs(rawTurnInput) > 0.02f)
        {
            float directionMultiplier = currentSpeed > 0f ? 1f : -1f;
            float actualRotateSpeed = rotateSpeed * Mathf.Clamp01(Mathf.Abs(currentSpeed) / walkSpeed);
            transform.Rotate(0, rawTurnInput * directionMultiplier * actualRotateSpeed * Time.deltaTime, 0);
        }
    }

    private void HandleJump()
    {
        bool keyboardJump = Input.GetKeyDown(KeyCode.Space);
        bool vrJump = _input != null && _input.XButtonDown;

        if ((keyboardJump || vrJump) && controller.isGrounded)
        {
            moveDirection.y = jumpForce;
            if (horseAnimator != null) horseAnimator.SetTrigger("Jump");

            if (jumpSound != null && horseAudioSource != null)
            {
                horseAudioSource.PlayOneShot(jumpSound, 0.8f);
            }
        }
    }

    private void ApplyMovement()
    {
        Vector3 forwardMovement = transform.forward * currentSpeed;
        moveDirection.x = forwardMovement.x;
        moveDirection.z = forwardMovement.z;

        if (!controller.isGrounded) moveDirection.y -= gravity * Time.deltaTime;
        else if (moveDirection.y < 0) moveDirection.y = -2f;

        controller.Move(moveDirection * Time.deltaTime);
    }

    private void ApplyGravityOnly()
    {
        if (!controller.isGrounded) moveDirection.y -= gravity * Time.deltaTime;
        else moveDirection.y = -2f;

        moveDirection.x = 0f;
        moveDirection.z = 0f;
        controller.Move(moveDirection * Time.deltaTime);
    }

    private void UpdateAnimations()
    {
        if (horseAnimator == null) return;

        float targetTurnValue = Mathf.Clamp(rawTurnInput, -1f, 1f);
        currentTurnAnim = Mathf.Lerp(currentTurnAnim, targetTurnValue, Time.deltaTime * 5f);
        horseAnimator.SetFloat("Turn", currentTurnAnim);

        float speedParam = currentSpeed;
        if (isBraking && currentSpeed > 0.1f) speedParam = -0.5f;

        currentSpeedAnim = Mathf.Lerp(currentSpeedAnim, speedParam, Time.deltaTime * 6f);
        horseAnimator.SetFloat("Speed", currentSpeedAnim);

        currentIdleIndexAnim = Mathf.MoveTowards(currentIdleIndexAnim, targetIdleIndex, Time.deltaTime * idleTransitionSpeed);
        horseAnimator.SetFloat("IdleIndex", currentIdleIndexAnim);
    }

    private void UpdateIdleSystems()
    {
        if (horseAnimator == null) return;

        // At hareket ediyorsa veya þaha kalkma/çifte atma yapýyorsa idle varyasyonunu durdur ve ana idle'a çek
        if (Mathf.Abs(currentSpeed) > 0.05f || IsCustomAnimationActive())
        {
            targetIdleIndex = 0f;
            isWaitingInMainIdle = true;
            hasPlayedIdleSound = false;
            return;
        }

        // At tamamen duruyorsa rastgele idle döngüsünü çalýþtýr
        AnimatorStateInfo stateInfo = horseAnimator.GetCurrentAnimatorStateInfo(0);
        float animationProgress = stateInfo.normalizedTime % 1f;

        if (animationProgress >= 0.98f && !horseAnimator.IsInTransition(0))
        {
            if (isWaitingInMainIdle)
            {
                targetIdleIndex = (float)UnityEngine.Random.Range(1, 4);
                isWaitingInMainIdle = false;

                if (!hasPlayedIdleSound)
                {
                    PlayRandomIdleSound();
                    hasPlayedIdleSound = true;
                }
            }
            else
            {
                targetIdleIndex = 0f;
                isWaitingInMainIdle = true;
                hasPlayedIdleSound = false;
            }
        }
        else if (animationProgress < 0.90f)
        {
            hasPlayedIdleSound = false;
        }
    }

    private void PlayWhipSound()
    {
        if (whipSound != null && horseAudioSource != null)
        {
            horseAudioSource.PlayOneShot(whipSound, 0.9f);
        }
    }

    private void PlayRandomIdleSound()
    {
        if (idleBreaths != null && idleBreaths.Length > 0 && horseAudioSource != null)
        {
            int randomIndex = UnityEngine.Random.Range(0, idleBreaths.Length);
            horseAudioSource.PlayOneShot(idleBreaths[randomIndex], 0.6f);
        }
    }

    public void PlayFootstepSoundEvent()
    {
        if (footstepAudioSource == null || footstepSounds == null || footstepSounds.Length == 0) return;

        if (Time.time - lastFootstepTime < FOOTSTEP_DEBOUNCE_TIME) return;

        AudioClip clipToPlay = footstepSounds[UnityEngine.Random.Range(0, footstepSounds.Length)];

        float volume = 0.4f;
        float pitchMin = 0.95f;
        float pitchMax = 1.05f;

        bool isTrotSpeed = currentSpeedAnim > walkSpeed + 0.5f && currentSpeedAnim <= trotSpeed + 0.5f;
        bool isTurningWhileTrot = currentSpeedAnim > walkSpeed - 0.5f && Mathf.Abs(currentTurnAnim) > 0.2f && currentSpeedAnim <= trotSpeed + 0.5f;

        if (isTrotSpeed || isTurningWhileTrot)
        {
            volume = 0.65f;
            pitchMin = 0.92f;
            pitchMax = 1.08f;
        }
        else if (currentSpeedAnim > trotSpeed + 0.5f)
        {
            volume = 0.9f;
            pitchMin = 0.88f;
            pitchMax = 1.04f;
        }

        footstepAudioSource.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
        footstepAudioSource.PlayOneShot(clipToPlay, volume);

        lastFootstepTime = Time.time;
    }

    private void HandleOutsideInteraction()
    {
        bool keyboardMount = Input.GetKeyDown(KeyCode.E);
        bool vrMount = _input != null && _input.AButtonDown;

        if (bngPlayer == null)
        {
            if (keyboardMount) MountHorse();
            return;
        }

        float dist = Vector3.Distance(bngPlayer.transform.position, transform.position);

        if (dist <= interactionDistance)
        {
            if (GameUI.Instance) GameUI.Instance.ShowText("PRESS (A) OR [E] TO MOUNT HORSE", this);
            if (vrMount || keyboardMount) MountHorse();
        }
        else
        {
            if (GameUI.Instance) GameUI.Instance.HideText(this);
        }
    }

    private void HandleInsideInteraction()
    {
        bool keyboardDismount = Input.GetKeyDown(KeyCode.E);
        bool vrDismount = _input != null && _input.AButtonDown;

        if (vrDismount || keyboardDismount) DismountHorse();
    }

    public void MountHorse()
    {
        if (bngPlayer) bngPlayer.AttachPlayerToSeat(saddleSeatPoint);
        isRiding = true;
        speedTimer = 0f;
        whipCount = 0;
        currentSpeed = 0f;
        targetSpeed = 0f;
        whipCooldownTimer = 0f;

        lastLeftHandPos = (leftReinGrabbable != null) ? leftReinGrabbable.transform.position : (reinsAnchorPoint != null ? reinsAnchorPoint.position : Vector3.zero);
        lastRightHandPos = (rightReinGrabbable != null) ? rightReinGrabbable.transform.position : (reinsAnchorPoint != null ? reinsAnchorPoint.position : Vector3.zero);

        if (GameUI.Instance) GameUI.Instance.HideText(this);
    }

    public void DismountHorse()
    {
        if (leftReinGrabbable != null && leftReinGrabbable.BeingHeld)
        {
            var holdingGrabbers = leftReinGrabbable.HeldByGrabbers;
            if (holdingGrabbers != null && holdingGrabbers.Count > 0)
            {
                leftReinGrabbable.DropItem(holdingGrabbers[0]);
            }
        }

        if (rightReinGrabbable != null && rightReinGrabbable.BeingHeld)
        {
            var holdingGrabbers = rightReinGrabbable.HeldByGrabbers;
            if (holdingGrabbers != null && holdingGrabbers.Count > 0)
            {
                rightReinGrabbable.DropItem(holdingGrabbers[0]);
            }
        }

        if (bngPlayer) bngPlayer.DetachPlayerFromSeat(exitPoint);
        isRiding = false;
        currentSpeed = 0f;

        if (GameUI.Instance) GameUI.Instance.HideText(this);
    }

    private void ResetReins()
    {
        if (leftReinGrabbable != null && !leftReinGrabbable.BeingHeld)
        {
            leftReinGrabbable.transform.localPosition = Vector3.Lerp(
                leftReinGrabbable.transform.localPosition,
                initialLeftReinLocalPos,
                Time.deltaTime * 5f
            );
        }

        if (rightReinGrabbable != null && !rightReinGrabbable.BeingHeld)
        {
            rightReinGrabbable.transform.localPosition = Vector3.Lerp(
                rightReinGrabbable.transform.localPosition,
                initialRightReinLocalPos,
                Time.deltaTime * 5f
            );
        }
    }

    // PettingZoneDetector scripti burayý çaðýracak
    public void ProcessPettingInteraction(Collider other)
    {
        // 1. LISTENIZDEKÝ COLLIDER KONTROLÜ: Çarpan collider izin verilen listede yoksa direk çýk
        if (allowedHandColliders == null || allowedHandColliders.Length == 0) return;

        bool isAllowed = false;
        for (int i = 0; i < allowedHandColliders.Length; i++)
        {
            if (allowedHandColliders[i] != null && allowedHandColliders[i] == other)
            {
                isAllowed = true;
                break;
            }
        }

        if (!isAllowed) return; // Ýzin verilen collider'lardan biri deðilse hiçbir iþlem yapma

        // 2. Cooldown kontrolü
        if (Time.time - lastPetTime < pettingCooldown) return;

        // 3. BNG Framework el tespiti (Titreþim ve yön hesabý için)
        BNG.Grabber grabber = other.GetComponentInParent<BNG.Grabber>();
        BNG.ControllerHand? handSide = null;
        if (grabber != null)
        {
            handSide = grabber.HandSide;
        }

        bool isLeft = handSide.HasValue && handSide.Value == BNG.ControllerHand.Left;
        Vector3 previousHandPos = isLeft ? lastLeftHandPetPos : lastRightHandPetPos;

        // El hýzýný hesapla
        float handSpeed = (other.transform.position - previousHandPos).magnitude / Time.deltaTime;

        if (handSpeed >= minHandVelocityForPet)
        {
            TriggerPettingEffects(handSide ?? BNG.ControllerHand.Right);
        }

        if (isLeft) lastLeftHandPetPos = other.transform.position;
        else lastRightHandPetPos = other.transform.position;
    }

    private void TriggerPettingEffects(BNG.ControllerHand handSide)
    {
        lastPetTime = Time.time;

        // 1. Özel Okþama Animasyonunu Tetikle
        if (horseAnimator != null)
        {
            horseAnimator.SetTrigger("Pet");
        }

        // 2. Ses Çal
        if (pettingSounds != null && pettingSounds.Length > 0 && horseAudioSource != null)
        {
            AudioClip randomPetSound = pettingSounds[UnityEngine.Random.Range(0, pettingSounds.Length)];
            horseAudioSource.PlayOneShot(randomPetSound, 0.75f);
        }

        // 3. Kalp / Sevgi Partikülü Çýkar
        if (pettingParticles != null)
        {
            pettingParticles.Play();
        }

        // 4. VR Dokunsal Titreþim (Haptics)
        if (_input != null)
        {
            _input.VibrateController(0.12f, 0.4f, 0.12f, handSide);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (reinsAnchorPoint == null) return;

        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = reinsAnchorPoint.localToWorldMatrix;

        Gizmos.color = Color.red;
        Vector3 brakeLineCenter = new(0f, 0f, -stopPullDistance);
        Gizmos.DrawLine(brakeLineCenter + Vector3.left * 0.25f, brakeLineCenter + Vector3.right * 0.25f);
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawLine(Vector3.zero, new(0f, 0f, -stopPullDistance));

        Gizmos.color = Color.green;
        Vector3 leftTurnPoint = new(-turnSideDistance, 0f, 0f);
        Vector3 rightTurnPoint = new(turnSideDistance, 0f, 0f);
        Gizmos.DrawLine(leftTurnPoint + Vector3.forward * 0.1f, leftTurnPoint + Vector3.back * 0.1f);
        Gizmos.DrawLine(rightTurnPoint + Vector3.forward * 0.1f, rightTurnPoint + Vector3.back * 0.1f);

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawLine(Vector3.zero, leftTurnPoint);
        Gizmos.DrawLine(Vector3.zero, rightTurnPoint);

        Gizmos.matrix = originalMatrix;
    }
}