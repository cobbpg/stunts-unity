using Stunts;
using UnityEngine;

public class AddVertexColor : MonoBehaviour {
	public Color vertexColor;

	[ContextMenu("Update Color")]
	void UpdateColor()
	{
		var mesh = GetComponent<MeshFilter>().mesh;
		var data = new Color[mesh.vertexCount];
		for (int i = 0; i < data.Length; i++)
		{
			data[i] = vertexColor;
		}
		mesh.colors = data;
		mesh.triangles = mesh.triangles;
	}
}
