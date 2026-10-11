using UnityEngine;
using BNG;

namespace BNG
{
    public class FuelPumpSystem : MonoBehaviour
    {
        [Header("Components (Auto-Assigned)")]
        private Grabbable pumpGrabbable;
        private AudioSource fillingAudio;
        private AudioSource autoFillStopAudio;
        private Rigidbody pumpRigidbody;

        [Header("Nozzle Animation")]
        public Transform NozzleTriggerTransform;
        public float maxTriggerRotation = 30f;

        [Header("Settings")]
        public float fuelFillRate = 5.0f;
        public float smoothSnapSpeed = 20f;

        [Header("Debug Status")]
        public bool isLocked = false;
        public CarControllerVR currentTargetVehicle;

        private Grabber holdingGrabber;
        private bool isAutoFilling = false;

        void Start()
        {
            // 1. Temel Bileþenlerin Atanmasý
            pumpRigidbody = GetComponent<Rigidbody>();
            pumpGrabbable = GetComponent<Grabbable>();

            // 2. Seslerin Sade Þekilde Aranmasý (Objedeki 2 adet AudioSource)
            AudioSource[] audios = GetComponents<AudioSource>();

            if (audios.Length >= 2)
            {
                fillingAudio = audios[0];      // Müfettiþteki (Inspector) ilk ses bileþeni
                autoFillStopAudio = audios[1];  // Müfettiþteki ikinci ses bileþeni
            }
            else if (audios.Length == 1)
            {
                fillingAudio = audios[0];
                Debug.LogWarning($"{gameObject.name} üzerinde sadece 1 adet AudioSource bulundu!");
            }
            else
            {
                Debug.LogError($"{gameObject.name} üzerinde hiç AudioSource bulunamadý!");
            }

            // Hata kontrolü
            if (pumpGrabbable == null) Debug.LogError("Grabbable bileþeni bulunamadý!");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isLocked || !pumpGrabbable.BeingHeld) return;

            CarControllerVR vs = other.GetComponentInParent<CarControllerVR>();

            if (vs != null && vs.fuelHoleTransform == other.transform)
            {
                if (vs.fuelCap != null && vs.fuelCap.isOpen)
                {
                    currentTargetVehicle = vs;
                    LockPump();
                }
            }
        }

        void Update()
        {
            if (pumpGrabbable.BeingHeld) holdingGrabber = pumpGrabbable.GetPrimaryGrabber();

            if (isLocked)
            {
                HandleFuelLogic();

                if (currentTargetVehicle != null && currentTargetVehicle.fuelHoleTransform != null)
                {
                    // Yumuþakça depo deliðine hizalanma (Snap)
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(transform.position, currentTargetVehicle.fuelHoleTransform.position, Time.deltaTime * smoothSnapSpeed),
                        Quaternion.Slerp(transform.rotation, currentTargetVehicle.fuelHoleTransform.rotation, Time.deltaTime * smoothSnapSpeed)
                    );
                }

                if (pumpGrabbable.BeingHeld) CheckBreakDistance();

                if (currentTargetVehicle == null || (currentTargetVehicle.fuelCap != null && !currentTargetVehicle.fuelCap.isOpen))
                    UnlockPump();
            }

            if (!pumpGrabbable.BeingHeld && !isLocked) StopFilling(false);
        }

        void LockPump()
        {
            isLocked = true;
            if (pumpRigidbody != null)
            {
                pumpRigidbody.constraints = RigidbodyConstraints.FreezeAll;
                pumpRigidbody.linearVelocity = Vector3.zero;
                pumpRigidbody.angularVelocity = Vector3.zero;
            }
        }

        void UnlockPump()
        {
            isLocked = false;
            currentTargetVehicle = null;
            StopFilling(isAutoFilling);
            if (pumpRigidbody != null)
            {
                pumpRigidbody.constraints = RigidbodyConstraints.None;
            }
        }

        void CheckBreakDistance()
        {
            if (holdingGrabber == null) return;
            if (Vector3.Distance(holdingGrabber.transform.position, transform.position) > pumpGrabbable.BreakDistance)
                UnlockPump();
        }

        void HandleFuelLogic()
        {
            if (currentTargetVehicle == null || currentTargetVehicle.fuelTank == null) return;
            if (pumpGrabbable.BeingHeld && CheckPrimaryButtonDown()) isAutoFilling = !isAutoFilling;

            float triggerValue = 0f;
            if (pumpGrabbable.BeingHeld && holdingGrabber != null)
            {
                triggerValue = (holdingGrabber.HandSide == ControllerHand.Left) ? InputBridge.Instance.LeftTrigger : InputBridge.Instance.RightTrigger;
            }

            if (NozzleTriggerTransform != null)
            {
                float targetZ = isAutoFilling ? maxTriggerRotation : (triggerValue * maxTriggerRotation);
                NozzleTriggerTransform.localRotation = Quaternion.Euler(0, 0, targetZ);
            }

            if (currentTargetVehicle.fuelTank.fuelTankFillAmount >= 1.0f) { StopFilling(true); return; }

            if (isAutoFilling || triggerValue > 0.05f)
            {
                float currentFillRate = isAutoFilling ? fuelFillRate : (fuelFillRate * triggerValue);
                currentTargetVehicle.fuelTank.Refill(currentFillRate);
                if (fillingAudio && !fillingAudio.isPlaying) fillingAudio.Play();
            }
            else { if (fillingAudio && fillingAudio.isPlaying) fillingAudio.Stop(); }
        }

        void StopFilling(bool playStopSound)
        {
            if (playStopSound && isAutoFilling && autoFillStopAudio != null) autoFillStopAudio.Play();
            isAutoFilling = false;
            if (fillingAudio && fillingAudio.isPlaying) fillingAudio.Stop();
        }

        bool CheckPrimaryButtonDown()
        {
            if (holdingGrabber == null) return false;
            ControllerBinding btn = (holdingGrabber.HandSide == ControllerHand.Right) ? ControllerBinding.AButtonDown : ControllerBinding.XButtonDown;
            return InputBridge.Instance.GetControllerBindingValue(btn);
        }
    }
}