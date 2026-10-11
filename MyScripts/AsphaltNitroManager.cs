using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro kütüphanesi
using BNG; // VR Desteði için

public enum CustomNitroState
{
    Off,
    Blue,
    OrangePerfect
}

public class CustomAsphaltNitro : MonoBehaviour
{
    [Header("Nitro Key Settings")]
    public KeyCode nitroKey = KeyCode.Q;

    [Header("Nitro Capacity & Drain Rates")]
    public float maxNitro = 100f;
    public float currentNitro = 0f;
    public float blueDrainRate = 5f;
    public float orangeDrainRate = 10f;

    [Header("Speed / Force Multipliers")]
    public float blueForce = 8000f;
    public float orangeForce = 14000f;

    [Header("Air Time Charging (Asphalt 8 Style)")]
    public float airTimeChargeRate = 10f;

    [Header("Drift & Risk Drift Settings")]
    [Tooltip("Drift atarken saniyede ne kadar nitro eklensin?")]
    public float driftNitroChargeRate = 15f;

    [Tooltip("Aracýn drift sayýlmasý için gidiþ yönü ile burnunun arasýndaki minimum açý (Derece)")]
    public float minDriftAngle = 15f;

    [Tooltip("Drift sayýlmasý için aracýn minimum hýzý (km/h)")]
    public float minDriftSpeed = 10f;

    [Tooltip("Engele kaç metreden fazla yakýnsak Risk Drift sayýlsýn?")]
    public float riskObstacleDistance = 2.0f;

    [Tooltip("Drift veya engel yakýnlýðý bittiðinde Risk Drift'in sýfýrlanmasý için beklenecek süre (Saniye)")]
    public float riskDriftGraceTime = 2.0f;

    [Tooltip("Risk Drift algýlanacak engellerin Layer'ý (Default, Obstacle vs.)")]
    public LayerMask obstacleLayer = ~0; // Varsayýlan olarak her þeyi algýlar

    [Header("Perfect Window Settings (Zamana Baðlý)")]
    [Tooltip("Ýlk nitroya basýldýktan kaç saniye sonra Orange Nitro penceresi gelsin?")]
    public float targetTimeSeconds = 2.0f;

    [Tooltip("Tolerans penceresi kaç saniye açýk kalsýn? (Örn: 0.4s ise 1.8s - 2.2s arasý basýlmalýdýr)")]
    public float windowTimeTolerance = 0.4f;

    [Header("UI References")]
    public Image nitroFillImage;
    public RectTransform perfectWindowUI; // NitroComboBar

    [Header("Risk Drift UI References")]
    [Tooltip("Ekranda 'Risk Drift' yazýsý ve süreyi gösterecek TextMeshPro UI nesnesi")]
    public TextMeshProUGUI riskDriftText;

    [Header("Particle Prefabs & Exhaust Points")]
    public Transform[] exhaustPoints;
    public GameObject blueNitroPrefab;
    public GameObject orangeNitroPrefab;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip blueNitroSound;
    public AudioClip orangeNitroSound;

    // RCCP Referansý
    private RCCP_CarController carController;

    // Privates
    private CustomNitroState currentState = CustomNitroState.Off;
    private Rigidbody carRigidbody;
    private InputBridge inputBridge;
    private bool isGrounded = true;

    // Zaman ve Perfect Nitro Takibi
    private float nitroStartTime = 0f;
    private bool canAttemptPerfect = false;

    // Risk Drift Takibi
    private float riskDriftTimer = 0f;
    private bool isRiskDrifting = false;
    private float currentGraceTimer = 0f; // 2 Saniyelik bekleme tolerans sayacý

    private GameObject[] activeBlueInstances;
    private GameObject[] activeOrangeInstances;

    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        carController = GetComponent<RCCP_CarController>();
        inputBridge = InputBridge.Instance;

        if (perfectWindowUI != null)
        {
            perfectWindowUI.gameObject.SetActive(false);
        }

        if (riskDriftText != null)
        {
            riskDriftText.gameObject.SetActive(false);
        }

