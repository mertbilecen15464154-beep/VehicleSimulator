using UnityEngine;
using BNG;

public class VR_RCCP_InputHandler : MonoBehaviour
{
    public static VR_RCCP_InputHandler Instance;

    [Header("PC Test Mode Settings")]
    [Tooltip("Týklandýðýnda VR kontrolcüler yerine Klavyeyi (WASD + Ok Tuþlarý + E + Space) aktif eder.")]
    public bool pcTestMode = false;
    [Tooltip("Klavye ile A ve D tuþlarýna basýldýðýnda direksiyonun merkeze/yönlere dönme hýzý.")]
    public float keyboardSteerSpeed = 12f;

    [Header("Auto Search Settings")]
    [Tooltip("Ýþaretliyse oyun baþladýðýnda aktif olan aracý ve bileþenlerini otomatik bulur.")]
    public bool autoFindVehicleOnStart = true;

    [Header("Current Vehicle References (Auto Filled)")]
    public RCCP_Input rccpInput;
    public RCCP_CarController carController;
    public Grabbable steeringGrabbable;

    private SteeringWheel connectedWheel;
    private RCCP_Gearbox gearbox;
    private RCCP_Engine engine;

    [Header("Settings")]
    public float joystickThreshold = 0.7f;
    private bool isHandling = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (autoFindVehicleOnStart)
        {
            AutoFindAndRegisterVehicle();
        }
    }

    /// <summary>
    /// Sahnede veya RCCP_SceneManager üzerindeki aktif aracý ve "SteeringWheel" isimli direksiyon bileþenini otomatik bulur.
    /// </summary>
    public void AutoFindAndRegisterVehicle()
    {
        RCCP_CarController targetCar;

        // 1. Öncelik: RCCP_SceneManager'daki aktif oyuncu aracýný al
        if (RCCP_SceneManager.Instance != null && RCCP_SceneManager.Instance.activePlayerVehicle != null)
        {
            targetCar = RCCP_SceneManager.Instance.activePlayerVehicle;
        }
        else
        {
            // 2. Öncelik: Sahnede aktif olan ilk RCCP_CarController'ý bul
            targetCar = FindFirstObjectByType<RCCP_CarController>();
        }

        if (targetCar == null)
        {
            Debug.LogWarning("[VR_RCCP_InputHandler] Sahnede aktif bir RCCP_CarController bulunamadý!");
            return;
        }

        // Araca ait RCCP_Input bileþenini bul
        if (!targetCar.TryGetComponent<RCCP_Input>(out var foundInput)) foundInput = targetCar.GetComponentInChildren<RCCP_Input>();

        // DIRECT STEERING WHEEL SEARCH:
        // Aracýn altýndaki tüm SteeringWheel bileþenlerini tara
        SteeringWheel foundWheelComponent = null;
        Grabbable foundGrabbable = null;

        SteeringWheel[] allWheels = targetCar.GetComponentsInChildren<SteeringWheel>(true);

        foreach (var wheel in allWheels)
        {
            // Ýsmi "SteeringWheel" olan objeyi veya ilk aktif SteeringWheel'i seç
            if (wheel.gameObject.name == "SteeringWheel" || foundWheelComponent == null)
            {
                foundWheelComponent = wheel;
                foundGrabbable = wheel.GetComponent<Grabbable>();

                // Tam eþleþme ("SteeringWheel" ismi) bulduysak döngüden çýk
                if (wheel.gameObject.name == "SteeringWheel") break;
            }
        }

        // Bulunan bileþenleri sisteme kaydet
        RegisterVehicle(targetCar, foundGrabbable, foundInput);
    }

    public void RegisterVehicle(RCCP_CarController car, Grabbable wheel, RCCP_Input inputComp)
    {
        carController = car;
        steeringGrabbable = wheel;
        rccpInput = inputComp;

        if (carController != null)
        {
            gearbox = carController.GetComponentInChildren<RCCP_Gearbox>();
            engine = carController.GetComponentInChildren<RCCP_Engine>();
        }

        if (steeringGrabbable != null)
            connectedWheel = steeringGrabbable.GetComponent<SteeringWheel>();

        if (rccpInput != null)
            rccpInput.overridePlayerInputs = true;

        isHandling = true;
        Debug.Log($"[VR_RCCP_InputHandler] Araç Baþarýyla Baðlandý: {carController.gameObject.name}");
    }

    public void UnregisterVehicle()
    {
        if (rccpInput != null)
        {
            rccpInput.steerInput = 0;
            rccpInput.throttleInput = 0;
            rccpInput.brakeInput = 0;
            rccpInput.handbrakeInput = 0;
            rccpInput.overridePlayerInputs = false;
        }

        carController = null;
        steeringGrabbable = null;
        connectedWheel = null;
        rccpInput = null;
        gearbox = null;
        engine = null;
        isHandling = false;
    }

    void Update()
    {
        // Otomatik Arama Güvenliði: Eðer araç atanmadýysa otomatik bulmayý dene
        if (!isHandling || rccpInput == null)
        {
            if (autoFindVehicleOnStart) AutoFindAndRegisterVehicle();
            return;
        }

        // -------------------------------------------------------------
        // 1. KLAVYE ÝLE TEST MODU (PC Test Mode = True)
        // -------------------------------------------------------------
        if (pcTestMode)
        {
            // MOTOR ÇALIÞTIRMA (E Tuþu)
            if (Input.GetKeyDown(KeyCode.E) && engine != null)
            {
                if (engine.engineRunning) engine.StopEngine();
                else engine.StartEngine();
            }

            // VÝTES KONTROLLERÝ (Yukarý Ok: D, Aþaðý Ok: R, N: Boþ)
            if (gearbox != null)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    gearbox.forceToNGear = false;
                    gearbox.forceToRGear = false;
                    gearbox.ShiftToGear(0);
                    gearbox.gearInput = 1f;
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    gearbox.forceToNGear = false;
                    gearbox.forceToRGear = true;
                    gearbox.ShiftReverse();
                    gearbox.gearInput = 1f;
                }
                else if (Input.GetKeyDown(KeyCode.N))
                {
                    gearbox.forceToNGear = true;
                    gearbox.forceToRGear = false;
                    gearbox.gearInput = 0f;
                }
            }

            // GAZ (W)
            rccpInput.throttleInput = Input.GetKey(KeyCode.W) ? 1f : 0f;

            // NORMAL FREN (S)
            rccpInput.brakeInput = Input.GetKey(KeyCode.S) ? 1f : 0f;

            // EL FRENI / DRIFT FRENI (Space)
            rccpInput.handbrakeInput = Input.GetKey(KeyCode.Space) ? 1f : 0f;

            // DÝREKSÝYON (A / D - Hýzlý ve seri dönüþ)
            float horizontalInput = Input.GetAxis("Horizontal");
            rccpInput.steerInput = Mathf.MoveTowards(rccpInput.steerInput, horizontalInput, Time.deltaTime * keyboardSteerSpeed);

            return; // VR kontrollerini atla
        }

        // -------------------------------------------------------------
        // 2. VR ÝLE OYNANIÞ MODU (PC Test Mode = False)
        // -------------------------------------------------------------
        rccpInput.throttleInput = InputBridge.Instance.RightTrigger;
        rccpInput.handbrakeInput = InputBridge.Instance.LeftTrigger;

        if (steeringGrabbable != null && steeringGrabbable.BeingHeld && connectedWheel != null)
        {
            rccpInput.steerInput = -connectedWheel.ScaleValue;
            HandleExtraInputs();
        }
        else
        {
            rccpInput.steerInput = Mathf.MoveTowards(rccpInput.steerInput, 0, Time.deltaTime * 2f);
        }
    }

    void HandleExtraInputs()
    {
        if (carController == null) return;
        Vector2 leftStick = InputBridge.Instance.LeftThumbstickAxis;
        if (leftStick.x > joystickThreshold) { carController.indicatorsRightLights = true; carController.indicatorsLeftLights = false; }
        else if (leftStick.x < -joystickThreshold) { carController.indicatorsLeftLights = true; carController.indicatorsRightLights = false; }
        else { carController.indicatorsLeftLights = false; carController.indicatorsLeftLights = false; }
    }
}