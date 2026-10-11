using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance;
    public GameObject interactionPanel;
    public Text infoText;

    private object currentOwner; // Yazýyý þu an hangi obje gösteriyor?

    void Awake() { Instance = this; if (interactionPanel != null) interactionPanel.SetActive(false);
    }

    public void ShowText(string message, object owner)
    {
        currentOwner = owner;
        if (infoText != null) infoText.text = message;
        if (interactionPanel != null) interactionPanel.SetActive(true);
    }

    public void HideText(object owner)
    {
        // Sadece yazýyý gösteren obje gizlemek isterse gizle
        if (currentOwner == owner && interactionPanel != null)
        {
            interactionPanel.SetActive(false);
            currentOwner = null;
        }
    }
}