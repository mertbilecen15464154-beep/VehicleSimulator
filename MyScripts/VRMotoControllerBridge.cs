using UnityEngine;
using BNG;
using SMPScripts; // Satýn aldýðýn paketin namespace'i

public class VRMotoControllerBridge : MonoBehaviour
{
    [Header("Motor Çalýþtýrma & Korna Ayarlarý")]
    public AudioSource hornAudioSource;       // Korna sesi
    public AudioSource ignitionAudioSource;   // Marþ basma sesi
    public MonoBehaviour motoSoundScript;     // MotoSound scripti

    private bool isEngineRunning = false;
    private bool isStartingEngine = false;    // Marþ iþlemi þu an aktif mi?
    private Coroutine ignitionCoroutine;      // Marþ zamanlayýcý referansý

    [Header("Moto Controller Referanslarý")]
    [Tooltip("Motor prefab'ýnýn üzerindeki MotoController scriptini buraya sürükle.")]
    public MotoController motoController;

    [Tooltip("Motor üzerindeki animasyonlarý yöneten script.")]
    public MotoAnimController motoAnimController;

    [Header("VR Gidon Etkileþim Ayarlarý")]
    [Tooltip("Sol gidona koyduðun BNG Grabbable objesi")]
    public Grabbable leftHandlebar;

    [Tooltip("Sað gidona (Gaz kolu) koyduðun BNG Grabbable objesi")]
    public Grabbable rightHandlebar;

    [Tooltip("Gidonun fiziksel olarak baðlý olduðu ve el yardýmýyla dönen Transform model objesi")]
    public Transform steeringHandlebarParent;

    [Tooltip("Gidonun sýnýrlandýrýlmýþ maksimum dönüþ açýsý (Derece cinsinden)")]
    public float maxSteerAngle = 40f;

    [Header("VR Hassasiyet Ayarlarý")]
    [Range(0.5f, 2f)] public float throttleSensitivity = 1f;
    [Tooltip("Gidon serbest býrakýldýðýnda ne kadar sürede düzelsin?")]
    public float steeringReturnSpeed = 7f;

    [Header("VR Wheelie (Ön Kaldýrma) Ayarlarý")]
    [Tooltip("Elleri ilk tuttuðumuz andan itibaren kaç metre yukarý kaldýrýrsak wheelie tetiklensin?")]
    public float wheelieYThreshold = 0.12f; // 12 santim yukarý çekince tetiklenir

    private float initialGrabLeftHandY = 0f;
    private float initialGrabRightHandY = 0f;

    [Header("VR Binme / Ýnme & Koltuk Ayarlarý")]
    [Tooltip("BNG Player Controller (XR Rig) referansý.")]
    public BNGPlayerController bngPlayer;
    [Tooltip("Oyuncunun motorda oturacaðý tam nokta")]
    public Transform bikeSeatPoint;
    [Tooltip("Oyuncu motordan indiðinde ýþýnlanacaðý güvenli nokta (Motorun yaný)")]
    public Transform bikeExitPoint;
    [Tooltip("Motora ne kadar yaklaþýldýðýnda biniþ yazýsý çýksýn?")]
    public float interactionDistance = 1.5f; // Tam istediðin gibi 1.5f yapýldý

    // VR'dan üreteceðimiz net eksen girdileri
    public float ThrottleInput { get; private set; }
    private Quaternion originalHandlebarRotation;
    private bool isHoldingAnyHandlebar = false;
    private bool isPlayerOnBike = false;

    private float vrSteer = 0f;
    private float vrThrottle = 0f;
    private readonly float vrRawThrottle = 0f;

    private float initialGrabHandZ = 0f;
    private bool wasLeftHeldLastFrame = false;
    private bool wasRightHeldLastFrame = false;

    void Start()
    {
        if (motoController == null) motoController = GetComponent<MotoController>();
        if (motoAnimController == null) motoAnimController = GetComponentInChildren<MotoAnimController>();
        if (bngPlayer == null) bngPlayer = FindFirstObjectByType<BNGPlayerController>();

        if (steeringHandlebarParent != null)
        {
            originalHandlebarRotation = steeringHandlebarParent.localRotation;
        }

        ResetInputs();
        InjectInputsToMotoController();

        isEngineRunning = false;
    }