        InstantiateNitroParticles();
    }

    void Update()
    {
        CheckGroundedState();

        // Havadaysa Nitro Doldur
        if (!isGrounded)
        {
            if (currentState != CustomNitroState.Off)
            {
                StopNitro();
            }
            AddNitro(airTimeChargeRate * Time.deltaTime);
        }

        // --- DRIFT & RISK DRIFT KONTROLÜ ---
        HandleDriftLogic();

        // Fren veya El Freni (S / Space) ile Kesme
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Space))
        {
            if (currentState != CustomNitroState.Off)
            {
                StopNitro();
            }
        }

        // Nitro Tuþu Kontrolü
        bool pcPressed = Input.GetKeyDown(nitroKey);
        bool vrPressed = inputBridge != null && inputBridge.XButtonDown;

        if (pcPressed || vrPressed)
        {
            if (isGrounded)
            {
                HandleNitroPress();
            }
        }

        // Nitro Tüketimi ve Süre Kontrolü
        if (currentState != CustomNitroState.Off)
        {
            float drainAmount = (currentState == CustomNitroState.Blue) ? blueDrainRate : orangeDrainRate;
            currentNitro -= drainAmount * Time.deltaTime;

            if (currentState == CustomNitroState.Blue && canAttemptPerfect)
            {
                float elapsedTime = Time.time - nitroStartTime;
                float maxAllowedTime = targetTimeSeconds + (windowTimeTolerance / 2f);

                if (elapsedTime > maxAllowedTime)
                {
                    canAttemptPerfect = false;
                    if (perfectWindowUI != null) perfectWindowUI.gameObject.SetActive(false);
                }
            }

            if (currentNitro <= 0f)
            {
                currentNitro = 0f;
                StopNitro();
            }
        }

        // UI Güncelleme
        if (nitroFillImage != null)
        {
            nitroFillImage.fillAmount = currentNitro / maxNitro;
        }
    }

    void FixedUpdate()
    {
        if (isGrounded)
        {
            if (currentState == CustomNitroState.Blue && carRigidbody != null)
            {
                carRigidbody.AddForce(transform.forward * blueForce, ForceMode.Force);
            }
            else if (currentState == CustomNitroState.OrangePerfect && carRigidbody != null)
            {
                carRigidbody.AddForce(transform.forward * orangeForce, ForceMode.Force);
            }
        }
    }

    private void HandleDriftLogic()
    {
        bool isDrifting = false;

        if (carController != null && carRigidbody != null)
        {
            float speedKmh = carRigidbody.linearVelocity.magnitude * 3.6f;

            if (speedKmh > minDriftSpeed)
            {
                float angle = Vector3.Angle(carRigidbody.linearVelocity.normalized, transform.forward);

                if (angle > minDriftAngle)
                {
                    isDrifting = true;
                }
            }
        }

        bool nearObstacle = CheckNearObstacles();

        // Aktif olarak hem drift atýyor hem engele yakýn mý?
        if (isDrifting && isGrounded && nearObstacle)
        {
            // Koþul saðlandýðý sürece tolerans sayacýný sürekli full tutuyoruz (2s)
            currentGraceTimer = riskDriftGraceTime;
            isRiskDrifting = true;

            // Drift Nitro Þarjý
            AddNitro(driftNitroChargeRate * Time.deltaTime);

            // Risk Drift Süresini Arttýr
            riskDriftTimer += Time.deltaTime;

            // UI Güncelle
            if (riskDriftText != null)
            {
                riskDriftText.gameObject.SetActive(true);
                riskDriftText.text = $"RISK DRIFT\n<size=80%>{riskDriftTimer:F1}s</size>";
            }
        }
        else
        {
            // Sadece normal drift yapýlýyorsa nitro dolsun
            if (isDrifting && isGrounded)
            {
                AddNitro(driftNitroChargeRate * Time.deltaTime);
            }

            // Eðer daha önce Risk Drift baþlamýþsa ama anlýk olarak koþul bozulduysa (Sað/Sol drift geçiþi veya engel mesafesi açýlmasý)
            if (isRiskDrifting)
            {
                currentGraceTimer -= Time.deltaTime;

                if (currentGraceTimer <= 0f)
                {
                    // 2 Saniyelik tolerans süresi tamamen bittiðinde sýfýrla ve kapat
                    ResetRiskDrift();
                }
            }
        }
    }

    private bool CheckNearObstacles()
    {
        const int maxColliders = 16;
        Collider[] hitColliders = new Collider[maxColliders];
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, riskObstacleDistance, hitColliders, obstacleLayer);

        for (int i = 0; i < hitCount; i++)
        {
            var col = hitColliders[i];
            if (col.gameObject != gameObject && !col.transform.IsChildOf(transform))
            {
                return true;
            }
        }
        return false;
    }

    private void ResetRiskDrift()
    {
        isRiskDrifting = false;
        riskDriftTimer = 0f;
        currentGraceTimer = 0f;

        if (riskDriftText != null)
        {
            riskDriftText.gameObject.SetActive(false);
        }
    }

    void HandleNitroPress()
    {
        if (currentNitro < (maxNitro * 0.1f) && currentState == CustomNitroState.Off) return;

        if (currentState == CustomNitroState.Off)
        {
            ActivateBlueNitro();
        }
        else if (currentState == CustomNitroState.Blue)
        {
            if (canAttemptPerfect)
            {
                float elapsedTime = Time.time - nitroStartTime;
                float minAllowedTime = targetTimeSeconds - (windowTimeTolerance / 2f);
                float maxAllowedTime = targetTimeSeconds + (windowTimeTolerance / 2f);

                if (elapsedTime >= minAllowedTime && elapsedTime <= maxAllowedTime)
                {
                    ActivateOrangeNitro();
                }
                else
                {
                    canAttemptPerfect = false;
                    if (perfectWindowUI != null) perfectWindowUI.gameObject.SetActive(false);
                }
            }
        }
        else if (currentState == CustomNitroState.OrangePerfect)
        {
            StopNitro();
        }
    }

    void ActivateBlueNitro()
    {
        currentState = CustomNitroState.Blue;
        nitroStartTime = Time.time;

        float startRatio = currentNitro / maxNitro;
        float drainRateRatio = blueDrainRate / maxNitro;

        float minTime = targetTimeSeconds - (windowTimeTolerance / 2f);
        float maxTime = targetTimeSeconds + (windowTimeTolerance / 2f);

        float windowEndRatio = Mathf.Clamp01(startRatio - (minTime * drainRateRatio));
        float windowStartRatio = Mathf.Clamp01(startRatio - (maxTime * drainRateRatio));

        if (perfectWindowUI != null)
        {
            perfectWindowUI.anchorMin = new Vector2(windowStartRatio, 0f);
            perfectWindowUI.anchorMax = new Vector2(windowEndRatio, 1f);

            perfectWindowUI.offsetMin = Vector2.zero;
            perfectWindowUI.offsetMax = Vector2.zero;

            perfectWindowUI.gameObject.SetActive(true);
        }

        canAttemptPerfect = true;

        SetInstancesState(activeBlueInstances, true);
        SetInstancesState(activeOrangeInstances, false);

        if (audioSource && blueNitroSound)
        {
            audioSource.clip = blueNitroSound;
            audioSource.Play();
        }
    }

    void ActivateOrangeNitro()
    {
        currentState = CustomNitroState.OrangePerfect;
        canAttemptPerfect = false;

        if (perfectWindowUI != null)
        {
            perfectWindowUI.gameObject.SetActive(false);
        }

        SetInstancesState(activeBlueInstances, false);
        SetInstancesState(activeOrangeInstances, true);

        if (audioSource && orangeNitroSound)
        {
            audioSource.clip = orangeNitroSound;
            audioSource.Play();
        }
    }

    public void StopNitro()
    {
        currentState = CustomNitroState.Off;
        canAttemptPerfect = false;

        if (perfectWindowUI != null)
        {
            perfectWindowUI.gameObject.SetActive(false);
        }

        SetInstancesState(activeBlueInstances, false);
        SetInstancesState(activeOrangeInstances, false);

        if (audioSource && audioSource.isPlaying) audioSource.Stop();
    }

    void InstantiateNitroParticles()
    {
        if (exhaustPoints == null || exhaustPoints.Length == 0) return;

        activeBlueInstances = new GameObject[exhaustPoints.Length];
        activeOrangeInstances = new GameObject[exhaustPoints.Length];

        for (int i = 0; i < exhaustPoints.Length; i++)
        {
            Transform point = exhaustPoints[i];

            if (blueNitroPrefab != null)
            {
                GameObject blueObj = Instantiate(blueNitroPrefab, point.position, point.rotation, point);
                blueObj.SetActive(false);
                activeBlueInstances[i] = blueObj;
            }

            if (orangeNitroPrefab != null)
            {
                GameObject orangeObj = Instantiate(orangeNitroPrefab, point.position, point.rotation, point);
                orangeObj.SetActive(false);
                activeOrangeInstances[i] = orangeObj;
            }
        }
    }

    void SetInstancesState(GameObject[] instances, bool active)
    {
        if (instances == null) return;
        foreach (var obj in instances)
        {
            if (obj != null)
            {
                obj.SetActive(active);
                if (active)
                {
                    ParticleSystem ps = obj.GetComponentInChildren<ParticleSystem>();
                    if (ps != null) ps.Play();
                }
            }
        }
    }

    public void AddNitro(float amount)
    {
        currentNitro = Mathf.Clamp(currentNitro + amount, 0f, maxNitro);
    }

    void CheckGroundedState()
    {
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 1.2f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, riskObstacleDistance);
    }
}