using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MenuLapSelector : MonoBehaviour
{
    public TextMeshProUGUI lapCountText;
    public int selectedLaps = 3;

    private void Start()
    {
        // Kaydedilmiþ tur varsa getir
        selectedLaps = PlayerPrefs.GetInt("SelectedLaps", 3);
        UpdateUI();
    }

    public void IncreaseLaps()
    {
        if (selectedLaps < 5)
        {
            selectedLaps++;
            SaveAndUpdate();
        }
    }

    public void DecreaseLaps()
    {
        if (selectedLaps > 1)
        {
            selectedLaps--;
            SaveAndUpdate();
        }
    }

    private void SaveAndUpdate()
    {
        PlayerPrefs.SetInt("SelectedLaps", selectedLaps);
        PlayerPrefs.Save();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (lapCountText != null)
        {
            lapCountText.text = selectedLaps.ToString();
        }
    }
}