using System.Collections.Generic;
using GWOO.Editor.Tools;
using NUnit.Framework;
using UnityEngine;

namespace GWOO.MaterialManager.Tests.Editor
{
    public sealed class MaterialFilterServiceTests
    {
        private readonly List<Material> _materials = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Material material in _materials)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            _materials.Clear();
        }

        [Test]
        public void RebuildVisible_FiltersBySearchAndExcludeTag()
        {
            MaterialFilterService service = new();
            MaterialManagerState state = new();

            state.foundMaterials.Add(new MaterialListItem(CreateMaterial("mat_fire_elite")));
            state.foundMaterials.Add(new MaterialListItem(CreateMaterial("mat_fire_ice")));
            state.foundMaterials.Add(new MaterialListItem(CreateMaterial("mat_poison")));

            state.searchQuery = "fire -ice";

            service.RebuildVisible(state);

            Assert.That(state.visibleMaterials.Count, Is.EqualTo(1));
            Assert.That(state.visibleMaterials[0].Material.name, Is.EqualTo("mat_fire_elite"));
        }

        [Test]
        public void RebuildVisible_IgnoreNullMaterialItems()
        {
            MaterialFilterService service = new();
            MaterialManagerState state = new();

            state.foundMaterials.Add(new MaterialListItem(null));
            state.foundMaterials.Add(new MaterialListItem(CreateMaterial("mat_valid")));
            state.searchQuery = string.Empty;

            service.RebuildVisible(state);

            Assert.That(state.visibleMaterials.Count, Is.EqualTo(1));
            Assert.That(state.visibleMaterials[0].Material.name, Is.EqualTo("mat_valid"));
        }

        private Material CreateMaterial(string name)
        {
            Shader shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null, "Sprites/Default shader is required for tests.");

            Material material = new(shader)
            {
                name = name
            };

            _materials.Add(material);
            return material;
        }
    }
}
