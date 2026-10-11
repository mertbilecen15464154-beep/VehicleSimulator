using UnityEngine;
using UnityEngine.UI;
using BNG;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CarControllerVR : MonoBehaviour
{
    public enum DisplayType { Digital, Analog }
    public enum TransmissionType { Automatic, Manual }
    public enum DisplaySystemType { DisplayWithRenderer, OnlyDisplay }

    [Header("Collision Management")]
    public bool autoIgnoreInternalCollisions = true;

    [System.Serializable]
    public class DoorData
    {
        public string doorName;
        public GameObject doorObject;
        public Grabbable enterHandle;
        public Grabbable exitHandle;
        public Transform distanceReference;
        public bool isRightDoor;
        [HideInInspector] public ConfigurableJoint joint;
        [HideInInspector] public Rigidbody rb;
        [HideInInspector] public bool isLocked = false;
    }

    [Header("Vehicle Doors")]
    public List<DoorData> doors = new();

    [Header("RCCP Connections")]
    public RCCP_CarController vrifVehicle;
    public RCCP_Lights rccpLights;
    public Grabbable steeringWheelGrabbable;
    public RCCP_Input rccpInput;

    [Header("Seat Setup")]
    public Transform seatPoint;
    public Transform exitPoint;

    [Header("Fuel System")]
    public RCCP_FuelTank fuelTank;
    public RCCP_FuelCap fuelCap;
    public Transform fuelHoleTransform;

    [Header("Signal Switch (Left Stick)")]
    public Transform signalSwitch;
    public float signalAngle = 15f;
    public bool inverseSignal = false;

    [Header("Wiper Switch (Right Stick)")]
    public Transform wiperSwitch;
    public Vector3 wiperSwitchDefaultRot;
    public Vector3 wiperSwitchStep1, wiperSwitchStep2, wiperSwitchStep3;
    public float mistyMaxRotation = -10f;

    [Header("Front Wipers")]
    public Transform frontWiperLeft;
    public Vector3 frontLeftStart, frontLeftEnd;
    public Transform frontWiperRight;
    public Vector3 frontRightStart, frontRightEnd;

    [Header("Back Wiper")]
    public Transform backWiper;
    public Vector3 backStart, backEnd;
    public float backWiperSpeed = 2.0f;

    [Header("Wiper Settings")]
    public float[] wiperSpeeds = { 0f, 1.5f, 3.0f, 6.0f };
    private int wiperMode = 0;
    private bool backWiperActive = false;
    private float frontWiperTimer = 0f;
    private float backWiperTimer = 0f;
    private bool isSinglePassRunning = false;

    [Header("Dashboard UI Elements")]
    public DisplayType rpmDisplayType = DisplayType.Digital;
    public Image rpmFillImage;
    public RectTransform rpmNeedle;
    public float rpmMinAngle = 0f, rpmMaxAngle = -270f;

    public DisplayType speedDisplayType = DisplayType.Digital;
    public TextMeshProUGUI speedText;
    public RectTransform speedNeedle;
    public float speedMinAngle = 0f, speedMaxAngle = -270f;
    public float maxSpeedValue = 260f;

    public DisplayType fuelDisplayType = DisplayType.Digital;
    public Image fuelFillImage;
    public RectTransform fuelNeedle;
    public float fuelMinAngle = 45f, fuelMaxAngle = -45f;

    public Image nosFillImage;
    public Image absIcon, espIcon, tcsIcon;
    public TextMeshProUGUI gearText;
    public GameObject lowBeamActiveUI, highBeamActiveUI;
    public GameObject leftIndicatorUI, rightIndicatorUI;

    [Header("Display & Material")]
    public DisplaySystemType displaySystemType = DisplaySystemType.DisplayWithRenderer;
    public Renderer displayRenderer;
    public GameObject displayCanvas;
    public string colorPropertyName = "_BaseColor";

    [Header("Pedals")]
    public TransmissionType pedalType = TransmissionType.Automatic;
    public Transform gasPedal;
    public bool gasPedalInverse = false;
    public Transform brakePedal;
    public bool brakePedalInverse = false;
    public Transform clutchPedal;
    public bool clutchPedalInverse = false;
    public float maxPedalRotation = 20f;

    [Header("Audio & Settings")]
    public AudioSource audioSource;
    public AudioClip doorCloseSound, doorOpenSound;
    public float interactionDistance = 1.2f;
    public float enterAngle = 10f;
    public float lockAngle = 1.0f;
    public float maxRPM = 8000f;

    private BNGPlayerController bngPlayer;
    private InputBridge input;
    private bool isInVehicle = false;
    private bool isEngineRunning = false;
    private bool lightInputReset = true;
    private bool wiperInputReset = true;

    void Start()
    {
        bngPlayer = Object.FindAnyObjectByType<BNGPlayerController>();
        input = InputBridge.Instance;

        if (autoIgnoreInternalCollisions) IgnoreInternalCollisions();

        foreach (var door in doors)
        {
            door.joint = door.doorObject.GetComponent<ConfigurableJoint>();
            door.rb = door.doorObject.GetComponent<Rigidbody>();
            if (door.joint) LockDoor(door);
        }
        SetVehicleElectronics(false);
    }

    void FixedUpdate()
    {
        HandleDoorLogic();
        if (isInVehicle)
        {
            HandleInsideInteraction();
            HandleLightInputs();
            HandleIndicators();
            HandlePedals();
            HandleNosInput();
            HandleWiperInputs();
        }
        else HandleOutsideInteraction();

        CheckEngineState();
        UpdateWiperSwitchVisual();

        if (isEngineRunning)
        {
            UpdateDashboardUI();
            AnimateWipers();
            UpdateSignalSwitchVisual();
        }
    }

    void IgnoreInternalCollisions()
    {
        Collider[] allColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < allColliders.Length; i++)
        {
            for (int j = i + 1; j < allColliders.Length; j++)
            {
                if (allColliders[i] != null && allColliders[j] != null)
                {
                    Physics.IgnoreCollision(allColliders[i], allColliders[j], true);
                }
            }
        }
    }

    void HandlePedals()
    {
        if (!isEngineRunning || vrifVehicle == null) return;
        float gasRot = vrifVehicle.throttleInput_V * maxPedalRotation * (gasPedalInverse ? -1 : 1);
        if (gasPedal) gasPedal.localRotation = Quaternion.Lerp(gasPedal.localRotation, Quaternion.Euler(gasRot, 0, 0), Time.deltaTime * 15f);
        float brakeRot = vrifVehicle.brakeInput_V * maxPedalRotation * (brakePedalInverse ? -1 : 1);
        if (brakePedal) brakePedal.localRotation = Quaternion.Lerp(brakePedal.localRotation, Quaternion.Euler(brakeRot, 0, 0), Time.deltaTime * 15f);
        if (pedalType == TransmissionType.Manual && clutchPedal)
        {
            float clutchRot = vrifVehicle.clutchInput_V * maxPedalRotation * (clutchPedalInverse ? -1 : 1);
            clutchPedal.localRotation = Quaternion.Lerp(clutchPedal.localRotation, Quaternion.Euler(clutchRot, 0, 0), Time.deltaTime * 15f);
        }
    }

    void SetVehicleElectronics(bool state)
    {
        if (displayCanvas) displayCanvas.SetActive(state);
        if (displaySystemType == DisplaySystemType.DisplayWithRenderer && displayRenderer)
            displayRenderer.material.SetColor(colorPropertyName, state ? Color.white : Color.black);
    }

    public void EnterVehicle()
    {
        if (bngPlayer) bngPlayer.AttachPlayerToSeat(seatPoint);
        isInVehicle = true;

        if (VR_RCCP_InputHandler.Instance != null)
        {
            VR_RCCP_InputHandler.Instance.RegisterVehicle(vrifVehicle, steeringWheelGrabbable, rccpInput);
        }
    }

    public void ExitVehicle()
    {
        if (bngPlayer) bngPlayer.DetachPlayerFromSeat(exitPoint);
        isInVehicle = false;

        if (VR_RCCP_InputHandler.Instance != null)
        {
            VR_RCCP_InputHandler.Instance.UnregisterVehicle();
        }
    }

    void HandleWiperInputs() { if (input == null || !isEngineRunning) return; float vInput = input.RightThumbstickAxis.y; if (vInput >= 0.7f && wiperInputReset) { wiperMode = Mathf.Min(wiperMode + 1, 3); wiperInputReset = false; } else if (vInput <= -0.7f && wiperInputReset) { if (wiperMode > 0) wiperMode--; else if (!isSinglePassRunning) StartCoroutine(SingleWiperPass()); wiperInputReset = false; } if (Mathf.Abs(vInput) < 0.2f) wiperInputReset = true; if (input.RightThumbstickDown) backWiperActive = !backWiperActive; }
    void AnimateWipers() { if (isSinglePassRunning) return; if (wiperMode > 0) { frontWiperTimer += Time.deltaTime * wiperSpeeds[wiperMode]; float fLerp = Mathf.PingPong(frontWiperTimer, 1.0f); ApplyWiperRotation(frontWiperLeft, fLerp, frontLeftStart, frontLeftEnd); ApplyWiperRotation(frontWiperRight, fLerp, frontRightStart, frontRightEnd); } else { ApplyWiperRotation(frontWiperLeft, 0, frontLeftStart, frontLeftEnd); ApplyWiperRotation(frontWiperRight, 0, frontRightStart, frontRightEnd); frontWiperTimer = 0; } if (backWiperActive) { backWiperTimer += Time.deltaTime * backWiperSpeed; float bLerp = Mathf.PingPong(backWiperTimer, 1.0f); ApplyWiperRotation(backWiper, bLerp, backStart, backEnd); } else { ApplyWiperRotation(backWiper, 0, backStart, backEnd); backWiperTimer = 0; } }

    void UpdateDashboardUI()
    {
        if (vrifVehicle == null) return;

        float rpmRatio = Mathf.Clamp01(vrifVehicle.engineRPM / maxRPM);
        if (rpmDisplayType == DisplayType.Digital && rpmFillImage) rpmFillImage.fillAmount = rpmRatio;
        else if (rpmDisplayType == DisplayType.Analog && rpmNeedle) { float targetAngle = Mathf.Lerp(rpmMinAngle, rpmMaxAngle, rpmRatio); rpmNeedle.localRotation = Quaternion.Euler(0, 0, targetAngle); }

        if (speedDisplayType == DisplayType.Digital && speedText) speedText.text = vrifVehicle.absoluteSpeed.ToString("F0");
        else if (speedDisplayType == DisplayType.Analog && speedNeedle) { float speedRatio = Mathf.Clamp01(vrifVehicle.absoluteSpeed / maxSpeedValue); float targetAngle = Mathf.Lerp(speedMinAngle, speedMaxAngle, speedRatio); speedNeedle.localRotation = Quaternion.Euler(0, 0, targetAngle); }

        // --- FUEL UI GÜNCELLEMESÝ ---
        if (fuelTank != null)
        {
            float fuelRatio = fuelTank.fuelTankFillAmount;
            if (fuelDisplayType == DisplayType.Digital && fuelFillImage) fuelFillImage.fillAmount = fuelRatio;
            else if (fuelDisplayType == DisplayType.Analog && fuelNeedle)
            {
                float targetAngle = Mathf.Lerp(fuelMinAngle, fuelMaxAngle, fuelRatio);
                fuelNeedle.localRotation = Quaternion.Euler(0, 0, targetAngle);
            }
        }

        if (gearText) gearText.text = vrifVehicle.direction == 1 ? (vrifVehicle.NGearNow ? "N" : (vrifVehicle.currentGear + 1).ToString()) : "R";

        if (vrifVehicle.Stability)
        {
            if (absIcon) absIcon.color = vrifVehicle.Stability.ABSEngaged ? Color.white : new Color(0, 0, 0, 0.2f);
            if (espIcon) espIcon.color = vrifVehicle.Stability.ESPIndicatorEngaged ? Color.white : new Color(0, 0, 0, 0.2f);
            if (tcsIcon) tcsIcon.color = vrifVehicle.Stability.TCSEngaged ? Color.white : new Color(0, 0, 0, 0.2f);
        }

        if (rccpLights)
        {
            if (lowBeamActiveUI) lowBeamActiveUI.SetActive(rccpLights.lowBeamHeadlights);
            if (highBeamActiveUI) highBeamActiveUI.SetActive(rccpLights.highBeamHeadlights);
            bool blink = rccpLights.indicatorTimer < 0.5f;
            if (leftIndicatorUI) leftIndicatorUI.SetActive(blink && rccpLights.indicatorsLeft);
            if (rightIndicatorUI) rightIndicatorUI.SetActive(blink && rccpLights.indicatorsRight);
        }

        if (vrifVehicle.OtherAddonsManager?.Nos && nosFillImage) nosFillImage.fillAmount = vrifVehicle.OtherAddonsManager.Nos.amount;
    }

    void HandleLightInputs() { if (input == null || rccpLights == null) return; float v = input.LeftThumbstickAxis.y; if (v >= 0.5f && lightInputReset) { rccpLights.lowBeamHeadlights = !rccpLights.lowBeamHeadlights; if (rccpLights.lowBeamHeadlights) rccpLights.highBeamHeadlights = false; lightInputReset = false; } else if (v <= -0.5f && lightInputReset) { rccpLights.highBeamHeadlights = !rccpLights.highBeamHeadlights; if (rccpLights.highBeamHeadlights) rccpLights.lowBeamHeadlights = false; lightInputReset = false; } else if (Mathf.Abs(v) < 0.2f) lightInputReset = true; }
    void HandleDoorLogic() { foreach (var door in doors) { RestrictDoorRotation(door); bool held = (door.enterHandle && door.enterHandle.BeingHeld) || (door.exitHandle && door.exitHandle.BeingHeld); if (held) { if (door.isLocked) UnlockDoor(door); } else if (GetJointAngle(door) <= lockAngle && !door.isLocked) LockDoor(door); } }
    void RestrictDoorRotation(DoorData door) { float y = door.doorObject.transform.localEulerAngles.y; if (y > 180) y -= 360; if (!door.isRightDoor && y < 0) door.doorObject.transform.localRotation = Quaternion.identity; else if (door.isRightDoor && y > 0) door.doorObject.transform.localRotation = Quaternion.identity; }
    void LockDoor(DoorData door) { door.isLocked = true; if (door.joint) door.joint.angularYMotion = ConfigurableJointMotion.Locked; if (audioSource && doorCloseSound) audioSource.PlayOneShot(doorCloseSound); }
    void UnlockDoor(DoorData door) { door.isLocked = false; if (door.joint) door.joint.angularYMotion = ConfigurableJointMotion.Limited; if (audioSource && doorOpenSound) audioSource.PlayOneShot(doorOpenSound); }
    float GetJointAngle(DoorData door) { float a = door.doorObject.transform.localEulerAngles.y; if (a > 180) a -= 360; return Mathf.Abs(a); }
    void HandleNosInput() { if (vrifVehicle?.OtherAddonsManager?.Nos != null) vrifVehicle.OtherAddonsManager.Nos.nosInUse = input.XButton; }
    void HandleIndicators() { float h = input.LeftThumbstickAxis.x; if (h < -0.8f && lightInputReset) { rccpLights.indicatorsLeft = !rccpLights.indicatorsLeft; rccpLights.indicatorsRight = false; lightInputReset = false; } else if (h > 0.8f && lightInputReset) { rccpLights.indicatorsRight = !rccpLights.indicatorsRight; rccpLights.indicatorsLeft = false; lightInputReset = false; } }
    void CheckEngineState() { if (vrifVehicle && vrifVehicle.engineRunning != isEngineRunning) { isEngineRunning = vrifVehicle.engineRunning; SetVehicleElectronics(isEngineRunning); } }
    void ApplyWiperRotation(Transform wiper, float lerpVal, Vector3 start, Vector3 end) { if (wiper) wiper.localRotation = Quaternion.Slerp(Quaternion.Euler(start), Quaternion.Euler(end), lerpVal); }
    IEnumerator SingleWiperPass() { isSinglePassRunning = true; float t = 0; while (t < 1.0f) { t += Time.deltaTime * 3f; float lerp = Mathf.PingPong(t, 0.5f) * 2f; ApplyWiperRotation(frontWiperLeft, lerp, frontLeftStart, frontLeftEnd); ApplyWiperRotation(frontWiperRight, lerp, frontRightStart, frontRightEnd); yield return null; } isSinglePassRunning = false; }
    void UpdateWiperSwitchVisual() { if (wiperSwitch == null) return; Vector3 targetRot = wiperMode switch { 1 => wiperSwitchStep1, 2 => wiperSwitchStep2, 3 => wiperSwitchStep3, _ => wiperSwitchDefaultRot }; if (wiperMode == 0 && input.RightThumbstickAxis.y < -0.1f) { float mistyAngle = Mathf.Lerp(0, mistyMaxRotation, Mathf.Abs(input.RightThumbstickAxis.y)); targetRot = wiperSwitchDefaultRot + new Vector3(mistyAngle, 0, 0); } wiperSwitch.localRotation = Quaternion.Lerp(wiperSwitch.localRotation, Quaternion.Euler(targetRot), Time.deltaTime * 15f); }
    void UpdateSignalSwitchVisual() { if (signalSwitch == null || rccpLights == null) return; float targetAngle = 0; if (rccpLights.indicatorsLeft) targetAngle = inverseSignal ? signalAngle : -signalAngle; if (rccpLights.indicatorsRight) targetAngle = inverseSignal ? -signalAngle : signalAngle; signalSwitch.localRotation = Quaternion.Lerp(signalSwitch.localRotation, Quaternion.Euler(0, 0, targetAngle), Time.deltaTime * 10f); }
    void HandleOutsideInteraction()
    {
        if (bngPlayer == null) return; // VR Oyuncusu yoksa (PC test modundaysak) hatayý engelle

        bool show = false;
        foreach (var d in doors)
        {
            if (d.distanceReference != null && Vector3.Distance(bngPlayer.transform.position, d.distanceReference.position) <= interactionDistance && GetJointAngle(d) >= enterAngle)
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowText("PRESS (A) TO ENTER", this);
                show = true;
                if (input != null && input.AButtonDown) EnterVehicle();
                break;
            }
        }
        if (!show && GameUI.Instance != null) GameUI.Instance.HideText(this);
    }

    void HandleInsideInteraction()
    {
        if (bngPlayer == null) return; // VR Oyuncusu yoksa hatayý engelle

        bool show = false;
        foreach (var d in doors)
        {
            if (GetJointAngle(d) >= enterAngle)
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowText("PRESS (A) TO EXIT", this);
                show = true;
                if (input != null && input.AButtonDown) ExitVehicle();
                break;
            }
        }
        if (!show && GameUI.Instance != null) GameUI.Instance.HideText(this);
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(CarControllerVR))]
public class CarControllerVREditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        UnityEditor.SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            string pName = prop.name;
            if (pName == "rpmFillImage" && serializedObject.FindProperty("rpmDisplayType").enumValueIndex != (int)CarControllerVR.DisplayType.Digital) continue;
            if ((pName == "rpmNeedle" || pName == "rpmMinAngle" || pName == "rpmMaxAngle") && serializedObject.FindProperty("rpmDisplayType").enumValueIndex != (int)CarControllerVR.DisplayType.Analog) continue;
            if (pName == "speedText" && serializedObject.FindProperty("speedDisplayType").enumValueIndex != (int)CarControllerVR.DisplayType.Digital) continue;
            if ((pName == "speedNeedle" || pName == "speedMinAngle" || pName == "speedMaxAngle" || pName == "maxSpeedValue") && serializedObject.FindProperty("speedDisplayType").enumValueIndex != (int)CarControllerVR.DisplayType.Analog) continue;

            // Fuel UI gizleme/gösterme kontrolü eklendi
            if (pName == "fuelFillImage" && serializedObject.FindProperty("fuelDisplayType").enumValueIndex != (int)CarControllerVR.DisplayType.Digital) continue;
            if ((pName == "fuelNeedle" || pName == "fuelMinAngle" || pName == "fuelMaxAngle") && serializedObject.FindProperty("fuelDisplayType").enumValueIndex != (int)CarControllerVR.DisplayType.Analog) continue;

            if ((pName == "clutchPedal" || pName == "clutchPedalInverse") && serializedObject.FindProperty("pedalType").enumValueIndex != (int)CarControllerVR.TransmissionType.Manual) continue;
            if ((pName == "displayRenderer" || pName == "colorPropertyName") && serializedObject.FindProperty("displaySystemType").enumValueIndex != (int)CarControllerVR.DisplaySystemType.DisplayWithRenderer) continue;
            UnityEditor.EditorGUILayout.PropertyField(prop, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
#endif