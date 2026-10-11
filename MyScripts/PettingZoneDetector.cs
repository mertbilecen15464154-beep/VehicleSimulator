using UnityEngine;

public class PettingZoneDetector : MonoBehaviour
{
    private HorseControllerVR mainHorseController;

    void Start()
    {
        mainHorseController = GetComponentInParent<HorseControllerVR>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (mainHorseController != null)
        {
            mainHorseController.ProcessPettingInteraction(other);
        }
    }
}