using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRSubSystems
{
    public class VehicleCameraTest : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Takip edilecek araç. Boþ býrakýlýrsa sahnede 'Player' etiketli aktif araç otomatik atanýr.")]
        public Transform target;

        [Tooltip("Sahnede Player etiketli araç otomatik aransýn mý?")]
        public bool autoFindPlayerTag = true;

        [Header("Position Settings")]
        [Range(0f, 100f)]
        [Tooltip("Araca olan arkaya doðru mesafe (Mouse tekerleði ile dinamik deðiþir)")]
        public float distance = 6.0f;

        [Range(0f, 100f)]
        [Tooltip("Aracýn ne kadar yukarýsýnda duracaðý")]
        public float height = 2.0f;

        [Header("Smoothness")]
        [Range(0f, 5f)]
        [Tooltip("Kameranýn pozisyon yumuþatmasý (Düþük deðer = Daha hýzlý takip)")]
        public float positionSmoothTime = 0.15f;

        [Range(0f, 10f)]
        [Tooltip("Kameranýn rotasyon yumuþatmasý")]
        public float rotationSmoothSpeed = 5.0f;

        [Header("Auto Return / Dynamic Follow")]
        [Tooltip("Fare býrakýldýðýnda kameranýn otomatik olarak aracýn arkasýna dönmesi")]
        public bool autoReturnToRear = true;

        [Range(0.5f, 15f)]
        [Tooltip("Fare býrakýldýðýnda kameranýn aracýn arkasýna dönme yumuþaklýk hýzý")]
        public float autoReturnSpeed = 3.0f;

        [Header("Mouse Orbit & Zoom Settings")]
        [Tooltip("Fare ile etrafta dönerken hassasiyet")]
        public float mouseSensitivity = 3.0f;

        [Tooltip("Mouse tekerleði ile yakýnlaþma/uzaklaþma (Zoom) hýzý")]
        public float zoomSensitivity = 5.0f;

        [Tooltip("Zoom yaparken kameranýn yaklaþabileceði en minimum mesafe")]
        public float minDistanceLimit = 2.0f;

        [Tooltip("Zoom yaparken kameranýn uzaklaþabileceði en maksimum mesafe")]
        public float maxDistanceLimit = 80f;

        // Gizli Deðiþkenler
        private Vector3 currentVelocity;
        private float mouseX = 0f;
        private float mouseY = 15f;
        private Camera cam;
        private float baseFOV;
        private Rigidbody targetRigidbody;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null) baseFOV = cam.fieldOfView;

            // Eðer target elle atanmadýysa Player etiketli objeyi bul
            if (target == null && autoFindPlayerTag)
            {
                FindPlayerTarget();
            }
            else if (target != null)
            {
                SetupTargetReferences();
            }
        }

        /// <summary>
        /// Sahnede "Player" etiketine sahip aktif olan aracý bulur ve target olarak atar.
        /// </summary>
        public void FindPlayerTarget()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
            {
                target = playerObj.transform;
                SetupTargetReferences();
                Debug.Log($"[VehicleCameraTest] Kamera baþarýyla 'Player' etiketli araca kilitlendi: {playerObj.name}");
            }
            else
            {
                Debug.LogWarning("[VehicleCameraTest] Sahnede 'Player' etiketine sahip aktif bir araç bulunamadý!");
            }
        }

        private void SetupTargetReferences()
        {
            if (target == null) return;
            targetRigidbody = target.GetComponent<Rigidbody>();
            if (targetRigidbody == null) targetRigidbody = target.GetComponentInParent<Rigidbody>();
            ResetCameraInPosition();
        }

        void LateUpdate()
        {
            // Eðer hedef yoksa sürekli arama yap (Araç sonradan spawn olursa veya deðiþirse tutmasý için)
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (autoFindPlayerTag) FindPlayerTarget();
                if (target == null) return;
            }

            if (targetRigidbody == null)
            {
                targetRigidbody = target.GetComponent<Rigidbody>();
                if (targetRigidbody == null) targetRigidbody = target.GetComponentInParent<Rigidbody>();
            }

            // Aracýn hýzýný Rigidbody üzerinden al
            float currentSpeed = 0f;
            if (targetRigidbody != null)
            {
#if UNITY_6000_0_OR_NEWER
                currentSpeed = targetRigidbody.linearVelocity.magnitude;
#else
                currentSpeed = targetRigidbody.velocity.magnitude;
#endif
            }

            // Girdileri ve otomatik hizalamayý iþle
            HandleMouseInput();

            // Kamerayý konumlandýr ve döndür
            ExecuteDynamicFollowMode(currentSpeed);

            // Dinamik FOV Efekti
            if (cam != null)
            {
                float targetFOV = baseFOV + Mathf.Clamp(currentSpeed * 0.3f, 0f, 20f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 2f);
            }
        }

        private void ExecuteDynamicFollowMode(float speed)
        {
            float dynamicDistance = distance + Mathf.Clamp(speed * 0.05f, 0f, 4f);

            // Kameranýn rotasyonu sadece faremizin/otomatik takibin belirlediði açýlardan oluþur
            Quaternion cameraRotation = Quaternion.Euler(mouseY, mouseX, 0f);

            // Hedef Pozisyon: Aracýn taklalarýndan baðýmsýz, pozisyon merkezini alýr
            Vector3 targetPosition = target.position - (cameraRotation * Vector3.forward * dynamicDistance);
            targetPosition += Vector3.up * height;

            // Pozisyon Yumuþatma
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, positionSmoothTime);

            // Bakýþ Yönü: Araç Spin veya Roll atsa bile kamera her zaman aracýn merkez pozisyonuna bakar
            Vector3 lookAtPoint = target.position + (Vector3.up * (height * 0.3f));
            Vector3 lookDirection = lookAtPoint - transform.position;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetLookRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetLookRotation, Time.deltaTime * rotationSmoothSpeed);
            }
        }

        private void HandleMouseInput()
        {
            // 1. FARE ÝLE SERBEST BAKIÞ
            if (Input.GetMouseButton(1) || Input.GetMouseButton(0))
            {
                mouseX += Input.GetAxis("Mouse X") * mouseSensitivity;
                mouseY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                mouseY = Mathf.Clamp(mouseY, -10f, 85f);
            }
            // 2. OTOMATÝK HÝZALANMA
            else if (autoReturnToRear)
            {
                float targetYaw = target.eulerAngles.y;

                if (targetRigidbody != null)
                {
#if UNITY_6000_0_OR_NEWER
                    Vector3 flatVelocity = new(targetRigidbody.linearVelocity.x, 0f, targetRigidbody.linearVelocity.z);
#else
                    Vector3 flatVelocity = new Vector3(targetRigidbody.velocity.x, 0f, targetRigidbody.velocity.z);
#endif
                    if (flatVelocity.sqrMagnitude > 4f)
                    {
                        targetYaw = Quaternion.LookRotation(flatVelocity).eulerAngles.y;
                    }
                }

                mouseX = Mathf.LerpAngle(mouseX, targetYaw, Time.deltaTime * autoReturnSpeed);
                mouseY = Mathf.Lerp(mouseY, 15f, Time.deltaTime * autoReturnSpeed);
            }

            // 3. ZOOM
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                distance -= scrollInput * zoomSensitivity * 10f;
                distance = Mathf.Clamp(distance, minDistanceLimit, maxDistanceLimit);
            }
        }

        public void ResetCameraInPosition()
        {
            if (target == null) return;
            mouseX = target.eulerAngles.y;
            mouseY = 15f;

            Quaternion cameraRotation = Quaternion.Euler(mouseY, mouseX, 0f);
            transform.position = target.position - (cameraRotation * Vector3.forward * distance) + (Vector3.up * height);
            transform.LookAt(target.position + (Vector3.up * (height * 0.3f)), Vector3.up);
        }
    }
}