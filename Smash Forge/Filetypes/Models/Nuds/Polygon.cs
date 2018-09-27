﻿using OpenTK;
using SFGraphics.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Smash_Forge
{
    public partial class NUD
    {
        public class Polygon : TreeNode
        {
            // Bone types and vertex types control two bytes of the vertsize.
            public enum BoneTypes
            {
                NoBones =  0x00,
                Float = 0x10,
                HalfFloat = 0x20,
                Byte = 0x40
            }

            public enum VertexTypes
            {
                NoNormals = 0x0,
                NormalsFloat = 0x1,
                NormalsTanBiTanFloat = 0x3,
                NormalsHalfFloat = 0x6,
                NormalsTanBiTanHalfFloat = 0x7
            }

            // Used to generate a unique color for viewport selection.
            private static List<int> previousDisplayIds = new List<int>();
            public int DisplayId { get { return displayId; } }
            private int displayId = 0;

            // The number of vertices is vertexIndices.Count because many vertices are shared.
            public List<Vertex> vertices = new List<Vertex>();
            public List<int> vertexIndices = new List<int>();
            public int displayFaceSize = 0;

            public NudRenderMesh renderMesh;

            public List<Material> materials = new List<Material>();

            // defaults to a basic bone weighted vertex format
            public int vertSize = (int)BoneTypes.Byte | (int)VertexTypes.NormalsHalfFloat;

            public int UVSize = 0x12;
            public int strip = 0x40;
            public int polflag = 0x04;

            // for drawing
            public bool isTransparent = false;
            public int[] display;
            public int[] selectedVerts;

            public Polygon()
            {
                Checked = true;
                Text = "Polygon";
                ImageKey = "polygon";
                SelectedImageKey = "polygon";
                GenerateDisplayId();
            }

            public void AddVertex(Vertex v)
            {
                vertices.Add(v);
            }

            private void GenerateDisplayId()
            {
                // Find last used ID. Next ID will be last ID + 1.
                // A color is generated from the integer as hexadecimal, but alpha is ignored.
                // Incrementing will affect RGB before it affects Alpha (ARGB color).
                int index = 0;
                if (previousDisplayIds.Count > 0)
                    index = previousDisplayIds.Last();
                index++;
                previousDisplayIds.Add(index);
                displayId = index;
            }

            public void AOSpecRefBlend()
            {
                // change aomingain to only affect specular and reflection. ignore 2nd material
                if (materials[0].entries.ContainsKey("NU_aoMinGain"))
                {
                    materials[0].entries["NU_aoMinGain"][0] = 15.0f;
                    materials[0].entries["NU_aoMinGain"][1] = 15.0f;
                    materials[0].entries["NU_aoMinGain"][2] = 15.0f;
                    materials[0].entries["NU_aoMinGain"][3] = 0.0f;
                }
            }

            public void GetDisplayVerticesAndIndices(out List<DisplayVertex> displayVerticesList, out List<int> vertexIndicesList)
            {
                displayVerticesList = CreateDisplayVertices();
                vertexIndicesList = new List<int>(display);
            }

            private List<DisplayVertex> CreateDisplayVertices()
            {
                // rearrange faces
                display = GetRenderingVertexIndices().ToArray();

                List<DisplayVertex> displayVertList = new List<DisplayVertex>();

                if (vertexIndices.Count < 3)
                    return displayVertList;
                foreach (Vertex v in vertices)
                {
                    DisplayVertex displayVert = new DisplayVertex()
                    {
                        pos = v.pos,
                        nrm = v.nrm,
                        tan = v.tan.Xyz,
                        bit = v.bitan.Xyz,
                        col = v.color / 127,
                        uv = v.uv.Count > 0 ? v.uv[0] : new Vector2(0, 0),
                        uv2 = v.uv.Count > 1 ? v.uv[1] : new Vector2(0, 0),
                        uv3 = v.uv.Count > 2 ? v.uv[2] : new Vector2(0, 0),
                        boneIds = new Vector4(
                            v.boneIds.Count > 0 ? v.boneIds[0] : -1,
                            v.boneIds.Count > 1 ? v.boneIds[1] : -1,
                            v.boneIds.Count > 2 ? v.boneIds[2] : -1,
                            v.boneIds.Count > 3 ? v.boneIds[3] : -1),
                        weight = new Vector4(
                            v.boneWeights.Count > 0 ? v.boneWeights[0] : 0,
                            v.boneWeights.Count > 1 ? v.boneWeights[1] : 0,
                            v.boneWeights.Count > 2 ? v.boneWeights[2] : 0,
                            v.boneWeights.Count > 3 ? v.boneWeights[3] : 0),
                    };
                    displayVertList.Add(displayVert);
                }

                selectedVerts = new int[displayVertList.Count];
                return displayVertList;
            }

            public void CalculateTangentBitangent()
            {
                // Don't generate tangents and bitangents if the vertex format doesn't support them. 
                int vertType = vertSize & 0xF;
                if (!(vertType == 3 || vertType == 7))
                    return;

                List<int> f = GetRenderingVertexIndices();
                Vector3[] tanArray = new Vector3[vertices.Count];
                Vector3[] bitanArray = new Vector3[vertices.Count];

                CalculateTanBitanArrays(f, tanArray, bitanArray);
                ApplyTanBitanArray(tanArray, bitanArray);
            }

            public void SetVertexColor(Vector4 intColor)
            {
                // (127, 127, 127, 127) is white.
                foreach (Vertex v in vertices)
                {
                    v.color = intColor;
                }
            }


            private void ApplyTanBitanArray(Vector3[] tanArray, Vector3[] bitanArray)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vertex v = vertices[i];
                    Vector3 newTan = tanArray[i];
                    Vector3 newBitan = bitanArray[i];

                    // The tangent and bitangent should be orthogonal to the normal but not each other. 
                    // Bitangents are not calculated with a cross product to prevent flipped shading with mirrored normal maps.
                    v.tan = new Vector4(VectorUtils.Orthogonalize(newTan, v.nrm), 1);
                    v.bitan = new Vector4(VectorUtils.Orthogonalize(newBitan, v.nrm), 1);
                    v.bitan *= -1;
                }
            }

            private void CalculateTanBitanArrays(List<int> faces, Vector3[] tanArray, Vector3[] bitanArray)
            {
                // Three verts per face.
                for (int i = 0; i < displayFaceSize; i += 3)
                {
                    Vertex v1 = vertices[faces[i]];
                    Vertex v2 = vertices[faces[i + 1]];
                    Vertex v3 = vertices[faces[i + 2]];

                    // Check for index out of range errors and just skip this face.
                    if (v1.uv.Count < 1 || v2.uv.Count < 1 || v3.uv.Count < 1)
                        continue;

                    Vector3 s = new Vector3();
                    Vector3 t = new Vector3();
                    VectorUtils.GenerateTangentBitangent(v1.pos, v2.pos, v3.pos, v1.uv[0], v2.uv[0], v3.uv[0], out s, out t);

                    // Average tangents and bitangents.
                    tanArray[faces[i]] += s;
                    tanArray[faces[i + 1]] += s;
                    tanArray[faces[i + 2]] += s;

                    bitanArray[faces[i]] += t;
                    bitanArray[faces[i + 1]] += t;
                    bitanArray[faces[i + 2]] += t;
                }
            }

            public void SmoothNormals()
            {
                Vector3[] normals = new Vector3[vertices.Count];

                List<int> f = GetRenderingVertexIndices();

                for (int i = 0; i < displayFaceSize; i += 3)
                {
                    Vertex v1 = vertices[f[i]];
                    Vertex v2 = vertices[f[i+1]];
                    Vertex v3 = vertices[f[i+2]];
                    Vector3 nrm = VectorUtils.CalculateNormal(v1.pos, v2.pos, v3.pos);

                    normals[f[i + 0]] += nrm;
                    normals[f[i + 1]] += nrm;
                    normals[f[i + 2]] += nrm;
                }
                
                for (int i = 0; i < normals.Length; i++)
                    vertices[i].nrm = normals[i].Normalized();

                // Compare each vertex with all the remaining vertices. This might skip some.
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vertex v = vertices[i];

                    for (int j = i + 1; j < vertices.Count; j++)
                    {
                        Vertex v2 = vertices[j];

                        if (v == v2)
                            continue;
                        float dis = (float)Math.Sqrt(Math.Pow(v.pos.X - v2.pos.X, 2) + Math.Pow(v.pos.Y - v2.pos.Y, 2) + Math.Pow(v.pos.Z - v2.pos.Z, 2));
                        if (dis <= 0f) // Extra smooth
                        {
                            Vector3 nn = ((v2.nrm + v.nrm) / 2).Normalized();
                            v.nrm = nn;
                            v2.nrm = nn;
                        }
                    }
                }
            }

            public void CalculateNormals()
            {
                Vector3[] normals = new Vector3[vertices.Count];

                for (int i = 0; i < normals.Length; i++)
                    normals[i] = new Vector3(0, 0, 0);

                List<int> f = GetRenderingVertexIndices();

                for (int i = 0; i < displayFaceSize; i += 3)
                {
                    Vertex v1 = vertices[f[i]];
                    Vertex v2 = vertices[f[i + 1]];
                    Vertex v3 = vertices[f[i + 2]];
                    Vector3 nrm = VectorUtils.CalculateNormal(v1.pos, v2.pos, v3.pos);

                    normals[f[i + 0]] += nrm * (nrm.Length / 2);
                    normals[f[i + 1]] += nrm * (nrm.Length / 2);
                    normals[f[i + 2]] += nrm * (nrm.Length / 2);
                }

                for (int i = 0; i < normals.Length; i++)
                    vertices[i].nrm = normals[i].Normalized();
            }

            public void AddDefaultMaterial()
            {
                Material mat = Material.GetDefault();
                materials.Add(mat);
                mat.textures.Add(new MatTexture(0x10000000));
                mat.textures.Add(MatTexture.GetDefault());
            }

            public List<int> GetRenderingVertexIndices()
            {
                if ((strip >> 4) == 4)
                {
                    displayFaceSize = vertexIndices.Count;
                    return vertexIndices;
                }
                else
                {
                    List<int> vertexIndices = new List<int>();

                    int startDirection = 1;
                    int p = 0;
                    int f1 = this.vertexIndices[p++];
                    int f2 = this.vertexIndices[p++];
                    int faceDirection = startDirection;
                    int f3;
                    do
                    {
                        f3 = this.vertexIndices[p++];
                        if (f3 == 0xFFFF)
                        {
                            f1 = this.vertexIndices[p++];
                            f2 = this.vertexIndices[p++];
                            faceDirection = startDirection;
                        }
                        else
                        {
                            faceDirection *= -1;
                            if ((f1 != f2) && (f2 != f3) && (f3 != f1))
                            {
                                if (faceDirection > 0)
                                {
                                    vertexIndices.Add(f3);
                                    vertexIndices.Add(f2);
                                    vertexIndices.Add(f1);
                                }
                                else
                                {
                                    vertexIndices.Add(f2);
                                    vertexIndices.Add(f3);
                                    vertexIndices.Add(f1);
                                }
                            }
                            f1 = f2;
                            f2 = f3;
                        }
                    } while (p < this.vertexIndices.Count);

                    displayFaceSize = vertexIndices.Count;
                    return vertexIndices;
                }
            }
        }
    }
}

