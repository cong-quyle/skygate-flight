using SkyGate.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SkyGate.Rendering.Avionics
{
    // =====================================================================================
    // PFD
    // =====================================================================================
    public sealed class PfdDisplay : IDisplay
    {
        const float PxPerDeg = 6.5f;
        static readonly Vector2 Center = new Vector2(-10f, 40f);

        RectTransform _horizon, _bankPtr, _spdBug, _trend, _hdgBug, _v1, _vr, _v2;
        TapeScale _spd, _alt, _hdg;
        Text _spdBox, _altBox, _vs, _ra, _selAlt, _qnh, _mach, _selSpd;
        readonly Text[] _fma = new Text[5];
        int _cSpd = -1, _cAlt = int.MinValue, _cVs = int.MinValue, _cRa = int.MinValue, _cSelAlt = int.MinValue, _cQnh = int.MinValue, _cMach = -1, _cSelSpd = -1;
        string _fmaThrust, _fmaVert, _fmaLat, _fmaApp, _fmaEng;

        public void Build(RectTransform cell)
        {
            UIFactory.Box(cell, "bg", Vector2.zero, new Vector2(512, 512), AirbusColors.Panel);

            var mask = UIFactory.Clip(cell, "HorizonMask", Center, new Vector2(250, 260));
            _horizon = UIFactory.Rect("Horizon", mask, Vector2.zero, new Vector2(1200, 1200));
            UIFactory.Box(_horizon, "sky", new Vector2(0, 300), new Vector2(1200, 600), AirbusColors.Sky);
            UIFactory.Box(_horizon, "ground", new Vector2(0, -300), new Vector2(1200, 600), AirbusColors.Ground);
            UIFactory.Box(_horizon, "line", Vector2.zero, new Vector2(1200, 2), Color.white);
            for (float p = -30f; p <= 30.01f; p += 2.5f)
            {
                if (Mathf.Abs(p) < 0.1f) continue;
                bool ten = Mathf.Abs(Mathf.Repeat(p, 10f)) < 0.01f || Mathf.Abs(Mathf.Repeat(p, 10f) - 10f) < 0.01f;
                bool five = !ten && Mathf.Abs(Mathf.Repeat(p, 5f)) < 0.01f;
                float w = ten ? 80f : five ? 50f : 24f;
                UIFactory.Box(_horizon, "p" + p, new Vector2(0, p * PxPerDeg), new Vector2(w, 2f), Color.white);
                if (ten)
                {
                    string txt = StrCache.I(Mathf.Abs((int)p));
                    UIFactory.Label(_horizon, "pl" + p, new Vector2(-(w * 0.5f + 18f), p * PxPerDeg), new Vector2(30, 20), 16, Color.white, TextAnchor.MiddleRight, txt);
                    UIFactory.Label(_horizon, "pr" + p, new Vector2(w * 0.5f + 18f, p * PxPerDeg), new Vector2(30, 20), 16, Color.white, TextAnchor.MiddleLeft, txt);
                }
            }

            // thang roll cố định + kim xoay theo roll
            float[] bank = { 0f, 10f, 20f, 30f, 45f };
            const float R = 128f;
            foreach (float a in bank)
                foreach (float sg in new[] { -1f, 1f })
                {
                    if (a == 0f && sg > 0f) continue;
                    float ang = a * sg;
                    float len = (a == 0f || a >= 30f) ? 14f : 8f;
                    var t = UIFactory.Box(cell, "bk" + ang, Center + new Vector2(Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad)) * R,
                        new Vector2(2f, len), Color.white);
                    t.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang);
                }
            _bankPtr = UIFactory.Rect("BankPtr", cell, Center, new Vector2(1, 1));
            UIFactory.Box(_bankPtr, "tri", new Vector2(0, R - 12f), new Vector2(16, 14), AirbusColors.Yellow, UIFactory.Triangle());

            // ký hiệu máy bay
            UIFactory.Box(cell, "acL", Center + new Vector2(-62, 0), new Vector2(52, 6), AirbusColors.Yellow);
            UIFactory.Box(cell, "acLv", Center + new Vector2(-38, -8), new Vector2(6, 16), AirbusColors.Yellow);
            UIFactory.Box(cell, "acR", Center + new Vector2(62, 0), new Vector2(52, 6), AirbusColors.Yellow);
            UIFactory.Box(cell, "acRv", Center + new Vector2(38, -8), new Vector2(6, 16), AirbusColors.Yellow);
            UIFactory.Box(cell, "acC", Center, new Vector2(8, 8), AirbusColors.Yellow);

            // băng tốc độ
            _spd = new TapeScale(cell, "SpdTape", new Vector2(-195, 40), new Vector2(70, 260), true, 10f, 2, 3.5f,
                v => StrCache.I((int)v), AirbusColors.White, labelsOnRight: false) { minValue = 30f };
            _spdBug = UIFactory.Box(_spd.Root, "bug", new Vector2(24, 0), new Vector2(14, 14), AirbusColors.Magenta, UIFactory.Triangle()).rectTransform;
            _spdBug.localRotation = Quaternion.Euler(0, 0, 90);
            _trend = UIFactory.Box(_spd.Root, "trend", new Vector2(30, 0), new Vector2(4, 4), AirbusColors.Yellow).rectTransform;
            _v1 = UIFactory.Label(_spd.Root, "v1", new Vector2(0, 0), new Vector2(20, 20), 16, AirbusColors.Cyan, TextAnchor.MiddleCenter, "1").rectTransform;
            _vr = UIFactory.Box(_spd.Root, "vr", new Vector2(0, 0), new Vector2(10, 10), AirbusColors.Cyan, UIFactory.Circle()).rectTransform;
            _v2 = UIFactory.Label(_spd.Root, "v2", new Vector2(0, 0), new Vector2(20, 20), 16, AirbusColors.Magenta, TextAnchor.MiddleCenter, "2").rectTransform;
            UIFactory.Box(cell, "spdBoxBg", new Vector2(-188, 40), new Vector2(56, 34), Color.black);
            _spdBox = UIFactory.Label(cell, "spdBox", new Vector2(-188, 40), new Vector2(56, 34), 28, AirbusColors.Green, TextAnchor.MiddleRight);
            _selSpd = UIFactory.Label(cell, "selSpd", new Vector2(-195, 185), new Vector2(70, 22), 18, AirbusColors.Magenta);
            _mach = UIFactory.Label(cell, "mach", new Vector2(-195, -100), new Vector2(70, 22), 18, AirbusColors.Green);

            // băng độ cao
            _alt = new TapeScale(cell, "AltTape", new Vector2(175, 40), new Vector2(70, 260), true, 100f, 5, 0.13f,
                v => StrCache.I((int)(v / 100f)), AirbusColors.White, labelsOnRight: true);
            UIFactory.Box(cell, "altBoxBg", new Vector2(178, 40), new Vector2(66, 34), Color.black);
            _altBox = UIFactory.Label(cell, "altBox", new Vector2(178, 40), new Vector2(66, 34), 26, AirbusColors.Green, TextAnchor.MiddleCenter);
            _selAlt = UIFactory.Label(cell, "selAlt", new Vector2(175, 185), new Vector2(70, 22), 18, AirbusColors.Cyan);
            _qnh = UIFactory.Label(cell, "qnh", new Vector2(175, -100), new Vector2(70, 22), 18, AirbusColors.Cyan);
            _vs = UIFactory.Label(cell, "vs", new Vector2(232, 40), new Vector2(40, 22), 16, AirbusColors.Green);

            // băng hướng
            _hdg = new TapeScale(cell, "HdgTape", new Vector2(-10, -160), new Vector2(230, 40), false, 5f, 2, 6f,
                v => StrCache.I2((int)(Mathf.Repeat(v, 360f) / 10f)), AirbusColors.White, wrap: 360f);
            UIFactory.Box(cell, "hdgMark", new Vector2(-10, -136), new Vector2(2, 14), AirbusColors.Yellow);
            _hdgBug = UIFactory.Box(_hdg.Root, "bug", new Vector2(0, -12), new Vector2(12, 12), AirbusColors.Cyan, UIFactory.Triangle()).rectTransform;
            _ra = UIFactory.Label(cell, "ra", new Vector2(-10, -95), new Vector2(80, 24), 22, AirbusColors.Green);

            // FMA: 5 cột
            float[] fx = { -200f, -100f, 0f, 100f, 200f };
            for (int i = 0; i < 5; i++)
                _fma[i] = UIFactory.Label(cell, "fma" + i, new Vector2(fx[i], 225), new Vector2(98, 24), 16,
                    i < 3 ? AirbusColors.Green : AirbusColors.White);
            UIFactory.Box(cell, "fmaLine", new Vector2(0, 210), new Vector2(500, 1), new Color(1, 1, 1, 0.3f));
        }

        static void SetInt(Text t, ref int cache, int v, string prefix = null)
        {
            if (cache == v) return;
            cache = v;
            t.text = prefix == null ? StrCache.I(v) : prefix + StrCache.I(v);
        }

        public void UpdateFrom(AircraftStateBus bus)
        {
            var s = bus.state;
            _horizon.localRotation = Quaternion.Euler(0, 0, s.rollDeg);
            Vector3 off = Quaternion.Euler(0, 0, s.rollDeg) * new Vector3(0, -s.pitchDeg * PxPerDeg, 0);
            _horizon.anchoredPosition = new Vector2(off.x, off.y);
            _bankPtr.localRotation = Quaternion.Euler(0, 0, s.rollDeg);

            _spd.Update(s.iasKt);
            _alt.Update(s.altitudeFt);
            _hdg.Update(s.headingDeg);

            SetInt(_spdBox, ref _cSpd, Mathf.Max(0, (int)s.iasKt));
            SetInt(_altBox, ref _cAlt, (int)(Mathf.Round(s.altitudeFt / 10f) * 10f));
            SetInt(_selAlt, ref _cSelAlt, (int)(Mathf.Round(s.selAltitudeFt / 100f) * 100f));
            SetInt(_selSpd, ref _cSelSpd, (int)s.selSpeedKt);
            SetInt(_qnh, ref _cQnh, (int)s.baroHpa);
            int vs = (int)(Mathf.Round(s.vsFpm / 100f) * 100f);
            if (_cVs != vs)
            {
                _cVs = vs;
                _vs.text = Mathf.Abs(vs) > 200 ? StrCache.I(Mathf.Abs(vs) / 100) : "";
            }
            int ra = s.radioAltFt < 2500f ? (int)(Mathf.Round(s.radioAltFt / 5f) * 5f) : -1;
            if (_cRa != ra) { _cRa = ra; _ra.text = ra >= 0 ? StrCache.I(ra) : ""; }
            int mach = s.mach > 0.5f ? (int)(s.mach * 100f) : 0;
            if (_cMach != mach) { _cMach = mach; _mach.text = mach > 0 ? "." + StrCache.I2(mach % 100) : ""; }

            // mốc trên băng tốc độ
            float ppu = 3.5f, lim = 130f;
            _spdBug.anchoredPosition = new Vector2(24f, Mathf.Clamp((s.selSpeedKt - s.iasKt) * ppu, -lim, lim));
            float tr = Mathf.Clamp(s.iasTrendKt * ppu, -lim, lim);
            _trend.anchoredPosition = new Vector2(30f, tr * 0.5f);
            _trend.sizeDelta = new Vector2(4f, Mathf.Max(1f, Mathf.Abs(tr)));
            _v1.anchoredPosition = new Vector2(-10f, Mathf.Clamp((s.v1Kt - s.iasKt) * ppu, -200f, 200f));
            _vr.anchoredPosition = new Vector2(-10f, Mathf.Clamp((s.vrKt - s.iasKt) * ppu, -200f, 200f));
            _v2.anchoredPosition = new Vector2(-10f, Mathf.Clamp((s.v2Kt - s.iasKt) * ppu, -200f, 200f));
            bool showV = s.onGround || s.altitudeFt < 3000f;
            _v1.gameObject.SetActive(showV); _vr.gameObject.SetActive(showV); _v2.gameObject.SetActive(showV);

            _hdgBug.anchoredPosition = new Vector2(Mathf.Clamp(Mathf.DeltaAngle(s.headingDeg, s.selHeadingDeg) * 6f, -108f, 108f), -12f);

            SetFma(0, bus.fmaThrust, ref _fmaThrust);
            SetFma(1, bus.fmaVertical, ref _fmaVert);
            SetFma(2, bus.fmaLateral, ref _fmaLat);
            SetFma(3, bus.fmaApproach, ref _fmaApp);
            SetFma(4, bus.fmaEngage, ref _fmaEng);
        }

        void SetFma(int i, string v, ref string cache)
        {
            if ((object)cache == (object)v || cache == v) return;
            cache = v; _fma[i].text = v ?? "";
        }
    }

    // =====================================================================================
    // ND (ROSE)
    // =====================================================================================
    public sealed class NdDisplay : IDisplay
    {
        static Texture2D _roseTex;
        const float RoseSize = 370f;
        static readonly Vector2 C = new Vector2(0, -30);
        RectTransform _rose, _hdgBugPivot, _trkPivot;
        readonly Text[] _labels = new Text[12];
        Text _gs, _hdgText;
        int _cGs = -1, _cHdg = -1;

        static Texture2D RoseTexture()
        {
            if (_roseTex != null) return _roseTex;
            const int n = 256;
            _roseTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(fx * fx + fy * fy);
                    float a = 0f;
                    if (r > 0.935f && r < 0.97f) a = 1f;                       // vòng ngoài
                    if (r > 0.86f && r <= 0.935f)
                    {
                        float ang = Mathf.Repeat(Mathf.Atan2(fx, fy) * Mathf.Rad2Deg, 360f);
                        float m5 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 5f) * 5f));
                        float m10 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 10f) * 10f));
                        float px1 = r * (n * 0.5f) * Mathf.Deg2Rad;            // số px trên 1 độ cung
                        bool is10 = m10 < 0.01f + 0.7f / px1;
                        if (m5 * px1 < 0.8f && (is10 || r > 0.90f)) a = 1f;
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            _roseTex.SetPixels32(px);
            _roseTex.Apply(true, true);
            return _roseTex;
        }

        public void Build(RectTransform cell)
        {
            UIFactory.Box(cell, "bg", Vector2.zero, new Vector2(512, 512), AirbusColors.Panel);
            _rose = UIFactory.Rect("Rose", cell, C, new Vector2(RoseSize, RoseSize));
            var raw = _rose.gameObject.AddComponent<RawImage>();
            raw.texture = RoseTexture(); raw.color = Color.white; raw.raycastTarget = false;
            string[] names = { "N", "03", "06", "09", "12", "15", "18", "21", "24", "27", "30", "33" };
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                _labels[i] = UIFactory.Label(_rose, "lb" + i, new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (RoseSize * 0.5f - 36f),
                    new Vector2(34, 22), 17, i == 0 ? AirbusColors.Cyan : AirbusColors.White, TextAnchor.MiddleCenter, names[i]);
            }
            _hdgBugPivot = UIFactory.Rect("HdgBug", _rose, Vector2.zero, new Vector2(1, 1));
            UIFactory.Box(_hdgBugPivot, "b", new Vector2(0, RoseSize * 0.5f - 6f), new Vector2(14, 14), AirbusColors.Cyan, UIFactory.Triangle());
            _trkPivot = UIFactory.Rect("Trk", _rose, Vector2.zero, new Vector2(1, 1));
            var d = UIFactory.Box(_trkPivot, "d", new Vector2(0, RoseSize * 0.5f - 52f), new Vector2(10, 10), AirbusColors.Green);
            d.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            var ring = UIFactory.Box(cell, "range", C, new Vector2(RoseSize * 0.5f, RoseSize * 0.5f), new Color(0.6f, 0.6f, 0.6f, 0.6f), UIFactory.Ring());
            ring.rectTransform.sizeDelta = new Vector2(RoseSize * 0.5f, RoseSize * 0.5f);
            UIFactory.Box(cell, "ac", C + new Vector2(0, -2), new Vector2(20, 26), AirbusColors.Yellow, UIFactory.Triangle());
            UIFactory.Box(cell, "lubber", C + new Vector2(0, RoseSize * 0.5f + 8f), new Vector2(3, 18), AirbusColors.Yellow);
            _gs = UIFactory.Label(cell, "gs", new Vector2(-170, 225), new Vector2(150, 24), 18, AirbusColors.Green, TextAnchor.MiddleLeft);
            _hdgText = UIFactory.Label(cell, "hdg", new Vector2(0, 232), new Vector2(120, 28), 24, AirbusColors.Green);
            UIFactory.Label(cell, "mode", new Vector2(170, 225), new Vector2(120, 24), 18, AirbusColors.Cyan, TextAnchor.MiddleRight, "ROSE NAV");
        }

        public void UpdateFrom(AircraftStateBus bus)
        {
            var s = bus.state;
            _rose.localRotation = Quaternion.Euler(0, 0, s.headingDeg);
            var inv = Quaternion.Euler(0, 0, -s.headingDeg);
            for (int i = 0; i < _labels.Length; i++) _labels[i].rectTransform.localRotation = inv;
            _hdgBugPivot.localRotation = Quaternion.Euler(0, 0, -s.selHeadingDeg);
            _trkPivot.localRotation = Quaternion.Euler(0, 0, -s.trackDeg);
            int gs = (int)s.gsKt;
            if (gs != _cGs) { _cGs = gs; _gs.text = "GS " + StrCache.I(gs); }
            int h = Mathf.RoundToInt(Mathf.Repeat(s.headingDeg, 360f)) % 360;
            if (h != _cHdg) { _cHdg = h; _hdgText.text = StrCache.I3(h); }
        }
    }

    // =====================================================================================
    // E/WD
    // =====================================================================================
    public sealed class EwdDisplay : IDisplay
    {
        sealed class N1Gauge
        {
            public Image fill; public Text n1, egt, ff;
            public int cN1 = -1, cEgt = -1, cFf = -1;
        }

        readonly N1Gauge[] _g = new N1Gauge[2];
        Text _fob, _flap, _memo;
        int _cFob = -1, _cFlap = -1;
        string _cMemo;

        N1Gauge Gauge(RectTransform cell, string name, float x)
        {
            var g = new N1Gauge();
            var bg = UIFactory.Box(cell, name + "bg", new Vector2(x, 105), new Vector2(150, 150), new Color(0.3f, 0.3f, 0.33f, 0.5f), UIFactory.Ring());
            bg.rectTransform.localRotation = Quaternion.Euler(0, 0, 135);
            bg.type = Image.Type.Filled; bg.fillMethod = Image.FillMethod.Radial360; bg.fillOrigin = 2; bg.fillClockwise = true; bg.fillAmount = 0.75f;
            g.fill = UIFactory.Box(cell, name + "fill", new Vector2(x, 105), new Vector2(150, 150), AirbusColors.Green, UIFactory.Ring());
            g.fill.rectTransform.localRotation = Quaternion.Euler(0, 0, 135);
            g.fill.type = Image.Type.Filled; g.fill.fillMethod = Image.FillMethod.Radial360; g.fill.fillOrigin = 2; g.fill.fillClockwise = true; g.fill.fillAmount = 0f;
            UIFactory.Label(cell, name + "t", new Vector2(x, 175), new Vector2(60, 20), 16, AirbusColors.Cyan, TextAnchor.MiddleCenter, "N1 %");
            g.n1 = UIFactory.Label(cell, name + "n1", new Vector2(x, 100), new Vector2(110, 50), 40, AirbusColors.Green);
            UIFactory.Label(cell, name + "el", new Vector2(x - 55, 25), new Vector2(50, 20), 15, AirbusColors.Cyan, TextAnchor.MiddleLeft, "EGT");
            g.egt = UIFactory.Label(cell, name + "egt", new Vector2(x + 25, 25), new Vector2(70, 24), 22, AirbusColors.Green, TextAnchor.MiddleRight);
            UIFactory.Label(cell, name + "fl", new Vector2(x - 55, -5), new Vector2(50, 20), 15, AirbusColors.Cyan, TextAnchor.MiddleLeft, "FF");
            g.ff = UIFactory.Label(cell, name + "ff", new Vector2(x + 25, -5), new Vector2(70, 24), 22, AirbusColors.Green, TextAnchor.MiddleRight);
            return g;
        }

        public void Build(RectTransform cell)
        {
            UIFactory.Box(cell, "bg", Vector2.zero, new Vector2(512, 512), AirbusColors.Panel);
            _g[0] = Gauge(cell, "e1", -110f);
            _g[1] = Gauge(cell, "e2", 110f);
            UIFactory.Box(cell, "sep", new Vector2(0, -40), new Vector2(480, 1), new Color(1, 1, 1, 0.3f));
            UIFactory.Label(cell, "fobl", new Vector2(-170, -62), new Vector2(60, 22), 16, AirbusColors.Cyan, TextAnchor.MiddleLeft, "FOB");
            _fob = UIFactory.Label(cell, "fob", new Vector2(-95, -62), new Vector2(110, 26), 22, AirbusColors.Green, TextAnchor.MiddleRight);
            UIFactory.Label(cell, "fobu", new Vector2(-20, -62), new Vector2(40, 22), 15, AirbusColors.Cyan, TextAnchor.MiddleLeft, "KG");
            UIFactory.Label(cell, "flapl", new Vector2(100, -62), new Vector2(60, 22), 16, AirbusColors.Cyan, TextAnchor.MiddleLeft, "FLAPS");
            _flap = UIFactory.Label(cell, "flap", new Vector2(190, -62), new Vector2(80, 26), 22, AirbusColors.Green, TextAnchor.MiddleLeft);
            _memo = UIFactory.Label(cell, "memo", new Vector2(-40, -150), new Vector2(380, 110), 20, AirbusColors.Green, TextAnchor.UpperLeft);
        }

        public void UpdateFrom(AircraftStateBus bus)
        {
            var s = bus.state;
            Eng(_g[0], s.n1Eng1, s.egtEng1, s.ffEng1);
            Eng(_g[1], s.n1Eng2, s.egtEng2, s.ffEng2);
            int fob = (int)(Mathf.Round(s.fuelKg / 10f) * 10f);
            if (fob != _cFob) { _cFob = fob; _fob.text = StrCache.I(fob); }
            int fl = Mathf.RoundToInt(s.flapLever);
            if (fl != _cFlap) { _cFlap = fl; _flap.text = A320Data.FlapName(fl); }
            string memo = s.gearLever > 0.5f && !s.onGround ? "LDG GEAR DN" : (s.HasLight(AircraftLightFlags.Beacon) ? "BEACON ON" : "");
            if (memo != _cMemo) { _cMemo = memo; _memo.text = memo; }
        }

        static void Eng(N1Gauge g, float n1, float egt, float ff)
        {
            g.fill.fillAmount = Mathf.Clamp(n1 / 100f, 0f, 1.05f) * 0.75f;
            int a = (int)n1; if (a != g.cN1) { g.cN1 = a; g.n1.text = StrCache.I(a); }
            int b = (int)(egt / 5f) * 5; if (b != g.cEgt) { g.cEgt = b; g.egt.text = StrCache.I(b); }
            int c = (int)(ff / 10f) * 10; if (c != g.cFf) { g.cFf = c; g.ff.text = StrCache.I(c); }
            g.egt.color = egt > 950f ? AirbusColors.Amber : AirbusColors.Green;
        }
    }

    // =====================================================================================
    // SD (trang DOOR)
    // =====================================================================================
    public sealed class SdDisplay : IDisplay
    {
        readonly Image[] _doors = new Image[6];
        Text _gw;
        int _cMask = -1, _cGw = -1;

        public void Build(RectTransform cell)
        {
            UIFactory.Box(cell, "bg", Vector2.zero, new Vector2(512, 512), AirbusColors.Panel);
            UIFactory.Label(cell, "title", new Vector2(0, 225), new Vector2(120, 28), 24, AirbusColors.White, TextAnchor.MiddleCenter, "DOOR");
            UIFactory.Box(cell, "hullO", new Vector2(0, 10), new Vector2(64, 330), new Color(0.6f, 0.6f, 0.65f));
            UIFactory.Box(cell, "hullI", new Vector2(0, 10), new Vector2(58, 324), AirbusColors.Panel);
            // s -> y: y = 150 - s / 37.57 * 290
            float Y(float s) => 150f - s / A320Data.Length * 290f;
            _doors[0] = UIFactory.Box(cell, "L1", new Vector2(-40, Y(6.1f)), new Vector2(16, 26), AirbusColors.Green);
            _doors[1] = UIFactory.Box(cell, "R1", new Vector2(40, Y(6.1f)), new Vector2(16, 26), AirbusColors.Green);
            _doors[2] = UIFactory.Box(cell, "L4", new Vector2(-40, Y(31f)), new Vector2(16, 26), AirbusColors.Green);
            _doors[3] = UIFactory.Box(cell, "R4", new Vector2(40, Y(31f)), new Vector2(16, 26), AirbusColors.Green);
            _doors[4] = UIFactory.Box(cell, "CF", new Vector2(40, Y(8.6f)), new Vector2(16, 20), AirbusColors.Green);
            _doors[5] = UIFactory.Box(cell, "CA", new Vector2(40, Y(24.5f)), new Vector2(16, 20), AirbusColors.Green);
            UIFactory.Label(cell, "info", new Vector2(-120, -200), new Vector2(240, 24), 16, AirbusColors.Cyan, TextAnchor.MiddleLeft, "TAT +15 C   SAT +15 C");
            UIFactory.Label(cell, "gwl", new Vector2(40, -225), new Vector2(40, 24), 16, AirbusColors.Cyan, TextAnchor.MiddleLeft, "GW");
            _gw = UIFactory.Label(cell, "gw", new Vector2(130, -225), new Vector2(100, 24), 20, AirbusColors.Green, TextAnchor.MiddleLeft);
        }

        public void UpdateFrom(AircraftStateBus bus)
        {
            var s = bus.state;
            int mask = 0;
            if (s.doorL1 > 0.01f) mask |= 1;
            if (s.doorR1 > 0.01f) mask |= 2;
            if (s.doorL4 > 0.01f) mask |= 4;
            if (s.doorR4 > 0.01f) mask |= 8;
            if (s.doorCargoFwd > 0.01f) mask |= 16;
            if (s.doorCargoAft > 0.01f) mask |= 32;
            if (mask != _cMask)
            {
                _cMask = mask;
                for (int i = 0; i < 6; i++) _doors[i].color = (mask & (1 << i)) != 0 ? AirbusColors.Amber : AirbusColors.Green;
            }
            int gw = (int)(Mathf.Round(s.grossWeightKg / 100f) * 100f);
            if (gw != _cGw) { _cGw = gw; _gw.text = StrCache.I(gw); }
        }
    }
}
