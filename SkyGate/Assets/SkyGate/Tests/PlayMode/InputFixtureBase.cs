using NUnit.Framework;
using SkyGate.Core;
using UnityEngine.InputSystem;

namespace SkyGate.Tests.PlayMode
{
    /// <summary>
    /// Nền chung cho test PlayMode cần giả lập thiết bị. Dùng InputTestFixture theo kiểu "bọc" (không kế thừa)
    /// để khỏi phụ thuộc cách override Setup/TearDown. Cô lập hoàn toàn thiết bị thật và trả lại trạng thái
    /// toàn cục (PilotInputBus) sau mỗi test.
    /// </summary>
    public abstract class InputFixtureBase
    {
        protected InputTestFixture Input;
        PilotControls _savedControls;

        [SetUp]
        public virtual void BaseSetUp()
        {
            _savedControls = PilotInputBus.Current;
            PilotInputBus.Current = PilotControls.Default();
            Input = new InputTestFixture();
            Input.Setup();
        }

        [TearDown]
        public virtual void BaseTearDown()
        {
            Input.TearDown();
            PilotInputBus.Current = _savedControls;
        }
    }
}