    void Update()
    {
        // --- BÝNME & ÝNME ETKÝLEÞÝM MANTIÐI ---
        if (!isPlayerOnBike)
        {
            HandleOutsideInteraction();
            ResetInputs();
            InjectInputsToMotoController();
            return;
        }
        else
        {
            HandleInsideInteraction();
        }

        // --- SÜRÜÞ VE GÝDON HESAPLAMALARI ---
        CheckGripStatus();

        if (isHoldingAnyHandlebar)
        {
            CalculateVRInputs();
        }
        else
        {
            ResetInputs();
        }

        InjectInputsToMotoController();
    }

    private void HandleOutsideInteraction()
    {
        if (bngPlayer == null) return;

        float dist = Vector3.Distance(bngPlayer.transform.position, transform.position);

        if (dist <= interactionDistance)
        {
            if (GameUI.Instance) GameUI.Instance.ShowText("PRESS (A) TO MOUNT BIKE", this);

            if (InputBridge.Instance.AButtonDown)
            {
                MountBike();
            }
        }
        else
        {
            if (GameUI.Instance) GameUI.Instance.HideText(this);
        }
    }

    private void HandleInsideInteraction()
    {
        if (InputBridge.Instance.AButtonDown)
        {
            DismountBike();
        }
    }

    public void MountBike()
    {
        if (isPlayerOnBike || bngPlayer == null || bikeSeatPoint == null) return;

        isPlayerOnBike = true;
        bngPlayer.AttachPlayerToSeat(bikeSeatPoint);

        if (GameUI.Instance) GameUI.Instance.HideText(this);

        /*if (motoAnimController != null)
        {
            motoAnimController.VR_EnterVehicle();
        }*/
    }

    public void DismountBike()
    {
        if (!isPlayerOnBike || bngPlayer == null || bikeExitPoint == null) return;

        isPlayerOnBike = false;

        // Sol elciði tutan grabber'ý bul ve güvenli þekilde DropItem yap (Görseldeki pürüzsüz kullaným)
        if (leftHandlebar != null && leftHandlebar.BeingHeld)
        {
            var holdingGrabbers = leftHandlebar.HeldByGrabbers;
            if (holdingGrabbers != null && holdingGrabbers.Count > 0)
            {
                leftHandlebar.DropItem(holdingGrabbers[0]);
            }
        }

        // Sað elciði tutan grabber'ý bul ve güvenli þekilde DropItem yap
        if (rightHandlebar != null && rightHandlebar.BeingHeld)
        {
            var holdingGrabbers = rightHandlebar.HeldByGrabbers;
            if (holdingGrabbers != null && holdingGrabbers.Count > 0)
            {
                rightHandlebar.DropItem(holdingGrabbers[0]);
            }
        }

        bngPlayer.DetachPlayerFromSeat(bikeExitPoint);

        /*if (motoAnimController != null)
        {
            motoAnimController.VR_ExitVehicle();
        }*/

        ResetInputs();
        InjectInputsToMotoController();
    }

    private void CheckGripStatus()
    {
        bool leftHeld = leftHandlebar != null && leftHandlebar.BeingHeld;
        bool rightHeld = rightHandlebar != null && rightHandlebar.BeingHeld;

        isHoldingAnyHandlebar = leftHeld || rightHeld;
    }

