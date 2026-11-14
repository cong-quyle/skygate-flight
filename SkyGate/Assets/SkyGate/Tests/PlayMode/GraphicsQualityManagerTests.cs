using System.Collections;
using NUnit.Framework;
using SkyGate.Core;
using SkyGate.Rendering.Aircraft;
using SkyGate.Rendering.Avionics;
using SkyGate.Rendering.Quality;
using UnityEngine;
using UnityEngine.TestTools;

namespace SkyGate.Tests.PlayMode
{
    /// <summary>
    /// Các mức đồ họa Low/Medium/High (plan mục 11). Test chỉ kiểm cấu hình mà tier áp dụng; FPS thật và nhiệt
    /// chỉ đo được trên điện thoại (xem Tests/README.md).
    /// </summary>
    public class GraphicsQualityManagerTests
    {
        GameObject _go;
        GraphicsQualityManager _mgr;

        // trạng thái toàn cục mà ApplyTier ghi đè: lưu để trả lại sau mỗi test
        int _fps, _vsync, _mip, _hz;
        float _lod;
        AircraftLightFlags _mask;

        [SetUp]
        public void SetUp()
        {
            _fps = Application.targetFrameRate; _vsync = QualitySettings.vSyncCount; _lod = QualitySettings.lodBias;
            _mip = QualitySettings.globalTextureMipmapLimit; _hz = AvionicsSystem.TargetHz; _mask = AircraftLights.RealLightMask;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            Application.targetFrameRate = _fps; QualitySettings.vSyncCount = _vsync; QualitySettings.lodBias = _lod;
            QualitySettings.globalTextureMipmapLimit = _mip; AvionicsSystem.TargetHz = _hz; AircraftLights.RealLightMask = _mask;
        }

        void Create(QualityTier tier)
        {
            _go = new GameObject("QualityTest");
            _go.SetActive(false);                       // đặt tier trước khi Awake chạy
            _mgr = _go.AddComponent<GraphicsQualityManager>();
            _mgr.tier = tier;
            _go.SetActive(true);
        }

        [UnityTest]
        public IEnumerator High_Targets60Fps_WithAllRealLightsAndFullAvionics()
        {
            Create(QualityTier.High);
            yield return null;
            Assert.That(_mgr.tier, Is.EqualTo(QualityTier.High));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
            Assert.That(QualitySettings.lodBias, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(0));
            Assert.That(AvionicsSystem.TargetHz, Is.EqualTo(30));
            Assert.That(AircraftLights.RealLightMask,
                Is.EqualTo(AircraftLightFlags.Landing | AircraftLightFlags.Taxi | AircraftLightFlags.RunwayTurnoff));
            Assert.That(_mgr.CurrentRenderScale, Is.EqualTo(0.80f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Medium_Is30Fps_WithOnlyTheLandingLightReal()
        {
            Create(QualityTier.Medium);
            yield return null;
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            Assert.That(QualitySettings.lodBias, Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(0));
            Assert.That(AvionicsSystem.TargetHz, Is.EqualTo(20));
            Assert.That(AircraftLights.RealLightMask, Is.EqualTo(AircraftLightFlags.Landing));
            Assert.That(_mgr.CurrentRenderScale, Is.EqualTo(0.75f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Low_Is30Fps_WithNoRealLights_AndHalfResTextures()
        {
            Create(QualityTier.Low);
            yield return null;
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            Assert.That(QualitySettings.lodBias, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(1));
            Assert.That(AvionicsSystem.TargetHz, Is.EqualTo(12));
            Assert.That(AircraftLights.RealLightMask, Is.EqualTo(AircraftLightFlags.None));
            Assert.That(_mgr.CurrentRenderScale, Is.EqualTo(0.65f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator CycleTier_GoesHighLowMediumHigh()
        {
            Create(QualityTier.High);
            yield return null;
            _mgr.CycleTier(); Assert.That(_mgr.tier, Is.EqualTo(QualityTier.Low));
            _mgr.CycleTier(); Assert.That(_mgr.tier, Is.EqualTo(QualityTier.Medium));
            _mgr.CycleTier(); Assert.That(_mgr.tier, Is.EqualTo(QualityTier.High));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60), "quay vòng về High phải trả lại 60 fps");
        }

        [UnityTest]
        public IEnumerator ApplyTier_ResetsThermalLevelAndRenderScale()
        {
            Create(QualityTier.High);
            yield return null;
            _mgr.ApplyTier(QualityTier.Low);
            Assert.That(_mgr.ThermalLevel, Is.EqualTo(0));
            Assert.That(_mgr.CurrentRenderScale, Is.EqualTo(0.65f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Instance_IsSet_ThenClearedOnDestroy()
        {
            Create(QualityTier.High);
            yield return null;
            Assert.That(GraphicsQualityManager.Instance, Is.SameAs(_mgr));
            Object.Destroy(_go);
            yield return null;
            Assert.That(GraphicsQualityManager.Instance == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Update_TracksGpuFrameTime()
        {
            Create(QualityTier.High);
            yield return null;
            yield return new WaitForSeconds(0.2f);
            Assert.That(_mgr.GpuMs, Is.GreaterThan(0f));
            Assert.That(float.IsNaN(_mgr.GpuMs), Is.False);
        }

        [UnityTest]
        public IEnumerator DynamicResolutionAndThermal_CanBeDisabled()
        {
            Create(QualityTier.High);
            _mgr.dynamicResolution = false;
            _mgr.thermalThrottle = false;
            yield return new WaitForSeconds(0.2f);
            Assert.That(_mgr.CurrentRenderScale, Is.EqualTo(0.80f).Within(1e-4f));
        }
    }
}
