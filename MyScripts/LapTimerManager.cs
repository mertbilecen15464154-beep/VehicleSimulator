using UnityEngine;
using TMPro;

public class LapTimerManager : MonoBehaviour
{
    public static LapTimerManager Instance;

    [Header("UI Metinleri")]
    public TextMeshProUGUI lapText;           // Örn: TUR 1 / 3
    public TextMeshProUGUI currentLapTimeText;// Örn: Tur Süresi: 00:45.23
    public TextMeshProUGUI bestLapTimeText;   // Örn: En Ýyi Tur: 00:42.10
    public TextMeshProUGUI totalTimeText;     // Örn: Toplam Süre: 01:30.15

    [Header("Yarýþ Durumu")]
    public int totalLaps = 3;
    public int currentLap = 1;
    public int totalCheckpoints = 5;
    private int nextCheckpointIndex = 0;

    private float currentLapTimer = 0f;
    private float bestLapTimer = Mathf.Infinity;
    private float totalTimer = 0f;
    private bool isRaceActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Menüden gelen tur sayýsýný al (varsayýlan 3)
        totalLaps = PlayerPrefs.GetInt("SelectedLaps", 3);
        totalLaps = Mathf.Clamp(totalLaps, 1, 5);

        currentLap = 1;
        nextCheckpointIndex = 0;
        currentLapTimer = 0f;
        totalTimer = 0f;
        bestLapTimer = Mathf.Infinity;

        isRaceActive = true;
        UpdateLapUI();
        UpdateBestLapUI();
    }

    private void Update()
    {
        if (!isRaceActive) return;

        // Zamanlarý güncelle
        currentLapTimer += Time.deltaTime;
        totalTimer += Time.deltaTime;

        // UI Güncellemeleri
        if (currentLapTimeText != null)
            currentLapTimeText.text = "Current Lap: " + FormatTime(currentLapTimer);

        if (totalTimeText != null)
            totalTimeText.text = FormatTime(totalTimer);
    }

    // Checkpoint geçiþlerinde çaðrýlacak fonksiyon
    public void OnCheckpointPassed(int checkpointIndex)
    {
        if (!isRaceActive) return;

        if (checkpointIndex == nextCheckpointIndex)
        {
            nextCheckpointIndex++;

            // Tüm checkpointler geçildiyse tur tamamlanmýþtýr
            if (nextCheckpointIndex >= totalCheckpoints)
            {
                CompleteLap();
            }
        }
    }

    private void CompleteLap()
    {
        // En iyi tur kontrolü
        if (currentLapTimer < bestLapTimer)
        {
            bestLapTimer = currentLapTimer;
            UpdateBestLapUI();
        }

        if (currentLap < totalLaps)
        {
            currentLap++;
            nextCheckpointIndex = 0; // Checkpointleri sýfýrla
            currentLapTimer = 0f;    // Yeni tur için sayacý sýfýrla
            UpdateLapUI();
        }
        else
        {
            FinishRace();
        }
    }

    private void FinishRace()
    {
        isRaceActive = false;
        Debug.Log("Yarýþ Bitti!");
        // Yarýþ bitiþ panelini açma kodlarýný buraya ekleyebilirsin
    }

    private void UpdateLapUI()
    {
        if (lapText != null)
            lapText.text = $"LAP: {currentLap} / {totalLaps}";
    }

    private void UpdateBestLapUI()
    {
        if (bestLapTimeText != null)
        {
            if (bestLapTimer == Mathf.Infinity)
                bestLapTimeText.text = "Best Lap: --:--.--";
            else
                bestLapTimeText.text = "Best Lap: " + FormatTime(bestLapTimer);
        }
    }

    // Saniyeyi 00:00.00 formatýna çeviren yardýmcý metot
    private string FormatTime(float timeInSeconds)
    {
        System.TimeSpan t = System.TimeSpan.FromSeconds(timeInSeconds);
        return string.Format("{0:D2}:{1:D2}.{2:D2}", t.Minutes, t.Seconds, t.Milliseconds / 10);
    }
}