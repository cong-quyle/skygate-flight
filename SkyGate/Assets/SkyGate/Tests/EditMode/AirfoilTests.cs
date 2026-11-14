using NUnit.Framework;
using SkyGate.Rendering.Procedural;

namespace SkyGate.Tests.EditMode
{
    public class AirfoilTests
    {
        [Test]
        public void HalfThickness_IsZeroAtLeadingEdge_AndNearZeroAtTrailingEdge()
        {
            Assert.That(Airfoil.HalfThickness(0f, 0.12f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(Airfoil.HalfThickness(1f, 0.12f), Is.LessThan(0.002f));
        }

        [Test]
        public void HalfThickness_NACA0012_PeaksNearThirtyPercentChord()
        {
            Assert.That(Airfoil.HalfThickness(0.3f, 0.12f), Is.EqualTo(0.06f).Within(1e-3f));
            Assert.That(Airfoil.HalfThickness(0.3f, 0.12f), Is.GreaterThan(Airfoil.HalfThickness(0.1f, 0.12f)));
            Assert.That(Airfoil.HalfThickness(0.3f, 0.12f), Is.GreaterThan(Airfoil.HalfThickness(0.8f, 0.12f)));
        }

        [Test]
        public void HalfThickness_ScalesLinearlyWithThickness_AndClampsX()
        {
            Assert.That(Airfoil.HalfThickness(0.4f, 0.24f), Is.EqualTo(2f * Airfoil.HalfThickness(0.4f, 0.12f)).Within(1e-5f));
            Assert.That(Airfoil.HalfThickness(-1f, 0.12f), Is.EqualTo(Airfoil.HalfThickness(0f, 0.12f)));
            Assert.That(Airfoil.HalfThickness(5f, 0.12f), Is.EqualTo(Airfoil.HalfThickness(1f, 0.12f)));
        }

        [Test]
        public void Camber_IsZeroWhenThereIsNoCamber()
        {
            Assert.That(Airfoil.Camber(0.5f, 0f, 0.4f), Is.EqualTo(0f));
            Assert.That(Airfoil.Camber(0.5f, -0.02f, 0.4f), Is.EqualTo(0f));
        }

        [Test]
        public void Camber_ReachesMaxAtMaxCamberPosition_AndClosesAtEdges()
        {
            const float m = 0.04f, p = 0.4f;
            Assert.That(Airfoil.Camber(0f, m, p), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(Airfoil.Camber(p, m, p), Is.EqualTo(m).Within(1e-5f));
            Assert.That(Airfoil.Camber(1f, m, p), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Airfoil.Camber(-2f, m, p), Is.EqualTo(Airfoil.Camber(0f, m, p)), "x is clamped");
        }

        [Test]
        public void Camber_IsContinuousAcrossMaxCamberPosition()
        {
            const float m = 0.04f, p = 0.4f;
            Assert.That(Airfoil.Camber(p - 1e-4f, m, p), Is.EqualTo(Airfoil.Camber(p + 1e-4f, m, p)).Within(1e-4f));
        }

        [Test]
        public void Spacing_FromLeadingEdge_UsesCosineDistribution()
        {
            var xs = Airfoil.Spacing(0f, 1f, 10);
            Assert.That(xs.Length, Is.EqualTo(11));
            Assert.That(xs[0], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(xs[10], Is.EqualTo(1f).Within(1e-5f));
            Assert.That(xs[1] - xs[0], Is.LessThan(0.1f), "denser than uniform near the leading edge");
            for (int i = 1; i < xs.Length; i++) Assert.That(xs[i], Is.GreaterThan(xs[i - 1]));
        }

        [Test]
        public void Spacing_AwayFromLeadingEdge_IsUniform()
        {
            var xs = Airfoil.Spacing(0.2f, 0.8f, 4);
            float[] expected = { 0.2f, 0.35f, 0.5f, 0.65f, 0.8f };
            Assert.That(xs.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < xs.Length; i++) Assert.That(xs[i], Is.EqualTo(expected[i]).Within(1e-5f));
        }
    }
}
