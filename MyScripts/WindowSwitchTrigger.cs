using System.Collections.Generic;
using UnityEngine;

public class WindowSwitchTrigger : MonoBehaviour
{
    [Header("Yetkili Parmaklar / Objeler")]
    public List<Collider> AuthorizedColliders = new();

    private List<Collider> _activeColliders = new();

    // Butonun okuyacaðý özellik
    public bool IsBeingTouched => _activeColliders.Count > 0;

    void OnTriggerEnter(Collider other)
    {
        if (AuthorizedColliders.Contains(other) && !_activeColliders.Contains(other))
        {
            _activeColliders.Add(other);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (_activeColliders.Contains(other))
        {
            _activeColliders.Remove(other);
        }
    }

    void Update()
    {
        // Obje deaktif edilirse veya silinirse listeyi temizle
        _activeColliders.RemoveAll(item => item == null || !item.enabled || !item.gameObject.activeInHierarchy);
    }
}