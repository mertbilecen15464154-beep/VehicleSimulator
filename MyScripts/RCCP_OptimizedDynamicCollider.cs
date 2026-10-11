using UnityEngine;

/// <summary>
/// Kasma ve takýlma yapmayan performans odaklý dinamik MeshCollider yöneticisi.
/// Component'i oyun baþýnda 1 kere ekler, sadece enabled durumunu deðiþtirir.
/// </summary>
public class RCCP_OptimizedDynamicCollider : MonoBehaviour
{
    [Header("Gövde & Yol Ayarlarý")]
    [Tooltip("Aracýn 3D modelinin bulunduðu Mesh/Body objesi.")]
    public Transform bodyTransform;

    [Tooltip("MeshCollider'ýn aktif olacaðý yol katmaný (AutoRoad).")]
    public LayerMask roadLayer;

    [Header("Mesafe Ayarlarý")]
    [Tooltip("Aracýn yola ne kadar yakýnken MeshCollider'ýn açýlacaðýný belirler.")]
    public float detectionDistance = 3.0f;

    private MeshCollider cachedMeshCollider;
    private bool isColliderActive = false;

    private void Awake()
    {
        // Body objesini otomatik tespit et
        if (bodyTransform == null)
        {
            Transform foundBody = transform.Find("body");
            bodyTransform = foundBody != null ? foundBody : transform;
        }

        SetupMeshCollider();
    }

    /// <summary>
    /// Oyun baþlarken 1 defaya mahsus MeshCollider'ý hazýrlar ve KAPATIR.
    /// </summary>
    private void SetupMeshCollider()
    {
        if (bodyTransform == null) return;

        // Objede zaten MeshCollider varsa onu al, yoksa 1 KERE ekle
        cachedMeshCollider = bodyTransform.GetComponent<MeshCollider>();
        if (cachedMeshCollider == null)
        {
            cachedMeshCollider = bodyTransform.gameObject.AddComponent<MeshCollider>();
        }

        // MeshFilter'dan mesh bilgisini al ve Convex yap
        MeshFilter mf = bodyTransform.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            cachedMeshCollider.sharedMesh = mf.sharedMesh;
        }

        cachedMeshCollider.convex = true;

        // OYUN BAÞINDA PASÝF YAP (Performans için)
        cachedMeshCollider.enabled = false;
        isColliderActive = false;
    }

    private void FixedUpdate()
    {
        if (cachedMeshCollider == null) return;

        // Yakýnlýk kontrolü (Raycast + SphereCheck)
        bool isNearRoad = Physics.Raycast(transform.position + transform.up * 0.5f, -transform.up, detectionDistance, roadLayer) ||
                           Physics.CheckSphere(transform.position, detectionDistance, roadLayer);

        // Durum deðiþtiyse sadece 'enabled' deðerini aç/kapat (Anýnda çalýþýr, kasma yapmaz)
        if (isNearRoad && !isColliderActive)
        {
            cachedMeshCollider.enabled = true;
            isColliderActive = true;
        }
        else if (!isNearRoad && isColliderActive)
        {
            cachedMeshCollider.enabled = false;
            isColliderActive = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);
    }
}