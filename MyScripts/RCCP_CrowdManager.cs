using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sahnedeki seyircilerin animasyonlarýný ve tezahürat seslerini yönetir.
/// </summary>
public class RCCP_CrowdManager : MonoBehaviour
{
    public static RCCP_CrowdManager Instance { get; private set; }

    [Header("Seyirci Objeleri")]
    [Tooltip("Sahnendeki tüm seyirci objelerinin baðlý olduðu parent (Örn: SpectatorsParent). Boþ býrakýrsan otomatik tarar.")]
    public Transform spectatorsParent;

    [Header("Animasyon Ayarlarý")]
    [Tooltip("Animator içindeki Cheer trigger parametre isimleri (Örn: Cheer1, Cheer2, Cheer3).")]
    public string[] cheerTriggerNames = new string[] { "Cheer1", "Cheer2", "Cheer3" };

    [Header("Ses Ayarlarý")]
    public AudioSource audioSource;
    [Tooltip("Çalýnacak rastgele seyirci sesleri (Cheer clips).")]
    public AudioClip[] cheerAudioClips;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Header("Soðuma (Cooldown)")]
    [Tooltip("Peþ peþe ayný sesin/animasyonun sürekli tetiklenmesini önler (saniye).")]
    public float triggerCooldown = 3f;

    private readonly List<Animator> spectatorAnimators = new();
    private float lastTriggerTime = -999f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // AudioSource bileþenini kontrol et/ekle
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        FindAllSpectatorAnimators();
    }

    /// <summary>
    /// Parent altýndaki tüm seyirci Animator'larýný bulup listeye kaydeder.
    /// </summary>

    public void FindAllSpectatorAnimators()
    {
        spectatorAnimators.Clear();

        if (spectatorsParent != null)
        {
            Animator[] anims = spectatorsParent.GetComponentsInChildren<Animator>();
            spectatorAnimators.AddRange(anims);
        }
        else
        {
            // Eðer parent verilmediyse sahnedeki tüm Animator'larý bulur
            Animator[] allAnims = FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (var anim in allAnims)
            {
                if (anim.gameObject.CompareTag("Spectator"))
                {
                    spectatorAnimators.Add(anim);
                }
            }
        }
    }

    /// <summary>
    /// Rampa tetiklendiðinde seyircileri coþturur.
    /// </summary>
    public void TriggerCrowdCheer()
    {
        // Cooldown kontrolü (Üst üste tetiklenmeyi engeller)
        if (Time.time < lastTriggerTime + triggerCooldown) return;
        lastTriggerTime = Time.time;

        // 1. ANÝMASYONLARI RASTGELE TETÝKLE
        if (spectatorAnimators.Count > 0 && cheerTriggerNames.Length > 0)
        {
            foreach (var anim in spectatorAnimators)
            {
                if (anim != null && anim.gameObject.activeInHierarchy)
                {
                    // Her seyirci rastgele farklý bir cheer animasyonu yapsýn
                    string randomTrigger = cheerTriggerNames[Random.Range(0, cheerTriggerNames.Length)];
                    anim.SetTrigger(randomTrigger);
                }
            }
        }

        // 2. RASTGELE CHEER SESÝ ÇAL
        if (cheerAudioClips != null && cheerAudioClips.Length > 0 && audioSource != null)
        {
            AudioClip randomClip = cheerAudioClips[Random.Range(0, cheerAudioClips.Length)];
            audioSource.PlayOneShot(randomClip, soundVolume);
        }
    }
}