using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace BNG
{
    public class HShifter : GrabbableEvents
    {
        public enum TransmissionType { Manual, Automatic }

        [Header("Þanzýman Seçimi")]
        public TransmissionType gearType = TransmissionType.Manual;

        [Header("RCCP Entegrasyonu")]
        public RCCP_Gearbox gearbox;

        [Header("UI Ayarlarý")]
        public TextMeshProUGUI gearText;
        public TextMeshProUGUI speedText;

        [Header("Hareket Limitleri")]
        public float upDownOffset = 25f;
        public float rightLeftOffset = 35f;

        [Header("Kilitleme Ayarlarý")]
        [Range(0.7f, 0.95f)]
        public float lockThreshold = 0.8f;
        public float snapStrength = 100f;

        [Header("Geri Bildirim")]
        public AudioSource shiftSound;

        [Header("Vites Durumu")]
        public string currentGear = "N";
        public bool isLocked = false;

        private float angleX, angleY;
        private Vector3 targetGearRotation;

        void Start()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;

            if (gearbox != null)
            {
                gearbox.transmissionType = (gearType == TransmissionType.Manual) ?
                    RCCP_Gearbox.TransmissionType.Manual : RCCP_Gearbox.TransmissionType.Automatic_DNRP;

                // Baþlangýçta vites kilitlenmesini engellemek için shiftingNow sýfýrlandý
                gearbox.shiftingNow = false;

                // Ýlk pozisyonu Boþ (N) olarak ayarla
                UpdateRCCPGear("N");
            }
        }

        public override void OnGrab(Grabber grabber)
        {
            base.OnGrab(grabber);
            isLocked = false;
        }

        public override void OnRelease()
        {
            base.OnRelease();
            if (currentGear != "N")
            {
                targetGearRotation = GetRotationForGear(currentGear);
                isLocked = true;
                if (shiftSound != null) shiftSound.Play();
            }
        }

        void Update()
        {
            transform.localPosition = Vector3.zero;
            UpdateUI();

            if (isLocked && (grab == null || !grab.BeingHeld))
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(targetGearRotation), Time.deltaTime * 15f);
            }
            else if (grab != null && grab.BeingHeld)
            {
                DoJoystickLook();
                ApplyAngleLimits();
                CalculateGearLogicAndSnap();
            }
            else
            {
                if (gearType == TransmissionType.Manual)
                {
                    transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.identity, Time.deltaTime * 5f);
                    if (currentGear != "N") { currentGear = "N"; UpdateRCCPGear("N"); }
                }
            }
        }

        void ApplyAngleLimits()
        {
            Vector3 rot = transform.localEulerAngles;
            angleX = (rot.x > 180) ? rot.x - 360 : rot.x;
            angleY = (rot.y > 180) ? rot.y - 360 : rot.y;

            angleX = Mathf.Clamp(angleX, -upDownOffset, upDownOffset);

            if (gearType == TransmissionType.Automatic)
                angleY = 0;
            else
                angleY = Mathf.Clamp(angleY, -rightLeftOffset, rightLeftOffset);

            transform.localEulerAngles = new Vector3(angleX, angleY, 0);
        }

        void CalculateGearLogicAndSnap()
        {
            float yInput = angleX / upDownOffset;
            string detectedGear = "N";

            if (gearType == TransmissionType.Automatic)
            {
                if (yInput > lockThreshold) detectedGear = "D";
                else if (yInput < -lockThreshold) detectedGear = "R";
                else detectedGear = "N";
            }
            else
            {
                float xInput = angleY / rightLeftOffset;
                if (xInput < -0.45f)
                {
                    if (yInput > lockThreshold) detectedGear = "1";
                    else if (yInput < -lockThreshold) detectedGear = "2";
                }
                else if (xInput > 0.45f)
                {
                    if (xInput < 0.9f)
                    {
                        if (yInput > lockThreshold) detectedGear = "5";
                        else if (yInput < -lockThreshold) detectedGear = "6";
                    }
                    else if (xInput >= 0.9f)
                    {
                        if (yInput < -lockThreshold) detectedGear = "R";
                    }
                }
                else if (Mathf.Abs(xInput) < 0.25f)
                {
                    if (yInput > lockThreshold) detectedGear = "3";
                    else if (yInput < -lockThreshold) detectedGear = "4";
                }
            }

            if (currentGear != detectedGear)
            {
                currentGear = detectedGear;
                UpdateRCCPGear(currentGear);
                if (grab != null && grab.BeingHeld && grab.GetPrimaryGrabber() != null)
                    InputBridge.Instance.VibrateController(0.2f, 0.2f, 0.1f, grab.GetPrimaryGrabber().HandSide);
            }
        }

        void UpdateRCCPGear(string gearName)
        {
            if (gearbox == null) return;

            // Zorlama durumlarýný temizle
            gearbox.forceToNGear = false;
            gearbox.forceToRGear = false;

            switch (gearName)
            {
                case "N":
                    gearbox.forceToNGear = true;
                    gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.Neutral;
                    gearbox.gearInput = 0f;
                    break;

                case "R":
                    gearbox.forceToRGear = true;
                    gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.InReverseGear;
                    if (gearType == TransmissionType.Automatic)
                        gearbox.shiftingTime = 0f;

                    gearbox.ShiftReverse();
                    gearbox.gearInput = 1f;
                    break;

                case "D":
                    if (gearType == TransmissionType.Automatic)
                    {
                        gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;
                        gearbox.shiftingTime = 0.2f;
                        gearbox.ShiftToGear(0);
                        gearbox.gearInput = 1f;
                    }
                    break;

                default:
                    // Manuel vitesler (1, 2, 3, 4, 5, 6)
                    if (int.TryParse(gearName, out int gNo))
                    {
                        gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;
                        int rccpIndex = gNo - 1;
                        gearbox.ShiftToGear(rccpIndex);
                        gearbox.gearInput = 1f;
                    }
                    break;
            }
        }

        void UpdateUI()
        {
            if (gearbox == null || gearText == null) return;

            if (gearType == TransmissionType.Automatic && currentGear == "D")
            {
                if (gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.InReverseGear)
                    gearText.text = "R";
                else if (gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.Neutral)
                    gearText.text = "N";
                else
                    gearText.text = (gearbox.currentGear + 1).ToString();
            }
            else
            {
                gearText.text = currentGear;
            }

            if (speedText != null && gearbox.CarController != null)
                speedText.text = Mathf.Abs(gearbox.CarController.speed).ToString("0");
        }

        Vector3 GetRotationForGear(string gear)
        {
            if (gearType == TransmissionType.Automatic)
            {
                return gear switch
                {
                    "D" => new Vector3(upDownOffset, 0, 0),
                    "R" => new Vector3(-upDownOffset, 0, 0),
                    _ => Vector3.zero
                };
            }

            return gear switch
            {
                "1" => new Vector3(upDownOffset, -rightLeftOffset, 0),
                "2" => new Vector3(-upDownOffset, -rightLeftOffset, 0),
                "3" => new Vector3(upDownOffset, 0, 0),
                "4" => new Vector3(-upDownOffset, 0, 0),
                "5" => new Vector3(upDownOffset, rightLeftOffset, 0),
                "6" => new Vector3(-upDownOffset, rightLeftOffset, 0),
                "R" => new Vector3(-upDownOffset, rightLeftOffset + 5f, 0),
                _ => Vector3.zero
            };
        }

        void DoJoystickLook()
        {
            if (grab.GetPrimaryGrabber() == null) return;
            Vector3 localTarget = transform.InverseTransformPoint(grab.GetPrimaryGrabber().transform.position);
            Vector3 targetPos = transform.TransformPoint(localTarget);
            Quaternion targetRot = Quaternion.LookRotation(targetPos - transform.position, transform.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * 25f);
        }
    }
}