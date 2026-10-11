using UnityEngine;
using TMPro;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    [Header("Lap Settings")]
    public int totalLaps = 3; // Menüden ayarlanacak (Min 1, Max 5)
    public int currentLap = 1;

    [Header("Checkpoint System")]
    public int totalCheckpoints;
    private int currentCheckpointIndex = 0;

    [Header("UI References")]
    public TextMeshProUGUI lapText;
    public GameObject raceFinishedPanel;
    public TextMeshProUGUI finalTimeText;

    private float raceTimer = 0f;
    private bool isRaceFinished = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // PlayerPrefs ile menüden seçilen tur sayýsýný al (varsayýlan 3)
        totalLaps = PlayerPrefs.GetInt("SelectedLaps", 3);
        totalLaps = Mathf.Clamp(totalLaps, 1, 5); // 1-5 arasýnda sýnýrla

        currentLap = 1;
        currentCheckpointIndex = 0;
        isRaceFinished = false;

        UpdateLapUI();
        if (raceFinishedPanel != null) raceFinishedPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isRaceFinished)
        {
            raceTimer += Time.deltaTime;
        }
    }

    public void OnCheckpointPassed(int checkpointIndex)
    {
        if (isRaceFinished) return;

        // Sýradaki doðru checkpoint mi geçildi?
        if (checkpointIndex == currentCheckpointIndex)
        {
            currentCheckpointIndex++;

            // Tüm checkpointler tamamlandý, tur bitti (Finish Çizgisi)
            if (currentCheckpointIndex >= totalCheckpoints)
            {
                OnLapCompleted();
            }
        }
    }

    private void OnLapCompleted()
    {
        if (currentLap < totalLaps)
        {
            currentLap++;
            currentCheckpointIndex = 0; // Checkpointleri sýfýrla
            UpdateLapUI();
        }
        else
        {
            FinishRace();
        }
    }

    private void FinishRace()
    {
        isRaceFinished = true;
        UpdateLapUI();

        if (raceFinishedPanel != null)
        {
            raceFinishedPanel.SetActive(true);
        }

        System.TimeSpan timeSpan = System.TimeSpan.FromSeconds(raceTimer);
        if (finalTimeText != null)
        {
            finalTimeText.text = string.Format("Süre: {0:D2}:{1:D2}.{2:D3}", timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
        }

        Debug.Log("Yarýþ Bitti!");
    }

    private void UpdateLapUI()
    {
        if (lapText != null)
        {
            lapText.text = $"TUR: {currentLap} / {totalLaps}";
        }
    }
}