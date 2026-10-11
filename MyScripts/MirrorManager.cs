using UnityEngine;
using System.Collections.Generic;

namespace BNG
{
    public class MirrorManager : MonoBehaviour
    {
        public static MirrorManager Instance;

        [Header("Pool Settings")]
        public GameObject mirrorCameraPrefab; // Sadece Camera bileþeni olan bir Prefab
        public int poolSize = 12; // Sahnede toplam kaç ayna kamerasý olsun? (4 araç x 3 ayna = 12)

        private readonly List<Camera> cameraPool = new();
        private readonly Queue<Camera> availableCameras = new();

        void Awake()
        {
            Instance = this;
            // Kamera havuzunu oluþtur
            for (int i = 0; i < poolSize; i++)
            {
                GameObject camObj = Instantiate(mirrorCameraPrefab, transform);
                Camera cam = camObj.GetComponent<Camera>();
                cam.enabled = false;
                cameraPool.Add(cam);
                availableCameras.Enqueue(cam);
            }
        }

        public Camera GetCameraFromPool()
        {
            if (availableCameras.Count > 0) return availableCameras.Dequeue();
            return null; // Havuz boþsa kamera verme (Performans sýnýrý)
        }

        public void ReturnToPool(Camera cam)
        {
            cam.enabled = false;
            cam.transform.SetParent(transform);
            cam.targetTexture = null;
            availableCameras.Enqueue(cam);
        }
    }
}