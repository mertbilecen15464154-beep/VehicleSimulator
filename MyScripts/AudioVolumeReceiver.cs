using UnityEngine;
using System.Collections.Generic;

public class AudioVolumeReceiver : MonoBehaviour
{
    private AudioSource targetSource;

    [Header("Bileþen Atamalarý")]
    [Tooltip("Bu araca ait tüm camlarý (WindowSystem) buraya sürükleyin.")]
    public List<WindowSystem> carWindows = new();

    [Tooltip("Bu araca ait tüm kapý Joint'lerini (ConfigurableJoint) buraya sürükleyin.")]
    public List<ConfigurableJoint> carDoorJoints = new();

    [Header("Ses Menzil Ayarlarý (Max Distance)")]
    [Tooltip("Camlar ve kapýlar kapalýyken sesin duyulabileceði en uzak mesafe.")]
    public float closedMaxDistance = 2f;

    [Tooltip("Cam veya kapý açýldýðýnda sesin duyulabileceði en uzak mesafe.")]
    public float openedMaxDistance = 10f;

    [Header("Kapý Hassasiyeti")]
    [Tooltip("Kapý kaç derece açýlýnca ses dýþarý taþsýn?")]
    public float doorOpenAngleThreshold = 2f;

    private void Start()
    {
        targetSource = GetComponent<AudioSource>();

        // Görseldeki gibi Custom Rolloff kullanýldýðýndan emin olalým
        if (targetSource)
        {
            targetSource.rolloffMode = AudioRolloffMode.Custom;
        }
    }

    private void Update()
    {
        if (targetSource == null) return;

        bool isAnythingOpen = false;

        // 1. Cam Kontrolü (%10 barajý)
        foreach (var window in carWindows)
        {
            if (window != null && window.GetOpenProgress() > 0.10f)
            {
                isAnythingOpen = true;
                break;
            }
        }

        // 2. Kapý Kontrolü (Angular Y sapmasý)
        if (!isAnythingOpen)
        {
            foreach (var joint in carDoorJoints)
            {
                if (joint != null)
                {
                    float currentAngle = Mathf.Abs(joint.transform.localEulerAngles.y);
                    if (currentAngle > 180) currentAngle = 360 - currentAngle;

                    if (currentAngle > doorOpenAngleThreshold)
                    {
                        isAnythingOpen = true;
                        break;
                    }
                }
            }
        }

        // 3. Sadece Max Distance Güncelleme
        // Curve (eðri) þekliniz sabit kalýr, sadece eðrinin bittiði uç nokta (Max Distance) kayar.
        float targetMax = isAnythingOpen ? openedMaxDistance : closedMaxDistance;
        targetSource.maxDistance = Mathf.Lerp(targetSource.maxDistance, targetMax, Time.deltaTime * 5f);
    }

    public void UpdateVolume(float value)
    {
        if (targetSource) targetSource.volume = value / 100f;
    }
}