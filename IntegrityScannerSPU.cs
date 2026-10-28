using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using UnityEngine;
using UnityEngine.Rendering;

namespace ServerIntegrity
{
    public sealed class IntegrityScannerSPU : SensorProcessingUnit
    {
        private const float ViewDistance = 50f;
        private const int MaxBatchSize = 1023;
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly Vector4 ProtectedColor = new Vector4(1f, 0.12f, 0.08f, 1f);
        private static readonly Vector4 VulnerableColor = new Vector4(0.1f, 1f, 0.2f, 1f);

        // Populated from the native T-Ray scanner prefab when this component is cloned.
        [SerializeField]
        private Material _material;

        private readonly Dictionary<int, ScannerMeshBatch> _batches =
            new Dictionary<int, ScannerMeshBatch>(128);
        private readonly List<ScannerMeshBatch> _batchList = new List<ScannerMeshBatch>(128);
        private MaterialPropertyBlock _propertyBlock;

        public override void Render()
        {
            if (GameManager.IsBatchMode || _material == null) return;
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

            Vector3 origin = RootParent != null ? RootParent.ThingTransformPosition : ThingTransformPosition;
            float maxDistanceSquared = ViewDistance * ViewDistance;
            GridController.AllStructuresPool.ForEach(structure =>
            {
                if (structure == null || structure.IsBeingDestroyed || structure.Renderers == null ||
                    structure.Renderers.Count == 0 ||
                    (structure.ThingTransformPosition - origin).sqrMagnitude > maxDistanceSquared) return;

                ThingRenderer renderer = structure.Renderers[0];
                Mesh mesh = renderer?.MeshFilter?.sharedMesh;
                if (mesh == null) return;

                int key = mesh.GetInstanceID();
                Vector4 color = structure.Indestructable ? ProtectedColor : VulnerableColor;
                if (_batches.TryGetValue(key, out ScannerMeshBatch batch))
                {
                    batch.Matrices.Add(structure.GetBatchMatrix());
                    batch.Colors.Add(color);
                    return;
                }

                batch = new ScannerMeshBatch
                {
                    Mesh = mesh,
                    Matrices = new List<Matrix4x4> { structure.GetBatchMatrix() },
                    Colors = new List<Vector4> { color }
                };
                _batches.Add(key, batch);
                _batchList.Add(batch);
            });

            foreach (ScannerMeshBatch batch in _batchList)
            {
                int sourceIndex = 0;
                while (sourceIndex < batch.Matrices.Count)
                {
                    int count = Math.Min(MaxBatchSize, batch.Matrices.Count - sourceIndex);
                    Matrix4x4[] matrices = new Matrix4x4[count];
                    Vector4[] colors = new Vector4[count];
                    batch.Matrices.CopyTo(sourceIndex, matrices, 0, count);
                    batch.Colors.CopyTo(sourceIndex, colors, 0, count);
                    _propertyBlock.Clear();
                    _propertyBlock.SetVectorArray(ColorProperty, colors);
                    Graphics.DrawMeshInstanced(batch.Mesh, 0, _material, matrices, count,
                        _propertyBlock, ShadowCastingMode.Off, false);
                    sourceIndex += count;
                }
            }

            _batches.Clear();
            _batchList.Clear();
            _propertyBlock.Clear();
        }
    }
}
