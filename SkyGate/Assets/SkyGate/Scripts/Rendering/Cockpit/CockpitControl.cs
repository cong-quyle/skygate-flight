using System;
using UnityEngine;

namespace SkyGate.Rendering.Cockpit
{
    public enum ControlKind { Button, Toggle, Knob, Lever }

    /// <summary>
    /// Nút/cần có thể chạm trong buồng lái. Rendering KHÔNG đổi trạng thái mô phỏng:
    /// chỉ phát sự kiện <see cref="Pressed"/> kèm id, hệ thống nào quan tâm tự lắng nghe.
    /// Highlight (nhấp nháy emission) dùng cho tutorial mức "Người mới".
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CockpitControl : MonoBehaviour
    {
        public string id;
        public ControlKind kind = ControlKind.Button;
        public Renderer[] highlightRenderers;

        public static event Action<string> Pressed;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock _mpb;
        bool _highlight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Pressed = null;

        public void Press() => Pressed?.Invoke(id);

        public void SetHighlight(bool on)
        {
            _highlight = on;
            if (!on) Apply(Color.black);
        }

        void Awake()
        {
            if (highlightRenderers == null || highlightRenderers.Length == 0)
                highlightRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void Update()
        {
            if (!_highlight) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            Apply(new Color(0.2f, 1.0f, 0.4f) * (0.4f + 1.6f * k));
        }

        void Apply(Color c)
        {
            if (highlightRenderers == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var r in highlightRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