    private void CalculateVRInputs()
    {
        // BNG InputBridge buton durumlarý
        bool bButtonPressed = InputBridge.Instance.BButton; // Sað el B tuþu
        bool xButtonPressed = InputBridge.Instance.XButton; // Sol el X tuþu

        // --- 1. KORNA MANTIÐI ---
        if (xButtonPressed && hornAudioSource != null)
        {
            if (!hornAudioSource.isPlaying) hornAudioSource.Play();
        }
        else if (!xButtonPressed && hornAudioSource != null && hornAudioSource.isPlaying)
        {
            hornAudioSource.Stop();
        }

        // El takipleri
        bool leftHeld = leftHandlebar != null && leftHandlebar.BeingHeld;
        bool rightHeld = rightHandlebar != null && rightHandlebar.BeingHeld;

        // --- 2. YENÝ COROUTINE TABANLI MARÞ SÝSTEMÝ ---
        if (!isEngineRunning)
        {
            // Sað elcik tutuluyor VE B tuþuna basýlýyorsa marþý baþlat
            if (rightHeld && bButtonPressed)
            {
                if (!isStartingEngine)
                {
                    ignitionCoroutine = StartCoroutine(StartEngineRoutine(rightHandlebar));
                }
            }
            else
            {
                // Elini çekerse veya B'yi býrakýrsa marþý anýnda iptal et ve sustur
                if (isStartingEngine)
                {
                    StopCoroutine(ignitionCoroutine);
                    isStartingEngine = false;
                    if (ignitionAudioSource != null && ignitionAudioSource.isPlaying) ignitionAudioSource.Stop();
                }
            }

            // MOTOR ÇALIÞMIYORSA: Girdileri sýfýrla, sadece gidon dönebilsin
            if (motoController != null) motoController.steerControls.y = 0f;
            HandleSteeringVisualsOnly(leftHeld, rightHeld);
            return; // Motor çalýþana kadar aþaðýdaki kodlara geçiþi engelle
        }

        // =========================================================================
        // MOTOR ÇALIÞTIYSA BUNDAN SONRAKÝ FÝZÝK VE GAZ/FREN KODLARI AKTÝF
        // =========================================================================

        // ÇÝFT EL DÝREKSÝYON & WHEELIE
        if (leftHeld && rightHeld)
        {
            Transform leftGrabber = leftHandlebar.HeldByGrabbers[0].transform;
            Transform rightGrabber = rightHandlebar.HeldByGrabbers[0].transform;

            if (!wasLeftHeldLastFrame || !wasRightHeldLastFrame)
            {
                initialGrabLeftHandY = leftGrabber.position.y;
                initialGrabRightHandY = rightGrabber.position.y;
                wasLeftHeldLastFrame = true;
                wasRightHeldLastFrame = true;
            }

            Vector3 localLeftHandPos = transform.InverseTransformPoint(leftGrabber.position);
            Vector3 localRightHandPos = transform.InverseTransformPoint(rightGrabber.position);

            float currentHandDiffZ = localRightHandPos.z - localLeftHandPos.z;
            float targetSteer = Mathf.Clamp(-currentHandDiffZ / 0.15f, -1f, 1f);
            vrSteer = Mathf.Lerp(vrSteer, targetSteer, Time.deltaTime * 12f);

            // Copilot Donaným Hýzý Okumasý
            float leftHandVelocityY = InputBridge.Instance.GetControllerVelocity(ControllerHand.Left).y;
            float rightHandVelocityY = InputBridge.Instance.GetControllerVelocity(ControllerHand.Right).y;

            float leftHandDeltaY = leftGrabber.position.y - initialGrabLeftHandY;
            float rightHandDeltaY = rightGrabber.position.y - initialGrabRightHandY;
            float averageDeltaY = (leftHandDeltaY + rightHandDeltaY) / 2f;

            if (leftHandVelocityY > 0.7f && rightHandVelocityY > 0.7f && averageDeltaY > 0.02f)
            {
                if (motoController != null) motoController.wheelieInput = true;
            }

            if (motoController != null && motoController.wheelieInput)
            {
                float wheelieHeightFactor = Mathf.Clamp01(averageDeltaY / 0.25f);
                motoController.wheeliePower = (motoController.steerControls.y * 200f) * wheelieHeightFactor;

                if (averageDeltaY < wheelieYThreshold)
                {
                    motoController.wheelieInput = false;
                    motoController.wheeliePower = 0f;
                }
            }
        }
        // TEK EL SAÐ
        else if (rightHeld && rightHandlebar.HeldByGrabbers != null && rightHandlebar.HeldByGrabbers.Count > 0)
        {
            wasLeftHeldLastFrame = false;
            if (motoController != null) { motoController.wheelieInput = false; motoController.wheeliePower = 0f; }

            Transform rightGrabberTransform = rightHandlebar.HeldByGrabbers[0].transform;
            Vector3 localHandPos = transform.InverseTransformPoint(rightGrabberTransform.position);

            if (!wasRightHeldLastFrame)
            {
                initialGrabHandZ = localHandPos.z;
                wasRightHeldLastFrame = true;
            }

            float deltaZ = localHandPos.z - initialGrabHandZ;
            float targetSteer = Mathf.Clamp(-deltaZ * 4f, -1f, 1f);
            vrSteer = Mathf.Lerp(vrSteer, targetSteer, Time.deltaTime * 10f);
        }
        // TEK EL SOL
        else if (leftHeld && leftHandlebar.HeldByGrabbers != null && leftHandlebar.HeldByGrabbers.Count > 0)
        {
            wasRightHeldLastFrame = false;
            if (motoController != null) { motoController.wheelieInput = false; motoController.wheeliePower = 0f; }

            Transform leftGrabberTransform = leftHandlebar.HeldByGrabbers[0].transform;
            Vector3 localHandPos = transform.InverseTransformPoint(leftGrabberTransform.position);

            if (!wasLeftHeldLastFrame)
            {
                initialGrabHandZ = localHandPos.z;
                wasLeftHeldLastFrame = true;
            }

            float deltaZ = localHandPos.z - initialGrabHandZ;
            float targetSteer = Mathf.Clamp(deltaZ * 4f, -1f, 1f);
            vrSteer = Mathf.Lerp(vrSteer, targetSteer, Time.deltaTime * 10f);
        }
        else
        {
            wasLeftHeldLastFrame = false;
            wasRightHeldLastFrame = false;
            if (motoController != null) { motoController.wheelieInput = false; motoController.wheeliePower = 0f; }
            vrSteer = Mathf.MoveTowards(vrSteer, 0f, Time.deltaTime * steeringReturnSpeed);
        }

        if (steeringHandlebarParent != null)
        {
            steeringHandlebarParent.localRotation = Quaternion.Slerp(steeringHandlebarParent.localRotation, Quaternion.Euler(0f, vrSteer * maxSteerAngle, 0f), Time.deltaTime * 12f);
        }

        if (motoController != null) motoController.steerControls.x = vrSteer;

        // --- GAZ & FREN GÝRDÝ KANALI ---
        float finalThrottleAndBrake = 0f;

        if (rightHandlebar != null && rightHandlebar.BeingHeld)
        {
            vrThrottle = Mathf.Clamp01(InputBridge.Instance.RightTrigger * throttleSensitivity);
            finalThrottleAndBrake = vrThrottle;
        }
        else
        {
            vrThrottle = 0f;
        }
        ThrottleInput = vrThrottle;

        if (leftHandlebar != null && leftHandlebar.BeingHeld)
        {
            float leftTrigger = InputBridge.Instance.LeftTrigger;
            if (leftTrigger > 0.1f)
            {
                finalThrottleAndBrake = -leftTrigger;
                if (leftHandlebar.HeldByGrabbers != null && leftHandlebar.HeldByGrabbers.Count > 0)
                {
                    InputBridge.Instance.VibrateController(0.2f, 0.3f, 0.05f, leftHandlebar.HeldByGrabbers[0].HandSide);
                }
            }
        }

        if (motoController != null)
        {
            motoController.steerControls.y = finalThrottleAndBrake;
        }
    }

