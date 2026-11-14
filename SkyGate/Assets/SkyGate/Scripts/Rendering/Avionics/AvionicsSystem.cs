using SkyGate.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SkyGate.Rendering.Avionics
{
    /// <summary>
    /// 4 màn hình (PFD, ND, E/WD, SD) vẽ bằng uGUI vào MỘT render texture atlas 1024×1024 (lưới 2×2).
    /// Chỉ render khi `visible` (camera ở buồng lái), theo tần số TargetHz (High = 30 Hz).
    /// Mọi DisplayUnit của máy bay dùng chung một material runtime có emission = atlas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvionicsSystem : MonoBehaviour
    {
        public AircraftStateBus bus;
        public int atlasSize = 1024;
        [Range(0.2f, 2f)] public float brightness = 1f;

        /// <summary>Tần số cập nhật màn hình, GraphicsQualityManager đặt theo tier.</summary>
        public static int TargetHz = 30;

        public bool visible;

        RenderTexture _rt;
        GameObject _root;
        Camera _cam;
        Material _runtimeMat;
        IDisplay[] _displays;
        float _acc;
        bool _built, _needFirst;
        UniversalRenderPipeline.SingleCameraRequest _request;
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            if (bus == null) bus = GetComponentInParent<AircraftStateBus>();
            Build();
        }

        public void SetVisible(bool v)
        {
            if (visible == v) return;
            visible = v;
            if (v) _needFirst = true;
        }

        void Build()
        {
            if (_built) return;
            int cellPx = atlasSize / 2;

            _rt = new RenderTexture(atlasSize, atlasSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "AvionicsAtlas",
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                antiAliasing = 1,
            };
            _rt.Create();

            _root = new GameObject("AvionicsRoot");
            _root.layer = SkyLayers.Avionics;
            _root.transform.position = new Vector3(0f, -3000f, 0f);

            var camGo = new GameObject("AvionicsCamera");
            camGo.layer = SkyLayers.Avionics;
            camGo.transform.SetParent(_root.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = atlasSize * 0.5f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;
            _cam.cullingMask = SkyLayers.Bit(SkyLayers.Avionics);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 50f;
            _cam.allowMSAA = false;
            _cam.allowHDR = false;
            _cam.targetTexture = _rt;
            _cam.enabled = false;
            var data = _cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;

            var canvasGo = new GameObject("AvionicsCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.layer = SkyLayers.Avionics;
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 10f;

            var canvasRt = (RectTransform)canvasGo.transform;
            _displays = new IDisplay[] { new PfdDisplay(), new NdDisplay(), new EwdDisplay(), new SdDisplay() };
            float h = cellPx * 0.5f;
            Vector2[] pos = { new Vector2(-h, h), new Vector2(h, h), new Vector2(-h, -h), new Vector2(h, -h) };
            for (int i = 0; i < _displays.Length; i++)
            {
                var cell = UIFactory.Clip(canvasRt, "Cell" + i, pos[i], new Vector2(cellPx, cellPx), Color.black);
                _displays[i].Build(cell);
            }

            // material runtime dùng chung cho mọi DU của máy bay (không sửa asset gốc)
            var units = GetComponentsInChildren<DisplayUnit>(true);
            if (units.Length > 0)
            {
                var src = units[0].screen.sharedMaterials[Mathf.Clamp(units[0].screenMaterialIndex, 0, units[0].screen.sharedMaterials.Length - 1)];
                _runtimeMat = new Material(src) { name = "AvionicsScreen(Runtime)" };
                _runtimeMat.SetTexture(EmissionMap, _rt);
                _runtimeMat.SetColor("_BaseColor", Color.black);
                _runtimeMat.EnableKeyword("_EMISSION");
                _runtimeMat.SetColor(EmissionColor, Color.white * brightness);
                foreach (var du in units)
                {
                    var mats = du.screen.sharedMaterials;
                    int idx = Mathf.Clamp(du.screenMaterialIndex, 0, mats.Length - 1);
                    mats[idx] = _runtimeMat;
                    du.screen.sharedMaterials = mats;
                }
            }
            _built = true;
            _needFirst = true;
        }

        void Update()
        {
            if (!_built || !visible || bus == null) return;
            if (_runtimeMat != null) _runtimeMat.SetColor(EmissionColor, Color.white * brightness);
            _acc += Time.unscaledDeltaTime;
            float interval = 1f / Mathf.Max(1, TargetHz);
            if (!_needFirst && _acc < interval) return;
            _acc = 0f;
            _needFirst = false;
            for (int i = 0; i < _displays.Length; i++) _displays[i].UpdateFrom(bus);
            Canvas.ForceUpdateCanvases();
            RenderOnce();
        }

        void RenderOnce()
        {
            _request = new UniversalRenderPipeline.SingleCameraRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, _request))
                RenderPipeline.SubmitRenderRequest(_cam, _request);
            else
                _cam.Render(); // dự phòng: URP bản cũ / pipeline khác
        }

        void OnDestroy()
        {
            if (_runtimeMat != null) Destroy(_runtimeMat);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_root != null) Destroy(_root);
        }
    }
}
