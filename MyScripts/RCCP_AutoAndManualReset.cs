using UnityEngine;
using System.Collections.Generic;

public class RCCP_AutoAndManualReset : MonoBehaviour
{
    [Header("Tuþ Ayarlarý")]
    public KeyCode resetKey = KeyCode.R;

    [Header("Otomatik Sýfýrlama Ayarlarý")]
    public float autoResetDelay = 3f;
    public float flipAngleThreshold = 60f;

    [Header("Waypoint Ayarlarý")]
    [Tooltip("Boþ býrakýrsan sahnede aktif olan 'Waypoints' isimli objeyi otomatik bulur.")]
    public Transform waypointsParent;

    [Tooltip("Araba sýfýrlandýðýnda Waypoint'in ne kadar üstüne konsun?")]
    public float spawnHeightOffset = 0.5f;

    private readonly List<Transform> waypoints = new();
    private float flipTimer = 0f;

    private void Start()
    {
        FindAndAssignWaypoints();
    }

    /// <summary>
    /// Sahnede aktif pist altýndaki "Waypoints" objesini bulur ve altýndaki tüm çocuklarý listeye ekler.
    /// </summary>
    public void FindAndAssignWaypoints()
    {
        waypoints.Clear();

        // Eðer Inspector'dan elle atama yapýlmadýysa sahnede "Waypoints" ismindeki aktif objeyi ara
        if (waypointsParent == null)
        {
            GameObject foundParent = GameObject.Find("Waypoints");
            if (foundParent != null && foundParent.activeInHierarchy)
            {
                waypointsParent = foundParent.transform;
            }
            else
            {
                // Alternatif: Pistlerin altýndaki Waypoints objelerini tarar
                Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.InstanceID);
                if (allWaypoints.Length > 0)
                {
                    waypointsParent = allWaypoints[0].transform.parent;
                }
            }
        }

        // Parent altýndaki aktif waypoint'leri sýrasýyla listeye al
        if (waypointsParent != null)
        {
            foreach (Transform child in waypointsParent)
            {
                if (child.gameObject.activeSelf)
                {
                    waypoints.Add(child);
                }
            }
        }
        else
        {
            Debug.LogWarning("[RCCP Reset] Sahnede aktif bir 'Waypoints' objesi bulunamadý!");
        }
    }

    private void Update()
    {
        if (RCCP_SceneManager.Instance == null) return;

        var vehicle = RCCP_SceneManager.Instance.activePlayerVehicle;
        if (vehicle == null || !vehicle.canControl) return;

        // Dinamik pist deðiþimlerinde Waypoint listesi boþsa yeniden aramaya çalýþýr
        if (waypoints.Count == 0)
        {
            FindAndAssignWaypoints();
        }

        // 1. MANUEL RESET (R Tuþu)
        if (Input.GetKeyDown(resetKey))
        {
            ResetToNearestWaypoint(vehicle);
            flipTimer = 0f;
            return;
        }

        // 2. OTOMATÝK RESET KONTROLÜ
        if (IsOnLoopRamp(vehicle))
        {
            flipTimer = 0f;
            return;
        }

        float angleWithUp = Vector3.Angle(vehicle.transform.up, Vector3.up);

        if (angleWithUp > flipAngleThreshold)
        {
            flipTimer += Time.deltaTime;

            if (flipTimer >= autoResetDelay)
            {
                ResetToNearestWaypoint(vehicle);
                flipTimer = 0f;
            }
        }
        else
        {
            flipTimer = 0f;
        }
    }

    private bool IsOnLoopRamp(RCCP_CarController vehicle)
    {
        if (Physics.Raycast(vehicle.transform.position, -vehicle.transform.up, out RaycastHit hit, 3f))
        {
            if (hit.collider.CompareTag("LoopRamp"))
            {
                return true;
            }
        }
        return false;
    }

    private void ResetToNearestWaypoint(RCCP_CarController vehicle)
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogWarning("Hiç Waypoint bulunamadý!");
            return;
        }

        int nearestIndex = -1;
        float minDistance = float.MaxValue;
        Vector3 vehiclePos = vehicle.transform.position;

        for (int i = 0; i < waypoints.Count; i++)
        {
            float dist = Vector3.Distance(vehiclePos, waypoints[i].position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestIndex = i;
            }
        }

        if (nearestIndex != -1)
        {
            Transform nearestWaypoint = waypoints[nearestIndex];

            // 1. DOÐRU YÖN HESAPLAMA
            Vector3 lookDirection;
            if (nearestIndex < waypoints.Count - 1)
            {
                lookDirection = (waypoints[nearestIndex + 1].position - nearestWaypoint.position).normalized;
            }
            else
            {
                lookDirection = nearestWaypoint.forward;
            }

            lookDirection.y = 0f;
            Quaternion targetRot = lookDirection != Vector3.zero ? Quaternion.LookRotation(lookDirection) : nearestWaypoint.rotation;

            // 2. KONUM HESAPLAMA
            Vector3 targetPos = nearestWaypoint.position + Vector3.up * spawnHeightOffset;

            // 3. IÞINLAMA
            RCCP_SceneManager.Instance.Transport(vehicle, targetPos, targetRot);

            // 4. FÝZÝKSEL HIZLARI SIFIRLAMA
            if (vehicle.Rigid != null)
            {
#if UNITY_6000_0_OR_NEWER
                vehicle.Rigid.linearVelocity = Vector3.zero;
#else
                vehicle.Rigid.velocity = Vector3.zero;
#endif
                vehicle.Rigid.angularVelocity = Vector3.zero;
            }

            // 5. RCCP ÝÇ HIZ SIFIRLAMA
            if (vehicle.CarController != null)
            {
                vehicle.CarController.speed = 0f;
            }
        }
    }
}