    private void HandleSteeringVisualsOnly(bool leftHeld, bool rightHeld)
    {
        if (leftHeld && rightHeld)
        {
            float currentHandDiffZ = transform.InverseTransformPoint(rightHandlebar.HeldByGrabbers[0].transform.position).z - transform.InverseTransformPoint(leftHandlebar.HeldByGrabbers[0].transform.position).z;
            vrSteer = Mathf.Lerp(vrSteer, Mathf.Clamp(-currentHandDiffZ / 0.15f, -1f, 1f), Time.deltaTime * 12f);
        }
        else if (rightHeld)
        {
            float deltaZ = transform.InverseTransformPoint(rightHandlebar.HeldByGrabbers[0].transform.position).z - initialGrabHandZ;
            vrSteer = Mathf.Lerp(vrSteer, Mathf.Clamp(-deltaZ * 4f, -1f, 1f), Time.deltaTime * 10f);
        }
        else if (leftHeld)
        {
            float deltaZ = transform.InverseTransformPoint(leftHandlebar.HeldByGrabbers[0].transform.position).z - initialGrabHandZ;
            vrSteer = Mathf.Lerp(vrSteer, Mathf.Clamp(deltaZ * 4f, -1f, 1f), Time.deltaTime * 10f);
        }
        else
        {
            vrSteer = Mathf.MoveTowards(vrSteer, 0f, Time.deltaTime * steeringReturnSpeed);
        }

        if (steeringHandlebarParent != null)
        {
            steeringHandlebarParent.localRotation = Quaternion.Slerp(steeringHandlebarParent.localRotation, Quaternion.Euler(0f, vrSteer * maxSteerAngle, 0f), Time.deltaTime * 12f);
        }
        if (motoController != null) motoController.steerControls.x = vrSteer;
    }

