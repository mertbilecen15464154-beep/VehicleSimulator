using UnityEngine;
using BNG;

public class WindowSystem : MonoBehaviour
{
    [System.Serializable]
    public class WindowOffset
    {
        [Tooltip("Eðer boþ býrakýlýrsa scriptin baðlý olduðu obje otomatik atanýr.")]
        public Transform windowObject;

        [HideInInspector] public Vector3 closedPosition;
        [HideInInspector] public Vector3 closedRotation;

        [Header("Hareket Miktarý (Offset)")]
        public Vector3 openPositionOffset = new(); // Örn: Aþaðý doðru 40cm
        public Vector3 openRotationOffset = Vector3.zero;
    }

    public WindowOffset window;

    [Header("Ayarlar")]
    public float moveSpeed = 0.5f;
    public AudioClip windowMovementSound;

    private AudioSource audioSource;
    private float currentProgress = 0f;
    private bool isDownActive = false;
    private bool isUpActive = false;

    public float GetOpenProgress() => currentProgress;

    void Awake()
    {
        // 1. Pencere objesi atanmamýþsa scriptin baðlý olduðu objeyi kullan
        if (window.windowObject == null)
        {
            window.windowObject = this.transform;
        }

        // 2. AudioSource bileþenini otomatik bul veya yoksa ekle
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 3. AudioSource ayarlarýný yap
        if (audioSource != null)
        {
            audioSource.clip = windowMovementSound;
            audioSource.loop = true;
            audioSource.playOnAwake = false;
        }
    }

    void Start()
    {
        // Ýlk pozisyon ve rotasyonu "Kapalý" durum olarak kaydet
        if (window.windowObject != null)
        {
            window.closedPosition = window.windowObject.localPosition;
            window.closedRotation = window.windowObject.localEulerAngles;
        }
    }

    void Update()
    {
        float direction = 0;

        if (isDownActive) direction = 1f; // Aþaðý/Açýlma
        else if (isUpActive) direction = -1f; // Yukarý/Kapanma

        if (direction != 0)
        {
            float lastProgress = currentProgress;
            currentProgress += direction * moveSpeed * Time.deltaTime;
            currentProgress = Mathf.Clamp01(currentProgress);

            if (currentProgress != lastProgress)
            {
                ApplyWindowMovement();
                HandleAudio(true);
            }
            else
            {
                HandleAudio(false);
            }
        }
        else
        {
            HandleAudio(false);
        }
    }

    // BNG Button / Lever Eventleri için
    public void OnDownButtonPush() => isDownActive = true;
    public void OnDownButtonRelease() => isDownActive = false;

    public void OnUpButtonPush() => isUpActive = true;
    public void OnUpButtonRelease() => isUpActive = false;

    void HandleAudio(bool shouldPlay)
    {
        if (audioSource == null || audioSource.clip == null) return;

        if (shouldPlay && !audioSource.isPlaying)
            audioSource.Play();
        else if (!shouldPlay && audioSource.isPlaying)
            audioSource.Stop();
    }

    void ApplyWindowMovement()
    {
        if (window.windowObject == null) return;

        // Pozisyonu baþlangýç deðerine göre ofsetle
        window.windowObject.localPosition = window.closedPosition + (window.openPositionOffset * currentProgress);

        // Rotasyonu baþlangýç deðerine göre ofsetle
        window.windowObject.localEulerAngles = window.closedRotation + (window.openRotationOffset * currentProgress);
    }
}