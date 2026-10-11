using UnityEngine;
using BNG;

public class RCCP_FuelCap : MonoBehaviour
{
    [Header("Components (Auto Assigned)")]
    private Transform fuelCapObject;
    private GrabbableUnityEvents pushTrigger;

    // Pompa sistemi dýþarýdan referans almalý (Çünkü tabanca ayrý bir obje)
    public FuelPumpSystem pumpSystem;

    [Header("Rotation Settings")]
    public Vector3 openRotation = new();
    private Quaternion startRotation;
    private Quaternion targetRotation;

    [Header("Animation Settings")]
    public float transitionSpeed = 5f;
    public bool isOpen = false;

    void Start()
    {
        // 1. fuelCapObject atamasý: Script bu objede olduðu için direkt transform'u alýyoruz
        fuelCapObject = transform;

        // 2. pushTrigger atamasý: Ayný objedeki bileþeni alýyoruz
        pushTrigger = GetComponent<GrabbableUnityEvents>();

        if (pushTrigger != null)
        {
            pushTrigger.onGrab.AddListener(OnPushCap);
        }
        else
        {
            Debug.LogError($"RCCP_FuelCap: {gameObject.name} üzerinde GrabbableUnityEvents bulunamadý!");
        }

        // Baþlangýç deðerlerini kaydet
        startRotation = fuelCapObject.localRotation;
        targetRotation = startRotation;
    }

    void Update()
    {
        // Kapaðý hedef rotasyona yumuþakça döndür
        fuelCapObject.localRotation = Quaternion.Slerp(
            fuelCapObject.localRotation,
            targetRotation,
            Time.deltaTime * transitionSpeed
        );
    }

    public void OnPushCap(Grabber grabber)
    {
        // --- POMPA KONTROLÜ ---
        // Eðer pompa takýlýysa ve kapak açýksa, kapaðýn kapanmasýný engelle
        if (pumpSystem != null && pumpSystem.isLocked && isOpen)
        {
            if (grabber != null) grabber.TryRelease(); // Elini kapaðýn üzerinden çekmesini saðla
            return;
        }

        // Kapaða dokunulduðu anda eli serbest býrak (Push mantýðý: dokun-býrak)
        if (grabber != null) grabber.TryRelease();

        // Durumu deðiþtir
        isOpen = !isOpen;

        // Hedef rotasyonu belirle
        targetRotation = isOpen ? Quaternion.Euler(openRotation) : startRotation;
    }
}