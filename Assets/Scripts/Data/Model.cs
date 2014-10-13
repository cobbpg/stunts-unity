using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Stunts
{
	public class Model
	{
		public readonly Vector3[] Vertices;
		public readonly Primitive[] Primitives;
		public readonly int PaintJobCount;
		private static readonly List<float> sin = new List<float>();

		public Model(ReadStream input)
		{
			var vertexCount = input.ReadByte();
			var primitiveCount = input.ReadByte();
			PaintJobCount = input.ReadByte();
			input.Skip(1);

			Vertices = new Vector3[vertexCount];
			for (int i = 0; i < vertexCount; i++)
			{
				Vertices[i] = input.ReadVertex();
			}

			input.Skip(8 * primitiveCount);

			Primitives = new Primitive[primitiveCount];
			for (int i = 0; i < primitiveCount; i++)
			{
				Primitives[i] = new Primitive(PaintJobCount, input);
			}
		}

		public Mesh ToMesh(ImportSettings settings, int materialIndex = 0)
		{
			var mesh = new Mesh();
			var vertices = new List<Vector3>();
			var vertexColors = new List<Color>();
			var vertexMasks = new List<Vector2>();
			var indicesNoZBias = new List<int>();
			var indicesZBias = new List<int>();

			int index = 0;
			foreach (var primitive in Primitives)
			{
				var colorIndex = settings.materialDescriptions[primitive.Materials[materialIndex]].paletteIndex;
				var color = settings.palette[colorIndex];
				var indices = primitive.HasZBias ? indicesZBias : indicesNoZBias;
				var mask = settings.materialDescriptions[primitive.Materials[materialIndex]].mask;
				var maskAttr = new Vector2((mask & 0xff) / 255f, ((mask >> 8) & 0xff) / 255f);

				switch (primitive.Type)
				{
					case PrimitiveType.Polygon:
						for (int i = 2; i < primitive.Indices.Length; i++)
						{
							vertices.Add(Vertices[primitive.Indices[0]]);
							vertices.Add(Vertices[primitive.Indices[i]]);
							vertices.Add(Vertices[primitive.Indices[i - 1]]);
							for (int j = 0; j < 3; j++)
							{
								vertexColors.Add(color);
								vertexMasks.Add(maskAttr);
								indices.Add(index++);
							}
						}
						break;
					case PrimitiveType.Line:
						var v1 = Vertices[primitive.Indices[0]];
						var v2 = Vertices[primitive.Indices[1]];
						var dir = (v2 - v1).normalized;
						var refdir = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? Vector3.up : Vector3.right;
						var radius = Vector3.Distance(v1, v2) * 0.007f;
						var perp1 = Vector3.Cross(dir, refdir) * radius;
						var perp2 = Vector3.Cross(dir, perp1);
						if (sin.Count == 0)
						{
							for (int i = 0; i < 11; i++)
							{
								sin.Add(Mathf.Sin(i * Mathf.PI / 4f));
							}
						}
						for (int i = 0; i < 8; i++)
						{
							if (i >= 2)
							{
								vertices.Add(v1 + perp1 * sin[0] + perp2 * sin[2]);
								vertices.Add(v1 + perp1 * sin[i] + perp2 * sin[i + 2]);
								vertices.Add(v1 + perp1 * sin[i - 1] + perp2 * sin[i + 1]);
								vertices.Add(v2 + perp1 * sin[0] + perp2 * sin[2]);
								vertices.Add(v2 + perp1 * sin[i] + perp2 * sin[i + 2]);
								vertices.Add(v2 + perp1 * sin[i - 1] + perp2 * sin[i + 1]);
								for (int j = 0; j < 6; j++)
								{
									vertexColors.Add(color);
									vertexMasks.Add(maskAttr);
									indices.Add(index++);
								}
							}
							vertices.Add(v1 + perp1 * sin[i] + perp2 * sin[i + 2]);
							vertices.Add(v1 + perp1 * sin[i + 1] + perp2 * sin[i + 3]);
							vertices.Add(v2 + perp1 * sin[i + 1] + perp2 * sin[i + 3]);
							vertices.Add(v2 + perp1 * sin[i + 1] + perp2 * sin[i + 3]);
							vertices.Add(v2 + perp1 * sin[i] + perp2 * sin[i + 2]);
							vertices.Add(v1 + perp1 * sin[i] + perp2 * sin[i + 2]);
							for (int j = 0; j < 6; j++)
							{
								vertexColors.Add(color);
								vertexMasks.Add(maskAttr);
								indices.Add(index++);
							}
						}
						break;
					default:
						break;
				}
			}

			mesh.vertices = vertices.Select(v => new Vector3(v.x, v.y, -v.z)).ToArray();
			mesh.colors = vertexColors.ToArray();
			mesh.uv = vertexMasks.ToArray();
			mesh.subMeshCount = 2;
			mesh.SetTriangles(indicesNoZBias.ToArray(), 0);
			mesh.SetTriangles(indicesZBias.ToArray(), 1);
			mesh.RecalculateNormals();

			return mesh;
		}
	}
}

