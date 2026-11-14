using System;
using System.Collections.Generic;
using SkyGate.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SkyGate.Rendering.Avionics
{
    public static class AirbusColors
    {
        public static readonly Color Green = new Color(0f, 0.9f, 0.2f);
        public static readonly Color Cyan = new Color(0f, 0.85f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.25f, 1f);
        public static readonly Color Amber = new Color(1f, 0.62f, 0f);
        public static readonly Color Yellow = new Color(1f, 1f, 0f);
        public static readonly Color Red = new Color(1f, 0.15f, 0.1f);
        public static readonly Color Sky = new Color(0.11f, 0.53f, 0.93f);
        public static readonly Color Ground = new Color(0.55f, 0.33f, 0.15f);
        public static readonly Color White = Color.white;
        public static readonly Color Panel = new Color(0.05f, 0.06f, 0.08f);
    }

    /// <summary>Cache chuỗi số để cập nhật chữ trên màn hình không sinh rác (GC) mỗi frame.</summary>
    public static class StrCache
    {
        static readonly string[] _i = new string[10001];
        static readonly string[] _i3 = new string[1000];
        static readonly string[] _i2 = new string[100];

        public static string I(int n)
        {
            if (n < 0 || n > 10000) return n.ToString();
            return _i[n] ?? (_i[n] = n.ToString());
        }

        public static string I3(int n)
        {
            n = Mathf.Clamp(n, 0, 999);
            return _i3[n] ?? (_i3[n] = n.ToString("000"));
        }

        public static string I2(int n)
        {
            n = Mathf.Clamp(n, 0, 99);
            return _i2[n] ?? (_i2[n] = n.ToString("00"));
        }
    }

    /// <summary>Hàm tạo phần tử uGUI. Mọi phần tử neo ở GIỮA cha, pivot giữa, toạ độ tính từ tâm cha.</summary>
    public static class UIFactory
    {
        static Font _font;
        static Sprite _circle, _ring, _triangle;

        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                return _font;
            }
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, string text = "")
        {
            var rt = Rect(name, parent, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        public static RectTransform Clip(Transform parent, string name, Vector2 pos, Vector2 size, Color? bg = null)
        {
            var rt = Rect(name, parent, pos, size);
            if (bg.HasValue)
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.color = bg.Value; img.raycastTarget = false;
            }
            rt.gameObject.AddComponent<RectMask2D>();
            return rt;
        }

        public static Sprite Circle() => _circle != null ? _circle : (_circle = MakeSprite(64, (x, y, n) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((1f - r) * n * 0.5f);
        }));

        public static Sprite Ring() => _ring != null ? _ring : (_ring = MakeSprite(128, (x, y, n) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01((1f - r) * n * 0.5f) * Mathf.Clamp01((r - 0.84f) * n * 0.5f);
            return a;
        }));

        public static Sprite Triangle() => _triangle != null ? _triangle : (_triangle = MakeSprite(64, (x, y, n) =>
        {
            // tam giác cân đỉnh hướng lên: x,y ∈ [-1,1]
            float yy = (y + 1f) * 0.5f;            // 0 đáy, 1 đỉnh
            float halfW = 1f - yy;                 // rộng dần về đáy
            float edge = Mathf.Clamp01((halfW - Mathf.Abs(x)) * n * 0.5f);
            return edge * Mathf.Clamp01(yy * n * 0.5f + 0.01f) * (yy <= 1f ? 1f : 0f);
        }));

        static Sprite MakeSprite(int n, Func<float, float, int, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                    byte a = (byte)(Mathf.Clamp01(alpha(fx, fy, n)) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }

    /// <summary>
    /// Băng số cuộn (tốc độ, độ cao, hướng). Dùng pool cố định: mỗi lần cập nhật chỉ đặt lại vị trí vài chục phần tử,
    /// chữ chỉ gán khi giá trị nhãn đổi.
    /// </summary>
    public sealed class TapeScale
    {
        readonly RectTransform[] _ticks;
        readonly Text[] _labels;
        readonly float[] _lastLabel;
        readonly bool _vertical;
        readonly float _unit, _px, _wrap;
        readonly int _labelEvery;
        readonly Func<float, string> _fmt;
        readonly float _half;
        public float minValue = float.NegativeInfinity;
        public RectTransform Root { get; private set; }

        public TapeScale(Transform parent, string name, Vector2 pos, Vector2 size, bool vertical, float unitPerTick,
            int labelEvery, float pxPerUnit, Func<float, string> fmt, Color color, bool labelsOnRight = true,
            float wrap = 0f, int fontSize = 18)
        {
            _vertical = vertical; _unit = unitPerTick; _px = pxPerUnit; _wrap = wrap; _labelEvery = labelEvery; _fmt = fmt;
            var root = UIFactory.Clip(parent, name, pos, size, new Color(0.12f, 0.13f, 0.16f, 0.85f));
            Root = root;
            float extent = (vertical ? size.y : size.x) * 0.5f;
            _half = extent / pxPerUnit;
            int n = Mathf.CeilToInt(2f * _half / unitPerTick) + 3;
            _ticks = new RectTransform[n];
            _labels = new Text[n];
            _lastLabel = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 tsz = vertical ? new Vector2(14f, 2f) : new Vector2(2f, 12f);
                var img = UIFactory.Box(root, "t" + i, Vector2.zero, tsz, color);
                _ticks[i] = img.rectTransform;
                _labels[i] = UIFactory.Label(root, "l" + i, Vector2.zero, new Vector2(46f, 22f), fontSize, color,
                    vertical ? (labelsOnRight ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight) : TextAnchor.MiddleCenter);
                _lastLabel[i] = float.NaN;
            }
            _sizeAcross = vertical ? size.x : size.y;
            _labelsOnRight = labelsOnRight;
        }

        readonly float _sizeAcross;
        readonly bool _labelsOnRight;

        public void Update(float value)
        {
            int i0 = Mathf.FloorToInt((value - _half) / _unit) - 1;
            for (int k = 0; k < _ticks.Length; k++)
            {
                int idx = i0 + k;
                float v = idx * _unit;
                float delta = _wrap > 0f ? Mathf.DeltaAngle(value, v) : v - value;
                bool visible = v >= minValue;
                _ticks[k].gameObject.SetActive(visible);
                bool hasLabel = visible && (idx % _labelEvery == 0);
                if (_labels[k].gameObject.activeSelf != hasLabel) _labels[k].gameObject.SetActive(hasLabel);
                if (!visible) continue;
                float p = delta * _px;
                if (_vertical)
                {
                    float edge = (_labelsOnRight ? -1f : 1f) * (_sizeAcross * 0.5f - 7f);
                    _ticks[k].anchoredPosition = new Vector2(edge, p);
                    if (hasLabel) _labels[k].rectTransform.anchoredPosition = new Vector2(edge * -0.15f + (_labelsOnRight ? -2f : 2f) , p);
                }
                else
                {
                    _ticks[k].anchoredPosition = new Vector2(p, -(_sizeAcross * 0.5f - 6f) * -1f);
                    if (hasLabel) _labels[k].rectTransform.anchoredPosition = new Vector2(p, -2f);
                }
                if (hasLabel && !Mathf.Approximately(_lastLabel[k], v))
                {
                    _lastLabel[k] = v;
                    _labels[k].text = _fmt(v);
                }
            }
        }
    }

    public interface IDisplay
    {
        void Build(RectTransform cell);
        void UpdateFrom(AircraftStateBus bus);
    }
}
