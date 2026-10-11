using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Takip edilecek araç. Boþ býrakýlýrsa sahnede 'Player' etiketli aktif araç otomatik atanýr.")]
    public Transform playerTransform;

    [Tooltip("Sahnede Player etiketli araç otomatik aransýn mý?")]
    public bool autoFindPlayerTag = true;

    [Header("Settings")]
    public float cameraHeight = 60f; // Kameranýn yükseklik mesafesi
    public bool rotateWithPlayer = true; // Harita gidiþ yönüne göre dönsün mü?
    public float rotationSmoothSpeed = 5f; // Harita dönüþ pürüzsüzlüðü

    private void Start()
    {
        if (playerTransform == null && autoFindPlayerTag)
        {
            FindPlayerTarget();
        }
    }

    /// <summary>
    /// Sahnede "Player" etiketine sahip aktif aracý bulur ve playerTransform olarak atar.
    /// </summary>
    public void FindPlayerTarget()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            Debug.Log($"[MinimapFollow] Minimap kamerasý 'Player' etiketli araca kilitlendi: {playerObj.name}");
        }
        else
        {
            Debug.LogWarning("[MinimapFollow] Sahnede 'Player' etiketine sahip aktif bir araç bulunamadý!");
        }
    }

    private void LateUpdate()
    {
        // Hedef yoksa veya sahneden silindiyse otomatik tekrar arat
        if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
        {
            if (autoFindPlayerTag) FindPlayerTarget();
            if (playerTransform == null) return;
        }

        // 1. Pozisyon Takibi (Kamera her zaman arabanýn üstünde sabit kalýr)
        Vector3 newPos = playerTransform.position;
        newPos.y = playerTransform.position.y + cameraHeight;
        transform.position = newPos;

        // 2. Rotasyon Takibi (Roll ve Taklalardan ETKÝLENMEYEN yön hesabý)
        if (rotateWithPlayer)
        {
            // Aracýn bakýþ yönünü alýyoruz ama Y (yukarý) bileþenini tamamen sýfýrlýyoruz.
            Vector3 flatForward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up);

            if (flatForward.sqrMagnitude > 0.001f)
            {
                // Yere dik bakan kamera için hedef rotasyon
                Quaternion targetRotation = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

                // Ani sýçramalarý önlemek için pürüzsüz geçiþ (Slerp)
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
            }
        }
        else
        {
            // Sabit Kuzey Yönü
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}