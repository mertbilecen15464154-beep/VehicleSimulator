using UnityEngine;

public class Waypoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        // 1. Noktayý turkuaz küre olarak göster
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.6f);

        // 2. Bir sonraki Waypoint'e sarý çizgi çek
        int currentIndex = transform.GetSiblingIndex();
        Transform parent = transform.parent;

        if (parent != null && currentIndex < parent.childCount - 1)
        {
            Transform nextWaypoint = parent.GetChild(currentIndex + 1);
            if (nextWaypoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, nextWaypoint.position);
            }
        }
        else
        {
            // Son Waypoint ise sadece baktýðý yönü göster
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * 3f);
        }
    }
}