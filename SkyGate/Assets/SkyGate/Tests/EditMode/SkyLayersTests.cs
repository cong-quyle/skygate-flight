using NUnit.Framework;
using SkyGate.Rendering;
using UnityEngine;

namespace SkyGate.Tests.EditMode
{
    public class SkyLayersTests
    {
        [Test]
        public void Bit_IsOneShiftedByLayer()
        {
            Assert.That(SkyLayers.Bit(0), Is.EqualTo(1));
            Assert.That(SkyLayers.Bit(3), Is.EqualTo(8));
            Assert.That(SkyLayers.Bit(10), Is.EqualTo(1 << 10));
        }

        [Test]
        public void Layers_AreValidAndDistinct()
        {
            int[] l = { SkyLayers.Avionics, SkyLayers.Interior, SkyLayers.Hull };
            foreach (int v in l) Assert.That(v, Is.InRange(0, 31));
            Assert.That(l[0], Is.Not.EqualTo(l[1]));
            Assert.That(l[1], Is.Not.EqualTo(l[2]));
            Assert.That(l[0], Is.Not.EqualTo(l[2]));
        }

        [Test]
        public void Layers_UseTheProjectLayerWhenDefined_OtherwiseTheDefaultIndex()
        {
            AssertLayer(SkyLayers.AvionicsName, SkyLayers.AvionicsDefault, SkyLayers.Avionics);
            AssertLayer(SkyLayers.InteriorName, SkyLayers.InteriorDefault, SkyLayers.Interior);
            AssertLayer(SkyLayers.HullName, SkyLayers.HullDefault, SkyLayers.Hull);
        }

        static void AssertLayer(string name, int fallback, int actual)
        {
            int defined = LayerMask.NameToLayer(name);
            Assert.That(actual, Is.EqualTo(defined >= 0 ? defined : fallback), name);
        }

        [Test]
        public void Layers_AreStableAcrossCalls()
        {
            Assert.That(SkyLayers.Avionics, Is.EqualTo(SkyLayers.Avionics));
            Assert.That(SkyLayers.Hull, Is.EqualTo(SkyLayers.Hull));
        }

        [Test]
        public void SetLayerRecursive_AppliesToTheWholeHierarchy_AndNotToSiblings()
        {
            var root = new GameObject("root");
            var child = new GameObject("child"); child.transform.SetParent(root.transform);
            var grandchild = new GameObject("grandchild"); grandchild.transform.SetParent(child.transform);
            var sibling = new GameObject("sibling");
            try
            {
                SkyLayers.SetLayerRecursive(root, 9);
                Assert.That(root.layer, Is.EqualTo(9));
                Assert.That(child.layer, Is.EqualTo(9));
                Assert.That(grandchild.layer, Is.EqualTo(9));
                Assert.That(sibling.layer, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sibling);
            }
        }
    }
}