    public void ResetInputs()
    {
        vrSteer = 0f;
        vrThrottle = 0f;
        ThrottleInput = 0f;

        if (motoController != null)
        {
            motoController.steerControls = Vector2.zero;
        }
    }

    private void InjectInputsToMotoController()
    {
        if (motoController == null) return;

        // Hesaplanan o harika fiziksel girdileri pakete enjekte ediyoruz
        motoController.customSteerAxis = vrSteer;
        motoController.customLeanAxis = vrSteer; // Gerçekçi motor yatýþ hissi için direksiyon ekseniyle senkronize
        motoController.customAccelerationAxis = vrThrottle;
        motoController.rawCustomAccelerationAxis = vrRawThrottle;
    }

    // Arka planda donanýmdan baðýmsýz 1 saniye sayacak marþ sistemi
    private System.Collections.IEnumerator StartEngineRoutine(Grabbable rightHand)
    {
        isStartingEngine = true;

        // Marþ sesini baþlat
        if (ignitionAudioSource != null) ignitionAudioSource.Play();

        float elapsed = 0f;
        while (elapsed < 1.0f)
        {
            elapsed += Time.unscaledDeltaTime; // Kare atlamalarýndan etkilenmeyen saf zaman

            // Oyuncuya marþ hissiyatý için pýt pýt titreþim ver
            if (rightHand != null && rightHand.HeldByGrabbers != null && rightHand.HeldByGrabbers.Count > 0)
            {
                InputBridge.Instance.VibrateController(0.1f, 0.15f, 0.02f, rightHand.HeldByGrabbers[0].HandSide);
            }
            yield return null;
        }

        // 1 saniye baþarýyla dolduysa motoru resmen ateþle!
        isEngineRunning = true;
        isStartingEngine = false;

        if (ignitionAudioSource != null) ignitionAudioSource.Stop();

        // Paketin asýl ses scriptini ayaða kaldýrýyoruz, artýk gürleyecek!
        if (motoSoundScript != null)
        {
            motoSoundScript.enabled = true;

            // Eðer referans dýþarýya açýk deðilse ama ayný GameObject üzerindeyse:
            if (motoSoundScript.TryGetComponent<AudioSource>(out var mainAudio))
            {
                mainAudio.Play();
            }
        }

        // Çalýþma anýnda ele güçlü bir patlama hissi ver (0.4 saniye)
        if (rightHand != null && rightHand.HeldByGrabbers != null && rightHand.HeldByGrabbers.Count > 0)
        {
            InputBridge.Instance.VibrateController(0.4f, 0.6f, 0.4f, rightHand.HeldByGrabbers[0].HandSide);
        }
    }

    public void StopEngine()
    {
        isEngineRunning = false;
        isStartingEngine = false;

        // Çalýþýyorsa marþ sesini kes
        if (ignitionAudioSource != null && ignitionAudioSource.isPlaying)
            ignitionAudioSource.Stop();

        // Ana motor sesini sustur ve scripti uyut
        if (motoSoundScript != null)
        {
            if (motoSoundScript.TryGetComponent<AudioSource>(out var mainAudio))
            {
                mainAudio.Stop();
            }

            motoSoundScript.enabled = false;
        }

        // MotoController fizik girdilerini sýfýrla ki motor stop edince kendi kendine akýp gitmesin
        if (motoController != null)
        {
            motoController.customAccelerationAxis = 0f;
            motoController.rawCustomAccelerationAxis = 0f;
        }
    }
}