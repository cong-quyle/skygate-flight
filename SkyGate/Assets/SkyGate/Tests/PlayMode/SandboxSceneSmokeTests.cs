using System.Collections;
using System.IO;
using NUnit.Framework;
using SkyGate.Core;
using SkyGate.Rendering.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace SkyGate.Tests.PlayMode
{
    /// <summary>
    /// Smoke test: mở GraphicsSandbox, chạy 2 giây, không được có lỗi/exception trong Console.
    /// Scene do menu "SkyGate > 2. Build Graphics Sandbox scene" sinh ra. Nếu scene chưa có trong repo thì test bị bỏ qua
    /// (Ignored), không phải đỏ, để CI vẫn chạy được trước khi bạn commit scene.
    /// Lưu ý: scene dùng camera/RenderTexture của avionics nên cần GPU; trên runner Linux không có GPU thật có thể lỗi.
    /// </summary>
    [Category("Smoke")]
    public class SandboxSceneSmokeTests
    {
        const string ScenePath = "Assets/SkyGate/Scenes/GraphicsSandbox.unity";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ScenePath)))
                Assert.Ignore("Chưa có " + ScenePath + ": chạy menu SkyGate > 2. Build Graphics Sandbox scene rồi commit scene.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
#else
            Assert.Ignore("Smoke test chỉ chạy trong Editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Scene_RunsTwoSecondsWithoutErrors()
        {
            for (int i = 0; i < 120; i++) yield return null;

            Assert.That(Object.FindFirstObjectByType<AircraftStateBus>() != null, Is.True, "thiếu AircraftStateBus");
            Assert.That(TouchFlightControls.Instance != null, Is.True, "thiếu TouchFlightControls");
            Assert.That(Camera.allCamerasCount, Is.GreaterThan(0));
            // LogAssert mặc định làm test đỏ nếu có Debug.LogError / exception trong lúc chạy.
        }

        [UnityTest]
        public IEnumerator Scene_AircraftStateStaysFinite()
        {
            for (int i = 0; i < 60; i++) yield return null;
            var s = Object.FindFirstObjectByType<AircraftStateBus>().state;
            foreach (float v in new[] { s.iasKt, s.altitudeFt, s.pitchDeg, s.rollDeg, s.headingDeg, s.n1Eng1, s.n1Eng2 })
                Assert.That(float.IsNaN(v) || float.IsInfinity(v), Is.False);
        }
    }
}
