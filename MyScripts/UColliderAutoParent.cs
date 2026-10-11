using UnityEngine;
using UnityEditor;

public class UColliderAutoParent : EditorWindow
{
    public GameObject horseRoot;      // Atýn ana objesi (Horse)
    public GameObject uColliderGroup; // UCollider'larýn durduðu ana obje (Body / Horse Body)

    [MenuItem("Tools/UCollider Auto Parent")]
    public static void ShowWindow()
    {
        GetWindow<UColliderAutoParent>("UCollider Auto Parent");
    }

    private void OnGUI()
    {
        GUILayout.Label("UCollider'larý En Yakýn Kemiðe Baðla", EditorStyles.boldLabel);

        horseRoot = (GameObject)EditorGUILayout.ObjectField("At Ana Objesi (Horse)", horseRoot, typeof(GameObject), true);
        uColliderGroup = (GameObject)EditorGUILayout.ObjectField("UCollider'lar Objesi", uColliderGroup, typeof(GameObject), true);

        if (GUILayout.Button("Collider'larý Otomatik Kemiðe Baðla"))
        {
            if (horseRoot == null || uColliderGroup == null)
            {
                Debug.LogError("Lütfen her iki objeyi de sürükleyip býrakýn!");
                return;
            }

            AutoParentColliders();
        }
    }

    private void AutoParentColliders()
    {
        // Atýn altýndaki tüm Transform'larý (Kemikleri) topla
        Transform[] allBones = horseRoot.GetComponentsInChildren<Transform>();

        // UCollider grubunun altýndaki tüm çocuklarý al
        int movedCount = 0;
        Transform[] colliders = uColliderGroup.GetComponentsInChildren<Transform>();

        Undo.RegisterCompleteObjectUndo(uColliderGroup, "Auto Parent UColliders");

        foreach (Transform col in colliders)
        {
            // Ana kapsayýcýnýn kendisini atla
            if (col == uColliderGroup.transform) continue;

            Transform nearestBone = null;
            float minDistance = float.MaxValue;

            // En yakýn kemiði hesapla
            foreach (Transform bone in allBones)
            {
                // UCollider'larýn kendisini kemik olarak hesaba katma
                if (bone.IsChildOf(uColliderGroup.transform)) continue;

                float dist = Vector3.Distance(col.position, bone.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestBone = bone;
                }
            }

            // En yakýn kemiði bulduysa onun altýna child yap
            if (nearestBone != null)
            {
                Undo.SetTransformParent(col, nearestBone, "Re-parent UCollider");
                movedCount++;
            }
        }

        Debug.Log($"Ýþlem Tamamlandý! Toplam {movedCount} adet UCollider en yakýn kemiklerin altýna taþýndý.");
    }
}