using NUnit.Framework;
using SkyGate.Core;

namespace SkyGate.Tests.EditMode
{
    public class A320DataTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Dimensions_AreCloseToPublishedA320Values()
        {
            Assert.That(A320Data.Length, Is.InRange(37f, 38f));
            Assert.That(A320Data.Wingspan, Is.InRange(35f, 36f));
            Assert.That(A320Data.Height, Is.InRange(11f, 12f));
            Assert.That(A320Data.WingSemiSpan * 2f, Is.LessThan(A320Data.Wingspan), "wingspan includes the sharklets");
            Assert.That(A320Data.MainGearTrack, Is.GreaterThan(0f));
            Assert.That(A320Data.Wheelbase, Is.GreaterThan(0f));
        }

        [TestCase(0f)]
        [TestCase(12.5f)]
        [TestCase(37.57f)]
        public void SToZ_And_ZToS_AreInverse(float s)
        {
            Assert.That(A320Data.ZToS(A320Data.SToZ(s)), Is.EqualTo(s).Within(Eps));
        }

        [Test]
        public void SToZ_NoseIsAtNoseZ_AndZDecreasesTowardsTail()
        {
            Assert.That(A320Data.SToZ(0f), Is.EqualTo(A320Data.NoseZ).Within(Eps));
            Assert.That(A320Data.SToZ(A320Data.Length), Is.LessThan(A320Data.SToZ(0f)));
        }

        [Test]
        public void FlapConfigs_HasFiveDetents_AndAnglesNeverDecrease()
        {
            var f = A320Data.FlapConfigs;
            Assert.That(f.Length, Is.EqualTo(5));
            Assert.That(f[0].name, Is.EqualTo("0"));
            Assert.That(f[4].name, Is.EqualTo("FULL"));
            for (int i = 1; i < f.Length; i++)
            {
                Assert.That(f[i].slatDeg, Is.GreaterThanOrEqualTo(f[i - 1].slatDeg), "slat @" + i);
                Assert.That(f[i].flapDeg, Is.GreaterThanOrEqualTo(f[i - 1].flapDeg), "flap @" + i);
            }
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(1f, 18f, 10f)]
        [TestCase(2f, 22f, 15f)]
        [TestCase(3f, 22f, 20f)]
        [TestCase(4f, 27f, 40f)]
        public void FlapAngles_AtDetent_MatchesTable(float pos, float slat, float flap)
        {
            A320Data.FlapAngles(pos, out float s, out float f);
            Assert.That(s, Is.EqualTo(slat).Within(Eps));
            Assert.That(f, Is.EqualTo(flap).Within(Eps));
        }

        [TestCase(0.5f, 9f, 5f)]
        [TestCase(3.5f, 24.5f, 30f)]
        public void FlapAngles_BetweenDetents_Interpolates(float pos, float slat, float flap)
        {
            A320Data.FlapAngles(pos, out float s, out float f);
            Assert.That(s, Is.EqualTo(slat).Within(Eps));
            Assert.That(f, Is.EqualTo(flap).Within(Eps));
        }

        [TestCase(-3f, 0f, 0f)]
        [TestCase(99f, 27f, 40f)]
        public void FlapAngles_OutOfRange_IsClamped(float pos, float slat, float flap)
        {
            A320Data.FlapAngles(pos, out float s, out float f);
            Assert.That(s, Is.EqualTo(slat).Within(Eps));
            Assert.That(f, Is.EqualTo(flap).Within(Eps));
        }

        [TestCase(0f, "0")]
        [TestCase(1f, "1+F")]
        [TestCase(2f, "2")]
        [TestCase(3f, "3")]
        [TestCase(4f, "FULL")]
        [TestCase(1.4f, "1+F")]
        [TestCase(1.6f, "2")]
        [TestCase(-5f, "0")]
        [TestCase(9f, "FULL")]
        public void FlapName_RoundsAndClamps(float lever, string expected)
        {
            Assert.That(A320Data.FlapName(lever), Is.EqualTo(expected));
        }
    }
}
