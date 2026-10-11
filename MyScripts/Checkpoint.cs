using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Tooltip("Pistteki sýrasý: 0, 1, 2, 3... (Bitiþ çizgisi son checkpoint olmalý)")]
    public int index;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.attachedRigidbody?.CompareTag("Player") == true)
        {
            LapTimerManager.Instance.OnCheckpointPassed(index);
        }
    }
}