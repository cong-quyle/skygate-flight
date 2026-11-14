using System.Reflection;
using NUnit.Framework;
using SkyGate.Core;
using UnityEngine;

namespace SkyGate.Tests.EditMode
{
    public class LiveryDefinitionTests
    {
        LiveryDefinition _livery;

        [SetUp] public void SetUp() { _livery = ScriptableObject.CreateInstance<LiveryDefinition>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_livery); }

        [Test]
        public void Defaults_DescribeAPlaceholderAirline()
        {
            Assert.That(_livery.displayName, Is.Not.Empty);
            Assert.That(_livery.airlineIcao, Has.Length.EqualTo(3));
            Assert.That(_livery.registration, Does.StartWith("VN-A"));
            Assert.That(_livery.callsign, Is.Not.Empty);
            Assert.That(_livery.titleText, Is.Not.Empty);
        }

        [Test]
        public void Defaults_UserSuppliedTextures_AreEmpty()
        {
            // Logo/wordmark là thương hiệu: người dùng tự cung cấp, repo không được tự mang theo.
            Assert.That(_livery.fuselageWordmark == null, Is.True);
            Assert.That(_livery.tailLogo == null, Is.True);
        }

        [Test]
        public void Defaults_PositionsLieOnTheFuselage()
        {
            Assert.That(_livery.swooshStartS, Is.InRange(0f, A320Data.Length));
            Assert.That(_livery.titleCenterS, Is.InRange(0f, A320Data.Length));
            Assert.That(_livery.registrationS, Is.InRange(0f, A320Data.Length));
            Assert.That(_livery.titleHeight, Is.GreaterThan(0f));
            Assert.That(_livery.registrationHeight, Is.GreaterThan(0f));
            Assert.That(_livery.swooshWidth, Is.GreaterThan(0f));
            Assert.That(_livery.tailLogoScale, Is.InRange(0.01f, 1f));
            Assert.That(Mathf.Abs(_livery.titleCenterYRel), Is.LessThan(A320Data.FuselageHalfHeight));
        }

        [Test]
        public void Defaults_AllColors_AreOpaqueAndInRange()
        {
            int checkedColors = 0;
            foreach (FieldInfo f in typeof(LiveryDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(Color)) continue;
                var c = (Color)f.GetValue(_livery);
                Assert.That(c.a, Is.EqualTo(1f).Within(1e-4f), f.Name + " alpha");
                foreach (float v in new[] { c.r, c.g, c.b })
                    Assert.That(v, Is.InRange(0f, 1f), f.Name);
                checkedColors++;
            }
            Assert.That(checkedColors, Is.GreaterThan(10), "reflection không tìm thấy các trường Color");
        }

        [Test]
        public void SwooshColors_AreRedAndYellowStyle()
        {
            Assert.That(_livery.swooshColor.r, Is.GreaterThan(_livery.swooshColor.g));
            Assert.That(_livery.swooshAccent.r, Is.GreaterThan(0.9f));
            Assert.That(_livery.swooshAccent.g, Is.GreaterThan(0.7f));
            Assert.That(_livery.rearSwoosh, Is.True);
        }
    }
}
