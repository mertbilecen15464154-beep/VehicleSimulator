using UnityEngine;
using System.Collections.Generic;

namespace BNG
{
    public class OptimizedVehicleMirrors : MonoBehaviour
    {
        [System.Serializable]
        public class MirrorPoint
        {
            public string Name; // Inspector'da buraya yazdýðýn isim görünür
            public Transform Anchor;
            public Renderer MirrorRenderer;
            public Vector2Int Resolution = new(512, 512);
            public bool InvertMirror = false;
            [HideInInspector] public Camera activeCamera;
            [HideInInspector] public RenderTexture rt;
        }

        [Header("Kamera / Ayna Listesi")]
        public List<MirrorPoint> mirrors = new List<MirrorPoint>();

        [Header("Optimization & Performance")]
        public float maxDistance = 15f;

        [Tooltip("Saniyede kaç kare render edilsin?")]
        public int targetFPS = 60;

        private bool hasCameras = false;
        private Transform playerTransform;
        private float nextUpdateTime;
        private int mirrorIndex = 0;

        void Start()
        {
            playerTransform = Camera.main != null ? Camera.main.transform : null;
        }

        void Update()
        {
            if (playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            // OYUNCU YAKINDAYSA AKTÝF ET
            if (dist <= maxDistance && !hasCameras)
            {
                AssignCameras();
            }
            // UZAKLAÞTIYSA KAPAT (PERFORMANS)
            else if (dist > maxDistance && hasCameras)
            {
                RemoveCameras();
            }

            // RENDER DÖNGÜSÜ
            if (hasCameras && mirrors.Count > 0 && Time.time >= nextUpdateTime)
            {
                RenderNextMirror();

                // FPS deðerini süreye çeviriyoruz: 1 / 60 = 0.016ms
                // Toplam yükü aynalara bölüyoruz ki sistem sarsýlmasýn
                float interval = 1f / Mathf.Max(targetFPS, 1);
                nextUpdateTime = Time.time + (interval / mirrors.Count);
            }
        }

        void AssignCameras()
        {
            if (MirrorManager.Instance == null) return;

            foreach (var mp in mirrors)
            {
                AssignSingleMirror(mp);
            }

            hasCameras = true;
        }

        void AssignSingleMirror(MirrorPoint mp)
        {
            mp.activeCamera = MirrorManager.Instance.GetCameraFromPool();
            if (mp.activeCamera == null) return;

            mp.rt = RenderTexture.GetTemporary(mp.Resolution.x, mp.Resolution.y, 16);
            mp.activeCamera.targetTexture = mp.rt;

            mp.activeCamera.transform.SetParent(mp.Anchor);
            mp.activeCamera.transform.SetPositionAndRotation(mp.Anchor.position, mp.Anchor.rotation);

            if (mp.InvertMirror)
                mp.activeCamera.projectionMatrix *= Matrix4x4.Scale(new Vector3(-1, 1, 1));

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", mp.rt);
            mp.MirrorRenderer.SetPropertyBlock(block);
        }

        void RemoveCameras()
        {
            foreach (var mp in mirrors)
            {
                ReturnMirror(mp);
            }
            hasCameras = false;
        }

        void ReturnMirror(MirrorPoint mp)
        {
            if (mp.activeCamera != null)
            {
                if (MirrorManager.Instance != null)
                    MirrorManager.Instance.ReturnToPool(mp.activeCamera);

                mp.activeCamera = null;
            }

            if (mp.rt != null)
            {
                RenderTexture.ReleaseTemporary(mp.rt);
                mp.rt = null;
            }
        }

        void RenderNextMirror()
        {
            if (mirrors.Count == 0) return;

            RenderMirror(mirrors[mirrorIndex]);
            mirrorIndex = (mirrorIndex + 1) % mirrors.Count;
        }

        void RenderMirror(MirrorPoint mp)
        {
            if (mp.activeCamera == null) return;
            if (mp.InvertMirror) GL.invertCulling = true;
            mp.activeCamera.Render();
            if (mp.InvertMirror) GL.invertCulling = false;
        }

        void OnDestroy() => RemoveCameras();
    }
}