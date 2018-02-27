﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Windows.Forms;
using SALT.Graphics;
using Smash_Forge.Rendering.Lights;
using Smash_Forge.Rendering;

namespace Smash_Forge
{
    public class NUD : FileBase, IDisposable
    {
        public NUD()
        {
            if (!Runtime.shaders.ContainsKey("NUD"))
            {
                ShaderTools.CreateShader("NUD", "/lib/Shader/Legacy/", "/lib/Shader/");
            }

            if (!Runtime.shaders.ContainsKey("NUD_Debug"))
            {
                ShaderTools.CreateShader("NUD_Debug", "/lib/Shader/Legacy/", "/lib/Shader/");
            }

            Runtime.shaders["NUD"].displayCompilationWarning("NUD");
            Runtime.shaders["NUD_Debug"].displayCompilationWarning("NUD_Debug");

            GL.GenBuffers(1, out vbo_position);
            GL.GenBuffers(1, out ibo_elements);
            GL.GenBuffers(1, out ubo_bones);
            GL.GenBuffers(1, out vbo_select);

            Text = "model.nud";
            ImageKey = "model";
            SelectedImageKey = "model";
        }

        public NUD(string fname) : this()
        {
            Read(fname);
            UpdateVertexDataAndSort();
        }

        int vbo_position;
        int ibo_elements;
        int ubo_bones;
        int vbo_select;

        public const int SMASH = 0;
        public const int POKKEN = 1;
        public int type = SMASH;
        public int boneCount = 0;
        public bool hasBones = false;
        List<Mesh> depthSortedMeshes = new List<Mesh>();
        public float[] boundingBox = new float[4];

        // xmb stuff
        public int lightSetNumber = 0;
        public int directUVTime = 0;
        public int drawRange = 0;
        public int drawingOrder = 0;
        public bool useDirectUVTime = false;
        public string modelType = "";

        public override Endianness Endian { get; set; }

        #region Rendering

        private DisplayVertex[] Vertices;
        private int[] Faces;

        public void Destroy()
        {
            GL.DeleteBuffer(vbo_position);
            GL.DeleteBuffer(ibo_elements);
            GL.DeleteBuffer(ubo_bones);
            GL.DeleteBuffer(vbo_select);

            Nodes.Clear();
        }

        public enum TextureFlags
        {
            Glow = 0x00000080,
            Shadow = 0x00000040,
            DummyRamp = 0x00000020,
            SphereMap = 0x00000010,
            StageAOMap = 0x00000008,
            RampCubeMap = 0x00000004,
            NormalMap = 0x00000002,
            DiffuseMap = 0x00000001
        }

        public enum DummyTextures
        {
            StageMapLow = 0x10101000,
            StageMapHigh = 0x10102000,
            PokemonStadium = 0x10040001,
            PunchOut = 0x10040000,
            DummyRamp = 0x10080000,
            ShadowMap = 0x10100000
        }

        public enum LightSetColors
        {
            Black = 0,
            Red = 1,
            Green = 2,
            Blue = 3,
            Orange = 4,
            Yellow = 5,
            Cyan = 6,
            Magenta = 7,
            Purple = 8,
            Grey = 9,
            White = 15
        }

        public enum SrcFactors
        {
            Nothing = 0x0,
            SourceAlpha = 0x1,
            One = 0x2,
            InverseSourceAlpha = 0x3,
            SourceColor = 0x4,
            Zero = 0xA
        }


        private void DepthSortMeshes()
        {
            foreach (Mesh m in Nodes)
            {
                m.calculateSortBias();

                if (m.Text.Contains("BILLBOARDYAXIS"))
                {
                    m.billboardY = true;
                }
                else if (m.Text.Contains("BILLBOARD"))
                {
                    m.billboard = true;
                }
                else if (m.Text.Contains("NSC"))
                {
                    m.nsc = true;
                }
            }
            List<Mesh> meshes = new List<Mesh>();
            foreach(Mesh m in Nodes)
                meshes.Add(m);
            depthSortedMeshes = meshes.OrderBy(o => (o.boundingBox[2] - o.boundingBox[3] + o.sortBias)).ToList();
        }

        public void UpdateVertexDataAndSort()
        {
            int poffset = 0;
            int voffset = 0;
            List<DisplayVertex> Vs = new List<DisplayVertex>();
            List<int> Ds = new List<int>();
            for (int mes = Nodes.Count - 1; mes >= 0; mes--)
            {
                Mesh m = (Mesh)Nodes[mes];
                for (int pol = m.Nodes.Count - 1; pol >= 0; pol--)
                {
                    Polygon p = (NUD.Polygon)m.Nodes[pol];
                    p.Offset = poffset * 4;
                    List<DisplayVertex> pv = p.CreateDisplayVertices();
                    Vs.AddRange(pv);
                    //Console.WriteLine(p.displayFaceSize + " " + p.Offset/4);
                    for(int i = 0; i < p.displayFaceSize; i++)
                    {
                        Ds.Add(p.display[i] + voffset);
                    }
                    poffset += p.displayFaceSize;
                    voffset += pv.Count;
                }
            }

            // Binds
            Vertices = Vs.ToArray();
            Faces = Ds.ToArray();

            //Console.WriteLine(Vertices.Length + " " + Faces.Length);

            // Bind only once!
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_position);
            GL.BufferData<DisplayVertex>(BufferTarget.ArrayBuffer, (IntPtr)(Vertices.Length * DisplayVertex.Size), Vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo_elements);
            GL.BufferData<int>(BufferTarget.ElementArrayBuffer, (IntPtr)(Faces.Length * sizeof(int)), Faces, BufferUsageHint.StaticDraw);

            DepthSortMeshes();
        }

        public void Render(VBN vbn, Camera camera)
        {
            if (Runtime.renderBoundingBox)
            {
                DrawBoundingBoxes();
            }

            //Prepare Shader
            Shader shader = Runtime.shaders["NUD"];

            if (Runtime.renderType != Runtime.RenderTypes.Shaded)
                shader = Runtime.shaders["NUD_Debug"];
            else
                shader = Runtime.shaders["NUD"];

            GL.UseProgram(shader.programID);

            // Load Bones
            shader.enableAttrib();
            if (vbn != null && !Runtime.useLegacyShaders)
            {
                Matrix4[] f = vbn.getShaderMatrix();

                int maxUniformBlockSize = GL.GetInteger(GetPName.MaxUniformBlockSize);
                int boneCount = vbn.bones.Count;
                int dataSize = boneCount * Vector4.SizeInBytes * 4;

                GL.BindBuffer(BufferTarget.UniformBuffer, ubo_bones);
                GL.BufferData(BufferTarget.UniformBuffer, (IntPtr)(dataSize), IntPtr.Zero, BufferUsageHint.DynamicDraw);
                GL.BindBuffer(BufferTarget.UniformBuffer, 0);

                var blockIndex = GL.GetUniformBlockIndex(shader.programID, "bones");
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, blockIndex, ubo_bones);

                if (f.Length > 0)
                {
                    GL.BindBuffer(BufferTarget.UniformBuffer, ubo_bones);
                    GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, (IntPtr)(f.Length * Vector4.SizeInBytes * 4), f);
                }
            }
            
            Render(shader, camera);
        }

        private void DrawBoundingBoxes()
        {
            GL.UseProgram(0);

            GL.Color4(Color.GhostWhite);
            RenderTools.DrawCube(new Vector3(boundingBox[0], boundingBox[1], boundingBox[2]), boundingBox[3], true);

            GL.Color4(Color.OrangeRed);
            foreach (NUD.Mesh mesh in Nodes)
            {
                if (mesh.Checked)
                    RenderTools.DrawCube(new Vector3(mesh.boundingBox[0], mesh.boundingBox[1], mesh.boundingBox[2]), mesh.boundingBox[3], true);
            }
        }

        public void GenerateBoundingBoxes()
        {
            foreach (Mesh m in Nodes)
                m.generateBoundingBox();

            Vector3 cen1 = new Vector3(0,0,0), cen2 = new Vector3(0,0,0);
            double rad1 = 0, rad2 = 0;

            //Get first vert
            int vertCount = 0;
            Vector3 vert0 = new Vector3();
            foreach (Mesh m in Nodes)
            {
                foreach (Polygon p in m.Nodes)
                {
                    foreach (Vertex v in p.vertices)
                    {
                        vert0 = v.pos;
                        vertCount++;
                        break;
                    }
                    break;
                }
                break;
            }

            if (vertCount == 0) //No vertices
                return;

            //Calculate average and min/max
            Vector3 min = new Vector3(vert0);
            Vector3 max = new Vector3(vert0);
            
            vertCount = 0;
            foreach (Mesh m in Nodes)
            {
                foreach (Polygon p in m.Nodes)
                {
                    foreach (Vertex v in p.vertices)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            min[i] = Math.Min(min[i], v.pos[i]);
                            max[i] = Math.Max(max[i], v.pos[i]);
                        }

                        cen1 += v.pos;
                        vertCount++;
                    }
                }
            }

            cen1 /= vertCount;
            for (int i = 0; i < 3; i++)
                cen2[i] = (min[i]+max[i])/2;

            //Calculate the radius of each
            double dist1, dist2;
            foreach (Mesh m in Nodes)
            {
                foreach (Polygon p in m.Nodes)
                {
                    foreach (Vertex v in p.vertices)
                    {
                        dist1 = ((Vector3)(v.pos - cen1)).Length;
                        if (dist1 > rad1)
                            rad1 = dist1;

                        dist2 = ((Vector3)(v.pos - cen2)).Length;
                        if (dist2 > rad2)
                            rad2 = dist2;
                    }
                }
            }

            //Use the one with the lowest radius
            Vector3 temp;
            double radius;
            if (rad1 < rad2)
            {
                temp = cen1;
                radius = rad1;
            }
            else
            {
                temp = cen2;
                radius = rad2;
            }

            //Set
            for (int i = 0; i < 3; i++)
            {
                boundingBox[i] = temp[i];
            }
            boundingBox[3] = (float)radius;
        }

        public void SetPropertiesFromXMB(XMBFile xmb)
        {
            if (xmb != null)
            {
                foreach (XMBEntry entry in xmb.Entries)
                {
                    if (entry.Name == "model")
                        modelType = xmb.Values[entry.FirstPropertyIndex];

                    if (entry.Children.Count > 0)
                    {
                        foreach (XMBEntry value in entry.Children)
                        {
                            if (xmb.Values.Count >= value.FirstPropertyIndex)
                            {
                                switch (value.Name)
                                {
                                    default:
                                        break;
                                    case "lightset":
                                        int.TryParse(xmb.Values[value.FirstPropertyIndex], out lightSetNumber);
                                        break;
                                    case "directuvtime":
                                        useDirectUVTime = true;
                                        int.TryParse(xmb.Values[value.FirstPropertyIndex], out directUVTime);
                                        break;
                                    case "draw_range":
                                        int.TryParse(xmb.Values[value.FirstPropertyIndex], out drawRange);
                                        break;
                                    case "drawing_order":
                                        int.TryParse(xmb.Values[value.FirstPropertyIndex], out drawingOrder);
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Render(Shader shader, Camera camera)
        {
            // create lists...
            // first draw opaque

            List<Polygon> opaque = new List<Polygon>();
            List<Polygon> trans = new List<Polygon>();
            
            foreach (Mesh m in depthSortedMeshes)
            {
                for (int pol = m.Nodes.Count - 1; pol >= 0; pol--)
                {
                    Polygon p = (Polygon)m.Nodes[m.Nodes.Count - 1 - pol];

                    if (p.materials.Count > 0)
                    {
                        if (p.materials[0].srcFactor != 0 || p.materials[0].dstFactor != 0)
                        {
                            trans.Add(p);
                            continue;
                        }
                    }

                    opaque.Add(p);
                }
            }

            foreach (Polygon p in opaque)
                if (p.Parent != null && ((Mesh)p.Parent).Checked)
                    DrawPolygon(p, shader, camera);

            foreach (Polygon p in trans)
                if (((Mesh)p.Parent).Checked)
                    DrawPolygon(p, shader, camera);

            foreach (Mesh m in Nodes)
            {
                for (int pol = m.Nodes.Count - 1; pol >= 0; pol--)
                {
                    Polygon p = (Polygon)m.Nodes[m.Nodes.Count - 1 - pol];
                    if (((Mesh)p.Parent).Checked)
                    {
                        if (Runtime.renderModelSelection && (((Mesh)p.Parent).IsSelected || p.IsSelected))
                        {
                            DrawPolygon(p, shader, camera, true);
                        }
                    }
                }
            }
        }

        private void DrawPolygon(Polygon p, Shader shader, Camera camera, bool drawSelection = false)
        {
            if (p.faces.Count <= 3)
                return;

            Material material = p.materials[0];

            GL.Uniform1(shader.getAttribute("flags"), material.Flags);
            GL.Uniform1(shader.getAttribute("selectedBoneIndex"), Runtime.selectedBoneIndex);

            // shader uniforms
            ShaderTools.BoolToIntShaderUniform(shader, Runtime.renderVertColor && material.useVertexColor, "renderVertColor");
            SetTextureUniforms(shader, material);
            SetMaterialPropertyUniforms(shader, material);
            SetLightingUniforms(shader);
            SetXMBUniforms(shader, p);
            SetNSCUniform(p, shader);

            // Used for NU_universe material projection coords. 
            GL.Uniform3(shader.getAttribute("cameraPosition"), camera.position);
         
            p.isTransparent = false;
            if (material.srcFactor > 0 || material.dstFactor > 0 || material.AlphaFunc > 0 || material.AlphaTest > 0)
                p.isTransparent = true;

            ShaderTools.BoolToIntShaderUniform(shader, p.isTransparent, "isTransparent");

            // Vertex shader attributes (UVs, skin weights, etc)
            SetVertexAttributes(p, shader);

            // alpha blending
            GL.Enable(EnableCap.Blend);

            BlendingFactorSrc blendSrc = srcFactor.Keys.Contains(material.srcFactor) ? srcFactor[material.srcFactor] : BlendingFactorSrc.SrcAlpha;
            BlendingFactorDest blendDst = dstFactor.Keys.Contains(material.dstFactor) ? dstFactor[material.dstFactor] : BlendingFactorDest.OneMinusSrcAlpha;
            GL.BlendFuncSeparate(blendSrc, blendDst, BlendingFactorSrc.One, BlendingFactorDest.One);
            GL.BlendEquationSeparate(BlendEquationMode.FuncAdd, BlendEquationMode.FuncAdd);
            if (material.srcFactor == 0 && material.dstFactor == 0)
                GL.Disable(EnableCap.Blend);

            // alpha testing
            GL.Enable(EnableCap.AlphaTest);
            if (material.AlphaTest == 0) GL.Disable(EnableCap.AlphaTest);

            float refAlpha = material.RefAlpha / 255f;

            // gequal used because fragcolor.a of 0 is refalpha of 1
            GL.AlphaFunc(AlphaFunction.Gequal, 0.1f);
            switch (material.AlphaFunc)
            {
                case 0x0:
                    GL.AlphaFunc(AlphaFunction.Never, refAlpha);
                    break;
                case 0x4:
                    GL.AlphaFunc(AlphaFunction.Gequal, refAlpha);
                    break;
                case 0x6:
                    GL.AlphaFunc(AlphaFunction.Gequal, refAlpha);
                    break;
            }

            // face culling
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            switch (material.cullMode)
            {
                case 0x0000:
                    GL.Disable(EnableCap.CullFace);
                    break;
                case 0x0404:
                    GL.CullFace(CullFaceMode.Front);
                    break;
                case 0x0405:
                    GL.CullFace(CullFaceMode.Back);
                    break;
                default:
                    GL.Disable(EnableCap.CullFace);
                    break;
            }
            if (p.Checked)
            {
                if ((p.IsSelected || p.Parent.IsSelected) && drawSelection)
                {
                    DrawModelSelection(p, shader);
                }
                else
                {
                    if (Runtime.renderModelWireframe)
                    {
                        DrawModelWireframe(p, shader);
                    }

                    // need this
                    if (Runtime.renderModel)
                    {
                        GL.DrawElements(PrimitiveType.Triangles, p.displayFaceSize, DrawElementsType.UnsignedInt, p.Offset);
                    }
                }
            }
        }

        private void SetLightingUniforms(Shader shader)
        {
            // stage light 1
            int index1 = 0 + (4 * lightSetNumber);
            LightTools.stageLight1 = LightTools.stageDiffuseLightSet[index1];
            int v = 0;
            if (LightTools.stageDiffuseLightSet[index1].enabled) v = 1; else v = 0;
            GL.Uniform1(shader.getAttribute("renderStageLight1"), v);
            GL.Uniform3(shader.getAttribute("stageLight1Color"), LightTools.stageLight1.difR, LightTools.stageLight1.difG, LightTools.stageLight1.difB);

            // stage light 2
            int index2 = 1 + (4 * lightSetNumber);
            LightTools.stageLight2 = LightTools.stageDiffuseLightSet[index2];
            if (LightTools.stageDiffuseLightSet[index2].enabled) v = 1; else v = 0;
            GL.Uniform1(shader.getAttribute("renderStageLight2"), v);
            GL.Uniform3(shader.getAttribute("stageLight2Color"), LightTools.stageLight2.difR, LightTools.stageLight2.difG, LightTools.stageLight2.difB);

            // stage light 3
            int index3 = 2 + (4 * lightSetNumber);
            LightTools.stageLight3 = LightTools.stageDiffuseLightSet[index3];
            if (LightTools.stageDiffuseLightSet[index3].enabled) v = 1; else v = 0;
            GL.Uniform1(shader.getAttribute("renderStageLight3"), v);
            GL.Uniform3(shader.getAttribute("stageLight3Color"), LightTools.stageLight3.difR, LightTools.stageLight3.difG, LightTools.stageLight3.difB);

            // stage light 4
            int index4 = 3 + (4 * lightSetNumber);
            LightTools.stageLight4 = LightTools.stageDiffuseLightSet[index4];
            ShaderTools.BoolToIntShaderUniform(shader, LightTools.stageDiffuseLightSet[index4].enabled, "renderStageLight4");

            GL.Uniform3(shader.getAttribute("stageLight4Color"), LightTools.stageLight4.difR, LightTools.stageLight4.difG, LightTools.stageLight4.difB);

            GL.Uniform3(shader.getAttribute("stageFogColor"), LightTools.stageFogSet[lightSetNumber]);
        }

        private static void SetNSCUniform(Polygon p, Shader shader)
        {
            Matrix4 nscMatrix = Matrix4.Identity;

            // transform object using the bone's transforms
            if (p.Parent != null && p.Parent.Text.Contains("_NSC"))
            {
                int index = ((Mesh)p.Parent).singlebind;
                if (index != -1)
                {
                    // hacky as f
                    nscMatrix = ((ModelContainer)p.Parent.Parent.Parent).VBN.bones[index].transform;
                }
            }

            GL.UniformMatrix4(shader.getAttribute("nscMatrix"), false, ref nscMatrix);
        }
        
        private void SetXMBUniforms(Shader shader, Polygon p)
        {
            if(modelType.Equals("stage"))
                GL.Uniform1(shader.getAttribute("isStage"), 1);
            else
                GL.Uniform1(shader.getAttribute("isStage"), 0);
            bool directUVTimeFlags = (p.materials[0].Flags & 0x00001900) == 0x00001900; // should probably move elsewhere

            if ((useDirectUVTime && directUVTimeFlags))
                GL.Uniform1(shader.getAttribute("useDirectUVTime"), 1);
            else
                GL.Uniform1(shader.getAttribute("useDirectUVTime"), 0);
            GL.Uniform1(shader.getAttribute("lightSet"), lightSetNumber);
        }

        private void SetVertexAttributes(Polygon p, Shader shader)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_position);
            //GL.BufferData<dVertex>(BufferTarget.ArrayBuffer, (IntPtr)(p.vertdata.Length * dVertex.Size), p.vertdata, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.getAttribute("vPosition"), 3, VertexAttribPointerType.Float, false, DisplayVertex.Size, 0);
            GL.VertexAttribPointer(shader.getAttribute("vNormal"), 3, VertexAttribPointerType.Float, false, DisplayVertex.Size, 12);
            GL.VertexAttribPointer(shader.getAttribute("vTangent"), 3, VertexAttribPointerType.Float, false, DisplayVertex.Size, 24);
            GL.VertexAttribPointer(shader.getAttribute("vBiTangent"), 3, VertexAttribPointerType.Float, false, DisplayVertex.Size, 36);
            GL.VertexAttribPointer(shader.getAttribute("vUV"), 2, VertexAttribPointerType.Float, false, DisplayVertex.Size, 48);
            GL.VertexAttribPointer(shader.getAttribute("vColor"), 4, VertexAttribPointerType.Float, false, DisplayVertex.Size, 56);
            GL.VertexAttribIPointer(shader.getAttribute("vBone"), 4, VertexAttribIntegerType.Int, DisplayVertex.Size, new IntPtr(72));
            GL.VertexAttribPointer(shader.getAttribute("vWeight"), 4, VertexAttribPointerType.Float, false, DisplayVertex.Size, 88);
            GL.VertexAttribPointer(shader.getAttribute("vUV2"), 2, VertexAttribPointerType.Float, false, DisplayVertex.Size, 104);
            GL.VertexAttribPointer(shader.getAttribute("vUV3"), 2, VertexAttribPointerType.Float, false, DisplayVertex.Size, 112);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo_elements);
            //GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(p.display.Length * sizeof(int)), p.display, BufferUsageHint.StaticDraw);
            //GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        private static void DrawModelWireframe(Polygon p, Shader shader)
        {
            // use vertex color for wireframe color
            GL.Uniform1(shader.getAttribute("colorOverride"), 1);
            GL.PolygonMode(MaterialFace.Front, PolygonMode.Line);
            GL.Enable(EnableCap.LineSmooth);
            GL.LineWidth(1f);
            GL.DrawElements(PrimitiveType.Triangles, p.displayFaceSize, DrawElementsType.UnsignedInt, p.Offset);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.Uniform1(shader.getAttribute("colorOverride"), 0);
        }

        private static void DrawModelSelection(Polygon p, Shader shader)
        {
            GL.Enable(EnableCap.StencilTest);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
            GL.Disable(EnableCap.DepthTest);

            bool[] cwm = new bool[4];
            GL.GetBoolean(GetIndexedPName.ColorWritemask, 4, cwm);
            GL.ColorMask(false, false, false, false);

            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
            GL.StencilMask(0xFF);

            GL.DrawElements(PrimitiveType.Triangles, p.displayFaceSize, DrawElementsType.UnsignedInt, p.Offset);

            GL.ColorMask(cwm[0], cwm[1], cwm[2], cwm[3]);

            GL.StencilFunc(StencilFunction.Notequal, 1, 0xFF);
            GL.StencilMask(0x00);

            // use vertex color for model selection color
            GL.Uniform1(shader.getAttribute("colorOverride"), 1);

            GL.PolygonMode(MaterialFace.Front, PolygonMode.Line);
            GL.LineWidth(2.0f);
            GL.DrawElements(PrimitiveType.Triangles, p.displayFaceSize, DrawElementsType.UnsignedInt, p.Offset);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.Uniform1(shader.getAttribute("colorOverride"), 0);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
        }

        private static void SetMaterialPropertyUniforms(Shader shader, Material mat)
        {
            // UV samplers
            MatPropertyShaderUniform(shader, mat, "NU_colorSamplerUV",   1, 1, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_colorSampler2UV",  1, 1, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_colorSampler3UV",  1, 1, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_normalSamplerAUV", 1, 1, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_normalSamplerBUV", 1, 1, 0, 0);

            // color properties
            MatPropertyShaderUniform(shader, mat, "NU_aoMinGain",       0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_colorGain",       1, 1, 1, 1);
            MatPropertyShaderUniform(shader, mat, "NU_finalColorGain",  1, 1, 1, 1);
            MatPropertyShaderUniform(shader, mat, "NU_finalColorGain2", 1, 1, 1, 1);
            MatPropertyShaderUniform(shader, mat, "NU_finalColorGain3", 1, 1, 1, 1);
            MatPropertyShaderUniform(shader, mat, "NU_colorOffset",     0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_diffuseColor",    1, 1, 1, 0.5f);
            MatPropertyShaderUniform(shader, mat, "NU_characterColor",  1, 1, 1, 1);

            MatPropertyShaderUniform(shader, mat, "NU_specularColor",     0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_specularColorGain", 1, 1, 1, 1);
            MatPropertyShaderUniform(shader, mat, "NU_specularParams",    0, 0, 0, 0);

            MatPropertyShaderUniform(shader, mat, "NU_fresnelColor",  0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_fresnelParams", 0, 0, 0, 0);

            MatPropertyShaderUniform(shader, mat, "NU_reflectionColor",  0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_reflectionParams", 0, 0, 0, 0);

            MatPropertyShaderUniform(shader, mat, "NU_fogColor",  0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_fogParams", 0, 1, 0, 0);

            MatPropertyShaderUniform(shader, mat, "NU_softLightingParams",    0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_customSoftLightParams", 0, 0, 0, 0);

            // misc properties
            MatPropertyShaderUniform(shader, mat, "NU_normalParams",           1, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_zOffset",                0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_angleFadeParams",        0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_dualNormalScrollParams", 0, 0, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_alphaBlendParams",       0, 0, 0, 0);

            // effect materials
            MatPropertyShaderUniform(shader, mat, "NU_effCombinerColor0", 1, 1, 1, 1);
            MatPropertyShaderUniform(shader, mat, "NU_effCombinerColor1", 1, 1, 1, 1);
            MatPropertyShaderUniform(shader, mat, "NU_effColorGain",      1, 1, 1, 1);
            MatPropertyShaderUniform(shader, mat, "NU_effScaleUV",        1, 1, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_effTransUV",        1, 1, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_effMaxUV",          1, 1, 0, 0);
            MatPropertyShaderUniform(shader, mat, "NU_effUniverseParam",  1, 0, 0, 0);

            // Create some conditionals rather than using different shaders.
            HasMatPropertyShaderUniform(shader, mat, "NU_softLightingParams",     "hasSoftLight");
            HasMatPropertyShaderUniform(shader, mat, "NU_customSoftLightParams",  "hasCustomSoftLight");
            HasMatPropertyShaderUniform(shader, mat, "NU_specularParams",         "hasSpecularParams");
            HasMatPropertyShaderUniform(shader, mat, "NU_dualNormalScrollParams", "hasDualNormal");
            HasMatPropertyShaderUniform(shader, mat, "NU_normalSamplerAUV",       "hasNrmSamplerAUV");
            HasMatPropertyShaderUniform(shader, mat, "NU_normalSamplerBUV",       "hasNrmSamplerBUV");
            HasMatPropertyShaderUniform(shader, mat, "NU_finalColorGain",         "hasFinalColorGain");
            HasMatPropertyShaderUniform(shader, mat, "NU_effUniverseParam",       "hasUniverseParam");
        }

        private static void SetTextureUniforms(Shader shader, Material mat)
        {
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasDiffuse,         "hasDif");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasDiffuse2,        "hasDif2");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasDiffuse3,        "hasDif3");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasStageMap,        "hasStage");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasCubeMap,         "hasCube");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasAoMap,           "hasAo");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasNormalMap,       "hasNrm");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasRamp,            "hasRamp");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasDummyRamp,       "hasDummyRamp");
            ShaderTools.BoolToIntShaderUniform(shader, mat.useColorGainOffset, "hasColorGainOffset");
            ShaderTools.BoolToIntShaderUniform(shader, mat.useDiffuseBlend,    "useDiffuseBlend");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasSphereMap,       "hasSphereMap");
            ShaderTools.BoolToIntShaderUniform(shader, mat.hasBayoHair,        "hasBayoHair");
            ShaderTools.BoolToIntShaderUniform(shader, mat.useReflectionMask,  "useDifRefMask");
            ShaderTools.BoolToIntShaderUniform(shader, mat.softLightBrighten,  "softLightBrighten");
                                              
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, RenderTools.defaultTex);

            GL.ActiveTexture(TextureUnit.Texture10);
            GL.BindTexture(TextureTarget.Texture2D, RenderTools.uvTestPattern);
            GL.Uniform1(shader.getAttribute("UVTestPattern"), 10);

            GL.ActiveTexture(TextureUnit.Texture11);
            GL.BindTexture(TextureTarget.Texture2D, RenderTools.boneWeightGradient);
            GL.Uniform1(shader.getAttribute("weightRamp1"), 11);

            GL.ActiveTexture(TextureUnit.Texture12);
            GL.BindTexture(TextureTarget.Texture2D, RenderTools.boneWeightGradient2);
            GL.Uniform1(shader.getAttribute("weightRamp2"), 12);

            GL.Uniform1(shader.getAttribute("dif"), 0);
            GL.Uniform1(shader.getAttribute("dif2"), 0);
            GL.Uniform1(shader.getAttribute("normalMap"), 0);
            GL.Uniform1(shader.getAttribute("cube"), 2);
            GL.Uniform1(shader.getAttribute("stagecube"), 2);
            GL.Uniform1(shader.getAttribute("spheremap"), 0);
            GL.Uniform1(shader.getAttribute("ao"), 0);
            GL.Uniform1(shader.getAttribute("ramp"), 0);

            int texid = 0;

            if (mat.hasDiffuse && texid < mat.textures.Count)
            {
                int hash = mat.textures[texid].hash;
                if (mat.displayTexId != -1) hash = mat.displayTexId;
                GL.Uniform1(shader.getAttribute("dif"), BindTexture(mat.textures[texid], hash, texid));
                mat.diffuse1ID = mat.textures[texid].hash;
                texid++;
            }

            // The order of the textures is critical. 
            TextureUniform(shader, mat, mat.hasSphereMap, "spheremap", ref texid, ref mat.sphereMapID);
            TextureUniform(shader, mat, mat.hasDiffuse2,  "dif2",      ref texid, ref mat.diffuse2ID);
            TextureUniform(shader, mat, mat.hasDiffuse3,  "dif3",      ref texid, ref mat.diffuse3ID);
            TextureUniform(shader, mat, mat.hasStageMap,  "stagecube", ref texid, ref mat.stageMapID);
            TextureUniform(shader, mat, mat.hasCubeMap,   "cube",      ref texid, ref mat.cubeMapID);
            TextureUniform(shader, mat, mat.hasAoMap,     "ao",        ref texid, ref mat.aoMapID);
            TextureUniform(shader, mat, mat.hasNormalMap, "normalMap", ref texid, ref mat.normalID);
            TextureUniform(shader, mat, mat.hasRamp,      "ramp",      ref texid, ref mat.rampID);
            TextureUniform(shader, mat, mat.hasDummyRamp, "dummyRamp", ref texid, ref mat.dummyRampID);
        }

        private static void MatPropertyShaderUniform(Shader shader, Material mat, string propertyName, float default1,
            float default2, float default3, float default4)
        {
            float[] values;
            mat.entries.TryGetValue(propertyName, out values);
            if (mat.anims.ContainsKey(propertyName))
            {
                values = mat.anims[propertyName];
            }
            if (values == null)
                values = new float[] { default1, default2, default3, default4 };
            string uniformName = propertyName.Substring(3); // remove the NU_ from name

            try
            {
                GL.Uniform4(shader.getAttribute(uniformName), values[0], values[1], values[2], values[3]);
            }
            catch (System.IndexOutOfRangeException)
            {
                // something went wrong reading mat data somewhere...
                // some other part of the code will probably also fail
            }
        }

        private static void TextureUniform(Shader shader, Material mat, bool hasTex, string name, ref int texid, ref int matTexId)
        {
            // Bind the texture and create the uniform if the material has the right textures and flags. 
            if (hasTex && texid < mat.textures.Count)
            {
                GL.Uniform1(shader.getAttribute(name), BindTexture(mat.textures[texid], mat.textures[texid].hash, texid));
                matTexId = mat.textures[texid].hash;
                texid++;
            }
        }

        public void MakeMetal(int newDifTexId, int newCubeTexId, float[] minGain, float[] refColor, float[] fresParams, float[] fresColor, bool preserveDiffuse = false, bool preserveNrmMap = true)
        {
            foreach (Mesh mesh in Nodes)
            {
                foreach (Polygon poly in mesh.Nodes)
                {
                    foreach (Material mat in poly.materials)
                    {
                        mat.MakeMetal(newDifTexId, newCubeTexId, minGain, refColor, fresParams, fresColor, preserveDiffuse, preserveNrmMap);
                    }
                }
            }
        }

        private static void HasMatPropertyShaderUniform(Shader shader, Material mat, string propertyName, string uniformName)
        {
            float[] values;
            mat.entries.TryGetValue(propertyName, out values);
            if (mat.anims.ContainsKey(propertyName))
                values = mat.anims[propertyName];

            int hasParam = 1;
            if (values == null)
                hasParam = 0;

            GL.Uniform1(shader.getAttribute(uniformName), hasParam);
        }

        
        public void RenderShadow(Matrix4 lightMatrix, Matrix4 view, Matrix4 modelMatrix)
        {
            // simple passthrough vertex render for shadow mapping
            Shader shader = Runtime.shaders["Shadow"];

            GL.UseProgram(shader.programID);

            GL.UniformMatrix4(shader.getAttribute("lightSpaceMatrix"), false, ref lightMatrix);
            GL.UniformMatrix4(shader.getAttribute("eyeview"), false, ref view);
            GL.UniformMatrix4(shader.getAttribute("modelMatrix"), false, ref modelMatrix);

            shader.enableAttrib();
            foreach(Mesh m in Nodes)
            {
                foreach(Polygon p in m.Nodes)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_position);
                    //GL.BufferData<dVertex>(BufferTarget.ArrayBuffer, (IntPtr)(p.vertdata.Length * dVertex.Size), p.vertdata, BufferUsageHint.StaticDraw);
                    GL.VertexAttribPointer(shader.getAttribute("vPosition"), 3, VertexAttribPointerType.Float, false, DisplayVertex.Size, 0);

                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo_elements);
                    //GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(p.display.Length * sizeof(int)), p.display, BufferUsageHint.StaticDraw);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

                    GL.DrawElements(PrimitiveType.Triangles, p.displayFaceSize, DrawElementsType.UnsignedInt, p.Offset);
                }
            }
            shader.disableAttrib();

            GL.UseProgram(0);
        }

        public void DrawPoints(Camera camera, VBN vbn, PrimitiveType type)
        {
            Shader shader = Runtime.shaders["Point"];
            GL.UseProgram(shader.programID);
            Matrix4 mat = camera.mvpMatrix;
            GL.UniformMatrix4(shader.getAttribute("mvpMatrix"), false, ref mat);

            if (type == PrimitiveType.Points)
            {
                GL.Uniform3(shader.getAttribute("col1"), 0f, 0f, 1f);
                GL.Uniform3(shader.getAttribute("col2"), 1f, 1f, 0f);
            }
            if (type == PrimitiveType.Triangles)
            {
                GL.Uniform3(shader.getAttribute("col1"), 0.5f, 0.5f, 0.5f);
                GL.Uniform3(shader.getAttribute("col2"), 1f, 0f, 0f);
            }

            shader.enableAttrib();
            foreach (Mesh m in Nodes)
            {
                foreach (Polygon p in m.Nodes)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_position);
                    //GL.BufferData<dVertex>(BufferTarget.ArrayBuffer, (IntPtr)(p.vertdata.Length * dVertex.Size), p.vertdata, BufferUsageHint.StaticDraw);
                    GL.VertexAttribPointer(shader.getAttribute("vPosition"), 3, VertexAttribPointerType.Float, false, DisplayVertex.Size, 0);
                    GL.VertexAttribPointer(shader.getAttribute("vBone"), 4, VertexAttribPointerType.Float, false, DisplayVertex.Size, 72);
                    GL.VertexAttribPointer(shader.getAttribute("vWeight"), 4, VertexAttribPointerType.Float, false, DisplayVertex.Size, 88);

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_select);
                    if (p.selectedVerts == null) return;
                    GL.BufferData<int>(BufferTarget.ArrayBuffer, (IntPtr)(p.selectedVerts.Length * sizeof(int)), p.selectedVerts, BufferUsageHint.StaticDraw);
                    GL.VertexAttribPointer(shader.getAttribute("vSelected"), 1, VertexAttribPointerType.Int, false, sizeof(int), 0);

                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo_elements);
                    //GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(p.display.Length * sizeof(int)), p.display, BufferUsageHint.StaticDraw);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                    
                    GL.PointSize(6f);
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                    GL.DrawElements(type, p.displayFaceSize, DrawElementsType.UnsignedInt, 0);
                }
            }
            shader.disableAttrib();
        }

        Dictionary<int, BlendingFactorDest> dstFactor = new Dictionary<int, BlendingFactorDest>(){
                    { 0x01, BlendingFactorDest.OneMinusSrcAlpha},
                    { 0x02, BlendingFactorDest.One},
                    { 0x03, BlendingFactorDest.OneMinusSrcAlpha},
                    { 0x04, BlendingFactorDest.OneMinusConstantAlpha},
                    { 0x05, BlendingFactorDest.ConstantAlpha},
        };

        static Dictionary<int, BlendingFactorSrc> srcFactor = new Dictionary<int, BlendingFactorSrc>(){
                    { 0x01, BlendingFactorSrc.SrcAlpha},
                    { 0x02, BlendingFactorSrc.SrcAlpha},
                    { 0x03, BlendingFactorSrc.SrcAlpha},
                    { 0x04, BlendingFactorSrc.SrcAlpha},
                    { 0x0a, BlendingFactorSrc.Zero}
        };

        static Dictionary<int, TextureWrapMode> wrapmode = new Dictionary<int, TextureWrapMode>(){
                    { 0x01, TextureWrapMode.Repeat},
                    { 0x02, TextureWrapMode.MirroredRepeat},
                    { 0x03, TextureWrapMode.ClampToEdge}
        };

        static Dictionary<int, TextureMinFilter> minfilter = new Dictionary<int, TextureMinFilter>(){
                    { 0x00, TextureMinFilter.LinearMipmapLinear},
                    { 0x01, TextureMinFilter.Nearest},
                    { 0x02, TextureMinFilter.Linear},
                    { 0x03, TextureMinFilter.NearestMipmapLinear},
        };

        static Dictionary<int, TextureMagFilter> magfilter = new Dictionary<int, TextureMagFilter>(){
                    { 0x00, TextureMagFilter.Linear},
                    { 0x01, TextureMagFilter.Nearest},
                    { 0x02, TextureMagFilter.Linear}
        };

        public static int BindTexture(NUD.MatTexture tex, int hash, int loc)
        {
            if (hash == (int) DummyTextures.StageMapLow)
            {
                GL.ActiveTexture(TextureUnit.Texture20 + loc);
                GL.BindTexture(TextureTarget.TextureCubeMap, RenderTools.cubeMapLow);
                return 20 + loc;

            }
            if (hash == (int)DummyTextures.StageMapHigh)
            {
                GL.ActiveTexture(TextureUnit.Texture20 + loc);
                GL.BindTexture(TextureTarget.TextureCubeMap, RenderTools.cubeMapHigh);
                return 20 + loc;

            }
            if (hash == (int)DummyTextures.DummyRamp)
            {
                GL.ActiveTexture(TextureUnit.Texture20 + loc);
                GL.BindTexture(TextureTarget.Texture2D, RenderTools.dummyRamp);
                return 20 + loc;
            }
            if (hash == (int)DummyTextures.PokemonStadium)
            {
                GL.ActiveTexture(TextureUnit.Texture20 + loc);
                GL.BindTexture(TextureTarget.Texture2D, RenderTools.pokemonStadiumDummyTex);
                return 20 + loc;
            }
            if (hash == (int)DummyTextures.PunchOut)
            {
                GL.ActiveTexture(TextureUnit.Texture20 + loc);
                GL.BindTexture(TextureTarget.Texture2D, RenderTools.punchOutDummyTex);
                return 20 + loc;
            }
            if (hash == (int)DummyTextures.ShadowMap)
            {
                GL.ActiveTexture(TextureUnit.Texture20 + loc);
                GL.BindTexture(TextureTarget.Texture2D, RenderTools.shadowMapDummyTex);
                return 20 + loc;
            }
            GL.ActiveTexture(TextureUnit.Texture3 + loc);
            GL.BindTexture(TextureTarget.Texture2D, RenderTools.defaultTex);

            int texid;
            bool success;
            foreach (NUT nut in Runtime.TextureContainers)
            {
                success = nut.draw.TryGetValue(hash, out texid);

                if (success)
                {
                    GL.BindTexture(TextureTarget.Texture2D, texid);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapmode[tex.wrapModeS]);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapmode[tex.wrapModeT]);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minfilter[tex.minFilter]);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magfilter[tex.magFilter]);
                    GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt, 0.0f);
                    if(tex.mipDetail == 0x4 || tex.mipDetail == 0x6)
                        GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt, 4.0f);
                    break;
                }
            }

            return 3 + loc;
        }

        #endregion

        #region MTA
        public void clearMTA()
        {
            foreach (Mesh me in Nodes)
            {
                foreach (Polygon p in me.Nodes)
                {
                    foreach (Material ma in p.materials)
                    {
                        ma.anims.Clear();
                    }
                }
            }
        }

        public void applyMTA(MTA m, int frame)
        {
            foreach (MatEntry mat in m.matEntries)
            {
                foreach (Mesh me in Nodes)
                {
                    foreach(Polygon p in me.Nodes)
                    {
                        foreach (Material ma in p.materials)
                        {
                            float[] matHashFloat;
                            ma.entries.TryGetValue("NU_materialHash", out matHashFloat);
                            if (matHashFloat != null) {

                                byte[] bytes = new byte[4];
                                Buffer.BlockCopy(matHashFloat, 0, bytes, 0, 4);
                                int matHash = BitConverter.ToInt32(bytes, 0);

                                int frm = (int)((frame * 60 / m.frameRate) % (m.numFrames));

                                if (matHash == mat.matHash || matHash == mat.matHash2)
                                {
                                    if (mat.hasPat)
                                    {
                                        ma.displayTexId = mat.pat0.getTexId(frm);
                                    }

                                    foreach(MatData md in mat.properties)
                                    {
                                        if (md.frames.Count > 0 && md.frames.Count > frm)
                                        {
                                            if (ma.anims.ContainsKey(md.name))
                                                ma.anims[md.name] = md.frames[frm].values;
                                            else
                                                    ma.anims.Add(md.name, md.frames[frm].values);
                                        }
                                            
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (VisEntry e in m.visEntries)
            {
                int state = e.getState(frame);
                foreach (Mesh me in Nodes)
                {
                    if (me.Text.Equals(e.name))
                    {
                        if (state == 0)
                        {
                            me.Checked = false;
                        }
                        else
                        {
                            me.Checked = true;
                        }
                        break;
                    }
                }
            }
        }
        #endregion

        #region Reading
        //------------------------------------------------------------------------------------------------------------------------
        /*
         * Reads the contents of the nud file into this class
         */
        //------------------------------------------------------------------------------------------------------------------------
        // HELPERS FOR READING
        private struct _s_Object
        {
            public int id;
            //public int polynamestart;
            public int singlebind;
            public int polyamt;
            public int positionb;
            public string name;
        }

        public struct _s_Poly
        {
            public int polyStart;
            public int vertStart;
            public int verAddStart;
            public int vertamt;
            public int vertSize;
            public int UVSize;
            public int polyamt;
            public int polsize;
            public int polflag;
            public int texprop1;
            public int texprop2;
            public int texprop3;
            public int texprop4;
        }

        public override void Read(string filename)
        {
            FileData d = new FileData(filename);
            d.Endian = Endianness.Big;
            d.seek(0);

            // read header
            string magic = d.readString(0, 4);

            if (magic.Equals("NDWD"))
                d.Endian = Endianness.Little;

            Endian = d.Endian;

            d.seek(0xA);
            int polysets = d.readShort();
            boneCount = d.readShort();
            d.skip(2);  // somethingsets
            int polyClumpStart = d.readInt() + 0x30;
            int polyClumpSize = d.readInt();
            int vertClumpStart = polyClumpStart + polyClumpSize;
            int vertClumpSize = d.readInt();
            int vertaddClumpStart = vertClumpStart + vertClumpSize;
            int vertaddClumpSize = d.readInt();
            int nameStart = vertaddClumpStart + vertaddClumpSize;
            boundingBox[0] = d.readFloat();
            boundingBox[1] = d.readFloat();
            boundingBox[2] = d.readFloat();
            boundingBox[3] = d.readFloat();

            // object descriptors

            _s_Object[] obj = new _s_Object[polysets];
            List<float[]> unknown = new List<float[]>();
            int[] boneflags = new int[polysets];
            for (int i = 0; i < polysets; i++)
            {
                float[] un = new float[8];
                un[0] = d.readFloat();
                un[1] = d.readFloat();
                un[2] = d.readFloat();
                un[3] = d.readFloat();
                un[4] = d.readFloat();
                un[5] = d.readFloat();
                un[6] = d.readFloat();
                un[7] = d.readFloat();
                unknown.Add(un);
                int temp = d.pos() + 4;
                d.seek(nameStart + d.readInt());
                obj[i].name = (d.readString());
                // read name string
                d.seek(temp);
                boneflags[i] = d.readInt();
                obj[i].singlebind = d.readShort();
                obj[i].polyamt = d.readShort();
                obj[i].positionb = d.readInt();
            }

            // reading polygon data
            int mi = 0;
            foreach (var o in obj)
            {
                Mesh m = new Mesh();
                m.Text = o.name;
                Nodes.Add(m);
                m.boneflag = boneflags[mi];
                m.singlebind = (short)o.singlebind;
                m.boundingBox = unknown[mi++];

                for (int i = 0; i < o.polyamt; i++)
                {
                    _s_Poly p = new _s_Poly();

                    p.polyStart = d.readInt() + polyClumpStart;
                    p.vertStart = d.readInt() + vertClumpStart;
                    p.verAddStart = d.readInt() + vertaddClumpStart;
                    p.vertamt = d.readShort();
                    p.vertSize = d.readByte();
                    p.UVSize = d.readByte();
                    p.texprop1 = d.readInt();
                    p.texprop2 = d.readInt();
                    p.texprop3 = d.readInt();
                    p.texprop4 = d.readInt();
                    p.polyamt = d.readShort();
                    p.polsize = d.readByte();
                    p.polflag = d.readByte();
                    d.skip(0xC);

                    int temp = d.pos();

                    // read vertex
                    Polygon pol = readVertex(d, p, o);
                    m.Nodes.Add(pol);

                    pol.materials = readMaterial(d, p, nameStart);

                    d.seek(temp);
                }
            }
        }

        //VERTEX TYPES----------------------------------------------------------------------------------------

        public static List<Material> readMaterial(FileData d, _s_Poly p, int nameOffset)
        {
            int propoff = p.texprop1;
            List<Material> mats = new List<Material>();

            while (propoff != 0)
            {
                d.seek(propoff);

                Material m = new Material();
                mats.Add(m);

                m.Flags = (uint)d.readInt();
                d.skip(4);

                
                m.srcFactor = d.readShort();
                int propCount = d.readShort();
                m.dstFactor = d.readShort();
                m.AlphaTest = d.readByte();
                m.AlphaFunc = d.readByte();

                d.skip(1); // unknown
                m.RefAlpha = d.readByte();
                m.cullMode = d.readShort();
                d.skip(4); // padding
                m.unkownWater = d.readInt();
                m.zBufferOffset = d.readInt();

                for (int i = 0; i < propCount; i++)
                {
                    MatTexture tex = new MatTexture();
                    tex.hash = d.readInt();
                    d.skip(6); // padding?
                    tex.mapMode = d.readShort();
                    tex.wrapModeS = d.readByte();
                    tex.wrapModeT = d.readByte();
                    tex.minFilter = d.readByte();
                    tex.magFilter = d.readByte();
                    tex.mipDetail = d.readByte();
                    tex.unknown = d.readByte();
                    d.skip(6);
                    m.textures.Add(tex);
                }

                int head = 0x20;

                if(d.Endian != Endianness.Little)
                while (head != 0)
                {
                    head = d.readInt();
                    int nameStart = d.readInt();

                    string name = d.readString(nameOffset + nameStart, -1);

                    int pos = d.pos();
                    int c = d.readInt();
                    d.skip(4);
                    float[] values = new float[c];
                    for (int i = 0; i < c; i++)
                    {
                        values[i] = d.readFloat();
                    }

                    // material properties should always have 4 values
                    if (values.Length < 4)
                    {
                        float[] newValues = { 0, 0, 0, 0 };
                        for (int i = 0; i < values.Length; i++)
                        {
                            // fill in existing values and use 0 for remaining values
                            newValues[i] = values[i];
                        }

                        m.entries.Add(name, newValues);
                    }
                    else
                        m.entries.Add(name, values);

                    d.seek(pos);

                    if (head == 0)
                        d.skip(0x20 - 8);
                    else
                        d.skip(head - 8);
                }

                if (propoff == p.texprop1)
                    propoff = p.texprop2;
                else
                    if (propoff == p.texprop2)
                        propoff = p.texprop3;
                    else
                        if (propoff == p.texprop3)
                            propoff = p.texprop4;
            }

            return mats;
        }

        private static Polygon readVertex(FileData d, _s_Poly p, _s_Object o)
        {
            Polygon m = new Polygon();
            m.vertSize = p.vertSize;
            m.UVSize = p.UVSize;
            m.polflag = p.polflag;
            m.strip = p.polsize;

            readVertex(d, p, o, m);

            // faces
            d.seek(p.polyStart);

            for (int x = 0; x < p.polyamt; x++)
            {
                m.faces.Add(d.readShort());
            }

            return m;
        }

        //VERTEX TYPES----------------------------------------------------------------------------------------
        private static void readUV(FileData d, _s_Poly p, _s_Object o, Polygon m, Vertex[] v)
        {
            int uvCount = (p.UVSize >> 4);
            int uvType = (p.UVSize) & 0xF;

            for (int i = 0; i < p.vertamt; i++)
            {
                v[i] = new Vertex();
                if (uvType == 0x0)
                {
                    for (int j = 0; j < uvCount; j++)
                        v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                }
                else
                    if (uvType == 0x2)
                    {
                        v[i].col = new Vector4(d.readByte(), d.readByte(), d.readByte(), d.readByte());
                        for (int j = 0; j < uvCount; j++)
                            v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                }
                else
                    if (uvType == 0x4)
                {
                    v[i].col = new Vector4(d.readHalfFloat() * 0xFF, d.readHalfFloat() * 0xFF, d.readHalfFloat() * 0xFF, d.readHalfFloat() * 0xFF);
                    for (int j = 0; j < uvCount; j++)
                        v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                }
                else
                        throw new NotImplementedException("UV type not supported " + uvType);
            }
        }

        private static void readVertex(FileData d, _s_Poly p, _s_Object o, Polygon m)
        {
            int weight = p.vertSize >> 4;
            int nrm = p.vertSize & 0xF;

            Vertex[] v = new Vertex[p.vertamt];

            d.seek(p.vertStart);

            if (weight > 0)
            {
                readUV(d, p, o, m, v);
                d.seek(p.verAddStart);
            }
            else
            {
                for (int i = 0; i < p.vertamt; i++)
                {
                    v[i] = new Vertex();
                }
            }

            for (int i = 0; i < p.vertamt; i++)
            {
                if (nrm != 8)
                {
                    v[i].pos.X = d.readFloat();
                    v[i].pos.Y = d.readFloat();
                    v[i].pos.Z = d.readFloat();
                }

                if (nrm == 1)
                {
                    v[i].nrm.X = d.readFloat();
                    v[i].nrm.Y = d.readFloat();
                    v[i].nrm.Z = d.readFloat();
                    d.skip(4); // n1?
                    d.skip(4); // r1?
                } else if (nrm == 2)
                {
                    v[i].nrm.X = d.readFloat();
                    v[i].nrm.Y = d.readFloat();
                    v[i].nrm.Z = d.readFloat();
                    d.skip(4); // n1?
                    d.skip(12); // r1?
                    d.skip(12); // r1?
                    d.skip(12); // r1?
                } else if (nrm == 3)
                {
                    d.skip(4); 
                    v[i].nrm.X = d.readFloat();
                    v[i].nrm.Y = d.readFloat();
                    v[i].nrm.Z = d.readFloat();
                    d.skip(4); 
                    v[i].bitan.X = d.readFloat();
                    v[i].bitan.Y = d.readFloat();
                    v[i].bitan.Z = d.readFloat();
                    v[i].bitan.W = d.readFloat();
                    v[i].tan.X = d.readFloat();
                    v[i].tan.Y = d.readFloat();
                    v[i].tan.Z = d.readFloat();
                    v[i].tan.W = d.readFloat();
                }
                else if (nrm == 6)
                {
                    v[i].nrm.X = d.readHalfFloat();
                    v[i].nrm.Y = d.readHalfFloat();
                    v[i].nrm.Z = d.readHalfFloat();
                    d.skip(2); // n1?
                } else if (nrm == 7)
                {
                    v[i].nrm.X = d.readHalfFloat();
                    v[i].nrm.Y = d.readHalfFloat();
                    v[i].nrm.Z = d.readHalfFloat();
                    d.skip(2); // n1?
                    v[i].bitan.X = d.readHalfFloat();
                    v[i].bitan.Y = d.readHalfFloat();
                    v[i].bitan.Z = d.readHalfFloat();
                    v[i].bitan.W = d.readHalfFloat();
                    v[i].tan.X = d.readHalfFloat();
                    v[i].tan.Y = d.readHalfFloat();
                    v[i].tan.Z = d.readHalfFloat();
                    v[i].tan.W = d.readHalfFloat();
                } else
                    d.skip(4);

                if (weight == 0)
                {
                    if (p.UVSize >= 18)
                    {
                        v[i].col.X = (int)d.readByte();
                        v[i].col.Y = (int)d.readByte();
                        v[i].col.Z = (int)d.readByte();
                        v[i].col.W = (int)d.readByte();
                        //v.a = (int) (d.readByte());
                    }


                    if((p.UVSize >> 4) == 1)
                        v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                    else if((p.UVSize >> 4) == 2)
                    {
                        v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                        v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                    }
                    else if ((p.UVSize >> 4) == 3)
                    {
                        v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                        v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                        v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                    }
                    else
                    {
                        for (int j = 0; j < (p.UVSize >> 4); j++)
                            v[i].uv.Add(new Vector2(d.readHalfFloat(), d.readHalfFloat()));
                    }
                }

                if (weight == 1)
                {
                    v[i].node.Add(d.readInt());
                    v[i].node.Add(d.readInt());
                    v[i].node.Add(d.readInt());
                    v[i].node.Add(d.readInt());
                    v[i].weight.Add(d.readFloat());
                    v[i].weight.Add(d.readFloat());
                    v[i].weight.Add(d.readFloat());
                    v[i].weight.Add(d.readFloat());
                }
                else if (weight == 2)
                {
                    v[i].node.Add(d.readShort());
                    v[i].node.Add(d.readShort());
                    v[i].node.Add(d.readShort());
                    v[i].node.Add(d.readShort());
                    v[i].weight.Add(d.readHalfFloat());
                    v[i].weight.Add(d.readHalfFloat());
                    v[i].weight.Add(d.readHalfFloat());
                    v[i].weight.Add(d.readHalfFloat());
                }
                else if (weight == 4)
                {
                    v[i].node.Add(d.readByte());
                    v[i].node.Add(d.readByte());
                    v[i].node.Add(d.readByte());
                    v[i].node.Add(d.readByte());
                    v[i].weight.Add((float)d.readByte() / 255f);
                    v[i].weight.Add((float)d.readByte() / 255f);
                    v[i].weight.Add((float)d.readByte() / 255f);
                    v[i].weight.Add((float)d.readByte() / 255f);
                }
                else if (weight == 0)
                {
                    v[i].node.Add((short)o.singlebind);
                    v[i].weight.Add(1);
                }
            }

            foreach (Vertex vi in v)
                m.vertices.Add(vi);
        }
        #endregion

        #region Building
        public override byte[] Rebuild()
        {
            FileOutput d = new FileOutput(); // data
            d.Endian = Endianness.Big;

            //GenerateBoundingBoxes();

            // mesh optimize

            d.writeString("NDP3");
            d.writeInt(0); //FileSize
            d.writeShort(0x200); //  version num
            d.writeShort(Nodes.Count); // polysets

            boneCount = ((ModelContainer)Parent).VBN.bones.Count;
            /*foreach (ModelContainer con in Runtime.ModelContainers)
            {
                if (con.NUD == this && con.VBN!=null)
                    boneCount = con.VBN.bones.Count;   
            }*/

            d.writeShort(boneCount == 0 ? 0 : 2); // type
            d.writeShort(boneCount == 0 ? boneCount : boneCount - 1); // Number of bones

            d.writeInt(0); // polyClump start
            d.writeInt(0); // polyClump size
            d.writeInt(0); // vertexClumpsize
            d.writeInt(0); // vertexaddclump size
            
            d.writeFloat(boundingBox[0]);
            d.writeFloat(boundingBox[1]);
            d.writeFloat(boundingBox[2]);
            d.writeFloat(boundingBox[3]);

            // other sections....
            FileOutput obj = new FileOutput(); // data
            obj.Endian = Endianness.Big;
            FileOutput tex = new FileOutput(); // data
            tex.Endian = Endianness.Big;

            FileOutput poly = new FileOutput(); // data
            poly.Endian = Endianness.Big;
            FileOutput vert = new FileOutput(); // data
            vert.Endian = Endianness.Big;
            FileOutput vertadd = new FileOutput(); // data
            vertadd.Endian = Endianness.Big;

            FileOutput str = new FileOutput(); // data
            str.Endian = Endianness.Big;


            // obj descriptor

            FileOutput tempstring = new FileOutput(); // data
            for (int i = 0; i < Nodes.Count; i++)
            {
                str.writeString(Nodes[i].Text);
                str.writeByte(0);
                str.align(16);
            }

            int polyCount = 0; // counting number of poly
            foreach (Mesh m in Nodes)
                polyCount += m.Nodes.Count;

            for (int i = 0; i < Nodes.Count; i++)
            {
                Mesh m = (Mesh)Nodes[i];
                foreach (float f in m.boundingBox)
                    d.writeFloat(f);

                d.writeInt(tempstring.size());

                tempstring.writeString(Nodes[i].Text);
                tempstring.writeByte(0);
                tempstring.align(16);

                d.writeInt(m.boneflag); // ID
                d.writeShort(m.singlebind); // Single Bind 
                d.writeShort(m.Nodes.Count); // poly count
                d.writeInt(obj.size() + 0x30 + Nodes.Count * 0x30); // position start for obj

                // write obj info here...
                for (int k = 0; k < Nodes[i].Nodes.Count; k++)
                {
                    obj.writeInt(poly.size());
                    obj.writeInt(vert.size());
                    obj.writeInt(((NUD.Polygon)Nodes[i].Nodes[k]).vertSize >> 4 > 0 ? vertadd.size() : 0);
                    obj.writeShort(((NUD.Polygon)Nodes[i].Nodes[k]).vertices.Count);
                    obj.writeByte(((NUD.Polygon)Nodes[i].Nodes[k]).vertSize); // type of vert

                    int maxUV = ((NUD.Polygon)Nodes[i].Nodes[k]).vertices[0].uv.Count; // TODO: multi uv stuff  mesh[i].polygons[k].maxUV() + 

                    obj.writeByte((maxUV << 4)|(((NUD.Polygon)Nodes[i].Nodes[k]).UVSize & 0xF)); 

                    // MATERIAL SECTION 

                    FileOutput te = new FileOutput();
                    te.Endian = Endianness.Big;

                    int[] texoff = writeMaterial(tex, ((NUD.Polygon)Nodes[i].Nodes[k]).materials, str);
                    //tex.writeOutput(te);

                    //obj.writeInt(tex.size() + 0x30 + mesh.Count * 0x30 + polyCount * 0x30); // Tex properties... This is tex offset
                    obj.writeInt(texoff[0] + 0x30 + Nodes.Count * 0x30 + polyCount * 0x30);
                    obj.writeInt(texoff[1] > 0 ? texoff[1] + 0x30 + Nodes.Count * 0x30 + polyCount * 0x30 : 0);
                    obj.writeInt(texoff[2] > 0 ? texoff[2] + 0x30 + Nodes.Count * 0x30 + polyCount * 0x30 : 0);
                    obj.writeInt(texoff[3] > 0 ? texoff[3] + 0x30 + Nodes.Count * 0x30 + polyCount * 0x30 : 0);

                    obj.writeShort(((NUD.Polygon)Nodes[i].Nodes[k]).faces.Count); // polyamt
                    obj.writeByte(((NUD.Polygon)Nodes[i].Nodes[k]).strip); // polysize 0x04 is strips and 0x40 is easy
                    // :D
                    obj.writeByte(((NUD.Polygon)Nodes[i].Nodes[k]).polflag); // polyflag

                    obj.writeInt(0); // idk, nothing padding??
                    obj.writeInt(0);
                    obj.writeInt(0);

                    // Write the poly...
                    foreach (int face in ((NUD.Polygon)Nodes[i].Nodes[k]).faces)
                        poly.writeShort(face);

                    // Write the vertex....

                    writeVertex(vert, vertadd, ((NUD.Polygon)Nodes[i].Nodes[k]));
                    vertadd.align(4, 0x0);
                }
            }

            //
            d.writeOutput(obj);
            d.writeOutput(tex);
            d.align(16);

            d.writeIntAt(d.size() - 0x30, 0x10);
            d.writeIntAt(poly.size(), 0x14);
            d.writeIntAt(vert.size(), 0x18);
            d.writeIntAt(vertadd.size(), 0x1c);

            d.writeOutput(poly);

            int s = d.size();
            d.align(16);
            s = d.size() - s;
            d.writeIntAt(poly.size() + s, 0x14);

            d.writeOutput(vert);

            s = d.size();
            d.align(16);
            s = d.size() - s;
            d.writeIntAt(vert.size() + s, 0x18);

            d.writeOutput(vertadd);

            s = d.size();
            d.align(16);
            s = d.size() - s;
            d.writeIntAt(vertadd.size() + s, 0x1c);

            d.writeOutput(str);

            d.writeIntAt(d.size(), 0x4);

            return d.getBytes();
        }

        private static void writeUV(FileOutput d, Polygon m)
        {
            int uvCount = (m.UVSize >> 4);
            int uvType = (m.UVSize) & 0xF;

            for (int i = 0; i < m.vertices.Count; i++)
            {

                if (uvType == 0x0)
                {
                    for (int j = 0; j < m.vertices[i].uv.Count; j++)
                    {
                        d.writeHalfFloat(m.vertices[i].uv[j].X);
                        d.writeHalfFloat(m.vertices[i].uv[j].Y);
                    }
                }else
                if (uvType == 0x2)
                {
                    d.writeByte((int)m.vertices[i].col.X);
                    d.writeByte((int)m.vertices[i].col.Y);
                    d.writeByte((int)m.vertices[i].col.Z);
                    d.writeByte((int)m.vertices[i].col.W);
                    for (int j = 0; j < m.vertices[i].uv.Count; j++)
                    {
                        d.writeHalfFloat(m.vertices[i].uv[j].X);
                        d.writeHalfFloat(m.vertices[i].uv[j].Y);
                    }
                }else
                if (uvType == 0x4)
                {
                    d.writeHalfFloat(m.vertices[i].col.X / 0xFF);
                    d.writeHalfFloat(m.vertices[i].col.Y / 0xFF);
                    d.writeHalfFloat(m.vertices[i].col.Z / 0xFF);
                    d.writeHalfFloat(m.vertices[i].col.W / 0xFF);
                    for (int j = 0; j < m.vertices[i].uv.Count; j++)
                    {
                        d.writeHalfFloat(m.vertices[i].uv[j].X);
                        d.writeHalfFloat(m.vertices[i].uv[j].Y);
                    }
                }
                else
                    throw new NotImplementedException("Unsupported UV format");
            }
        }

        private static void writeVertex(FileOutput d, FileOutput add, Polygon poly)
        {
            int weight = poly.vertSize >> 4;
            int vertType = poly.vertSize & 0xF;
            
            if (weight > 0)
            {
                writeUV(d, poly);
                d = add;
            }

            for (int i = 0; i < poly.vertices.Count; i++)
            {
                
                Vertex v = poly.vertices[i];
                if (vertType < 8)
                {
                    d.writeFloat(v.pos.X);
                    d.writeFloat(v.pos.Y);
                    d.writeFloat(v.pos.Z);
                }
                
                if(vertType == 0)
                {
                    d.writeInt(0);
                }
                else if (vertType == 1)
                {
                    d.writeFloat(v.nrm.X);
                    d.writeFloat(v.nrm.Y);
                    d.writeFloat(v.nrm.Z);
                    d.writeFloat(1);
                    d.writeFloat(1);
                }
                else if (vertType == 2)
                {
                    d.writeFloat(v.nrm.X);
                    d.writeFloat(v.nrm.Y);
                    d.writeFloat(v.nrm.Z);
                    d.writeFloat(1);
                    d.writeFloat(v.bitan.X); d.writeFloat(v.bitan.Y); d.writeFloat(v.bitan.Z);
                    d.writeFloat(1);
                    d.writeFloat(v.tan.X); d.writeFloat(v.tan.Y); d.writeFloat(v.tan.Z);
                    d.writeFloat(1);
                    d.writeFloat(1);
                }
                else if (vertType == 3)
                {
                    d.writeFloat(1);
                    d.writeFloat(v.nrm.X);
                    d.writeFloat(v.nrm.Y);
                    d.writeFloat(v.nrm.Z);
                    d.writeFloat(1);
                    d.writeFloat(poly.vertices[i].bitan.X);
                    d.writeFloat(poly.vertices[i].bitan.Y);
                    d.writeFloat(poly.vertices[i].bitan.Z);
                    d.writeFloat(poly.vertices[i].bitan.W);
                    d.writeFloat(poly.vertices[i].tan.X);
                    d.writeFloat(poly.vertices[i].tan.Y);
                    d.writeFloat(poly.vertices[i].tan.Z);
                    d.writeFloat(poly.vertices[i].tan.W);
                }
                else if (vertType == 6)
                {
                    d.writeHalfFloat(v.nrm.X);
                    d.writeHalfFloat(v.nrm.Y);
                    d.writeHalfFloat(v.nrm.Z);
                    d.writeHalfFloat(1);
                }
                else if (vertType == 7)
                {
                    d.writeHalfFloat(v.nrm.X);
                    d.writeHalfFloat(v.nrm.Y);
                    d.writeHalfFloat(v.nrm.Z);
                    d.writeHalfFloat(1);
                    d.writeHalfFloat(poly.vertices[i].bitan.X);
                    d.writeHalfFloat(poly.vertices[i].bitan.Y);
                    d.writeHalfFloat(poly.vertices[i].bitan.Z);
                    d.writeHalfFloat(poly.vertices[i].bitan.W);
                    d.writeHalfFloat(poly.vertices[i].tan.X);
                    d.writeHalfFloat(poly.vertices[i].tan.Y);
                    d.writeHalfFloat(poly.vertices[i].tan.Z);
                    d.writeHalfFloat(poly.vertices[i].tan.W);
                }

                if (weight == 0)
                {
                    if (poly.UVSize >= 18)
                    {
                        d.writeByte((int)poly.vertices[i].col.X);
                        d.writeByte((int)poly.vertices[i].col.Y);
                        d.writeByte((int)poly.vertices[i].col.Z);
                        d.writeByte((int)poly.vertices[i].col.W);
                    }

                    for (int j = 0; j < poly.vertices[i].uv.Count; j++)
                    {
                        d.writeHalfFloat(poly.vertices[i].uv[j].X);
                        d.writeHalfFloat(poly.vertices[i].uv[j].Y);
                    }
                }

                if (weight == 1)
                {
                    d.writeInt(v.node.Count > 0 ? v.node[0] : 0);
                    d.writeInt(v.node.Count > 1 ? v.node[1] : 0);
                    d.writeInt(v.node.Count > 2 ? v.node[2] : 0);
                    d.writeInt(v.node.Count > 3 ? v.node[3] : 0);
                    d.writeFloat(v.weight.Count > 0 ? v.weight[0] : 0);
                    d.writeFloat(v.weight.Count > 1 ? v.weight[1] : 0);
                    d.writeFloat(v.weight.Count > 2 ? v.weight[2] : 0);
                    d.writeFloat(v.weight.Count > 3 ? v.weight[3] : 0);
                }
                if (weight == 2)
                {
                    d.writeShort(v.node.Count > 0 ? v.node[0] : 0);
                    d.writeShort(v.node.Count > 1 ? v.node[1] : 0);
                    d.writeShort(v.node.Count > 2 ? v.node[2] : 0);
                    d.writeShort(v.node.Count > 3 ? v.node[3] : 0);
                    d.writeHalfFloat(v.weight.Count > 0 ? v.weight[0] : 0);
                    d.writeHalfFloat(v.weight.Count > 1 ? v.weight[1] : 0);
                    d.writeHalfFloat(v.weight.Count > 2 ? v.weight[2] : 0);
                    d.writeHalfFloat(v.weight.Count > 3 ? v.weight[3] : 0);
                }
                if (weight == 4)
                {
                    d.writeByte(v.node.Count > 0 ? v.node[0] : 0);
                    d.writeByte(v.node.Count > 1 ? v.node[1] : 0);
                    d.writeByte(v.node.Count > 2 ? v.node[2] : 0);
                    d.writeByte(v.node.Count > 3 ? v.node[3] : 0);
                    d.writeByte((int)(v.weight.Count > 0 ? Math.Round(v.weight[0] * 0xFF) : 0));
                    d.writeByte((int)(v.weight.Count > 1 ? Math.Round(v.weight[1] * 0xFF) : 0));
                    d.writeByte((int)(v.weight.Count > 2 ? Math.Round(v.weight[2] * 0xFF) : 0));
                    d.writeByte((int)(v.weight.Count > 3 ? Math.Round(v.weight[3] * 0xFF) : 0));
                }
            }
        }

        public static int[] writeMaterial(FileOutput d, List<Material> materials, FileOutput str)
        {
            int[] offs = new int[4];
            int c = 0;
            foreach (Material mat in materials)
            {
                offs[c++] = d.size();
                d.writeInt((int)mat.Flags);
                d.writeInt(0); // padding
                d.writeShort(mat.srcFactor);
                d.writeShort(mat.textures.Count);
                d.writeShort(mat.dstFactor);
                d.writeByte(mat.AlphaTest);
                d.writeByte(mat.AlphaFunc);
                d.writeByte(0); // unknown padding?
                d.writeByte(mat.RefAlpha);
                d.writeShort(mat.cullMode);
                d.writeInt(0); // padding
                d.writeInt(mat.unkownWater); 
                d.writeInt(mat.zBufferOffset); 

                foreach (MatTexture tex in mat.textures)
                {
                    d.writeInt(tex.hash);
                    d.writeInt(0);
                    d.writeShort(0);
                    d.writeShort(tex.mapMode);
                    d.writeByte(tex.wrapModeS);
                    d.writeByte(tex.wrapModeT);
                    d.writeByte(tex.minFilter);
                    d.writeByte(tex.magFilter);
                    d.writeByte(tex.mipDetail);
                    d.writeByte(tex.unknown);
                    d.writeInt(0); // padding
                    d.writeShort(0);
                }

                for (int i = 0; i < mat.entries.Count; i++)
                {
                    float[] data;
                    mat.entries.TryGetValue(mat.entries.ElementAt(i).Key, out data);
                    d.writeInt(i == mat.entries.Count - 1 ? 0 : 16 + 4 * data.Length);
                    d.writeInt(str.size());

                    str.writeString(mat.entries.ElementAt(i).Key);
                    str.writeByte(0);
                    str.align(16);

                    d.writeInt(data.Length);
                    d.writeInt(0);
                    foreach (float f in data)
                        d.writeFloat(f);
                }
            }
            return offs;
        }

        #endregion
        
        #region Functions
        public void MergePoly()
        {
            Dictionary<string, Mesh> nmesh = new Dictionary<string, Mesh>();
            foreach(Mesh m in Nodes)
            {
                if (nmesh.ContainsKey(m.Text))
                {
                    // merge poly
                    List<Polygon> torem = new List<Polygon>();
                    foreach(Polygon p in m.Nodes)
                        torem.Add(p);

                    foreach (Polygon p in torem)
                    {
                        m.Nodes.Remove(p);
                        nmesh[m.Text].Nodes.Add(p);
                    }
                } else
                {
                    nmesh.Add(m.Text, m);
                }
            }
            // consolidate
            Nodes.Clear();
            foreach (string n in nmesh.Keys)
            {
                Nodes.Add(nmesh[n]);
            }
        }

        public void ChangeTextureIds(int newTexId)
        {
            foreach (Mesh mesh in Nodes)
            {
                foreach (Polygon polygon in mesh.Nodes)
                {
                    foreach (Material material in polygon.materials)
                    {
                        foreach (MatTexture matTexture in material.textures)
                        {
                            // Only change the first 3 bytes.
                            matTexture.hash = matTexture.hash & 0xFF;
                            int first3Bytes = (int)(newTexId & 0xFFFFFF00);
                            matTexture.hash = matTexture.hash | first3Bytes;
                        }
                    }
                }
            }
        }

        public int GetFirstTexId()
        {
            foreach (Mesh m in Nodes)
            {
                foreach (Polygon poly in m.Nodes)
                {
                    foreach (Material mat in poly.materials)
                    {
                        foreach (MatTexture matTex in mat.textures)
                        {
                            return matTex.hash;
                        }
                    }
                }
            }

            return 0;
        }

        #endregion

        #region ClassStructure

        public struct Vector4i
        {
            int x, y, z, w;

            public Vector4i(int x, int y, int z, int w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }

            public static int Size = 4 * sizeof(int);
        }

        public struct DisplayVertex
        {
            // Used for rendering.
            public Vector3 pos;
            public Vector3 nrm;
            public Vector3 tan;
            public Vector3 bit;
            public Vector2 uv;
            public Vector4 col;
            public Vector4 node;
            public Vector4 weight;
            public Vector2 uv2;
            public Vector2 uv3;

            public static int Size = 4 * (3 + 3 + 3 + 3 + 2 + 4 + 4 + 4 + 2 + 2);
        }

        public class Vertex
        {
            public Vector3 pos = new Vector3(0, 0, 0), nrm = new Vector3(0, 0, 0);
            public Vector4 bitan = new Vector4(0, 0, 0, 1), tan = new Vector4(0, 0, 0, 1);
            public Vector4 col = new Vector4(127, 127, 127, 127);
            public List<Vector2> uv = new List<Vector2>();
            public List<int> node = new List<int>();
            public List<float> weight = new List<float>();

            public Vertex()
            {
            }

            public Vertex(float x, float y, float z)
            {
                pos = new Vector3(x, y, z);
            }

            public bool Equals(Vertex p)
            {
                return pos.Equals(p.pos) && nrm.Equals(p.nrm) && new HashSet<Vector2>(uv).SetEquals(p.uv) && col.Equals(p.col)
                    && new HashSet<int>(node).SetEquals(p.node) && new HashSet<float>(weight).SetEquals(p.weight);
            }

            public override string ToString()
            {
                return pos.ToString();
            }
        }

        public class MatTexture
        {
            public int hash;
            public int mapMode = 0;
            public int wrapModeS = 1;
            public int wrapModeT = 1;
            public int minFilter = 3;
            public int magFilter = 2;
            public int mipDetail = 6;
            public int unknown = 0;

            public MatTexture()
            {

            }

            public MatTexture(int hash)
            {
                this.hash = hash;
            }

            public MatTexture Clone()
            {
                MatTexture t = new MatTexture();
                t.hash = hash;
                t.mapMode = mapMode;
                t.wrapModeS = wrapModeS;
                t.wrapModeT = wrapModeT;
                t.minFilter = minFilter;
                t.magFilter = magFilter;
                t.mipDetail = mipDetail;
                t.unknown = unknown;
                return t;
            }

            public static MatTexture getDefault()
            {
                MatTexture defaultTex = new MatTexture((int)DummyTextures.DummyRamp);
                return defaultTex;
            }
        }

        public class Material
        {
            public Dictionary<string, float[]> entries = new Dictionary<string, float[]>();
            public Dictionary<string, float[]> anims = new Dictionary<string, float[]>();
            public List<MatTexture> textures = new List<MatTexture>();

            private uint flag;
            public uint Flags
            {
                get
                {
                    return RebuildFlag4thByte();
                }
                set
                {
                    flag = value;
                    CheckFlags();
                }
            }

            public int blendMode = 0;
            public int dstFactor = 0;
            public int srcFactor = 0;
            public int AlphaTest = 0;
            public int AlphaFunc = 0;
            public int RefAlpha = 0;
            public int cullMode = 0;
            public int displayTexId = -1;

            public int unknown1 = 0;
            public int unkownWater = 0;
            public int zBufferOffset = 0;

            //flags
            public bool glow = false;
            public bool hasShadow = false;
            public bool useVertexColor = false;
            public bool useReflectionMask = false;
            public bool useColorGainOffset = false;
            public bool hasBayoHair = false;
            public bool useDiffuseBlend = false;
            public bool softLightBrighten = false;

            // Texture flags
            public bool hasDiffuse = false;
            public bool hasNormalMap = false;
            public bool hasDiffuse2 = false;
            public bool hasDiffuse3 = false;
            public bool hasAoMap = false;
            public bool hasStageMap = false;
            public bool hasCubeMap = false;
            public bool hasRamp = false;
            public bool hasSphereMap = false;
            public bool hasDummyRamp = false;

            // texture IDs for preserving existing textures
            public int diffuse1ID = 0;
            public int diffuse2ID = 0;
            public int diffuse3ID = 0;
            public int normalID = 0;
            public int rampID = (int)DummyTextures.DummyRamp;
            public int dummyRampID = (int)DummyTextures.DummyRamp;
            public int sphereMapID = 0;
            public int aoMapID = 0;
            public int stageMapID = (int)DummyTextures.StageMapHigh;
            public int cubeMapID = 0;

            public Material()
            {

            }

            public Material Clone()
            {
                Material m = new Material();

                foreach (KeyValuePair<string, float[]> e in entries)
                    m.entries.Add(e.Key, e.Value);

                m.Flags = Flags;
                m.blendMode = blendMode;
                m.dstFactor = dstFactor;
                m.srcFactor = srcFactor;
                m.AlphaTest = AlphaTest;
                m.AlphaFunc = AlphaFunc;
                m.RefAlpha = RefAlpha;
                m.cullMode = cullMode;
                m.displayTexId = displayTexId;

                m.unknown1 = 0;
                m.unkownWater = 0;
                m.zBufferOffset = 0;

                foreach(MatTexture t in textures)
                {
                    m.textures.Add(t.Clone());
                }

                return m;
            }

            public static Material GetDefault()
            {
                Material material = new Material();
                material.Flags = 0x94010161;
                material.cullMode = 0x0405;
                material.entries.Add("NU_colorSamplerUV", new float[] { 1, 1, 0, 0 });
                material.entries.Add("NU_fresnelColor", new float[] { 1, 1, 1, 1 });
                material.entries.Add("NU_blinkColor", new float[] { 0, 0, 0, 0 });
                material.entries.Add("NU_aoMinGain", new float[] { 0, 0, 0, 0 });
                material.entries.Add("NU_lightMapColorOffset", new float[] { 0, 0, 0, 0 });
                material.entries.Add("NU_fresnelParams", new float[] { 1, 0, 0, 0 });
                material.entries.Add("NU_alphaBlendParams", new float[] { 0, 0, 0, 0 });
                material.entries.Add("NU_materialHash", new float[] { FileData.toFloat(0x7E538F65), 0, 0, 0 });

                material.textures.Add(new MatTexture(0x10000000));
                material.textures.Add(MatTexture.getDefault());
                return material;
            }

            public static Material GetStageDefault()
            {
                Material material = new Material();
                material.Flags = 0xA2001001;
                material.RefAlpha = 128;
                material.cullMode = 1029;

                // Display a default texture rather than a dummy texture.
                material.textures.Add(new MatTexture(0));

                material.entries.Add("NU_colorSamplerUV", new float[] { 1, 1, 0, 0 });
                material.entries.Add("NU_diffuseColor", new float[] { 1, 1, 1, 0.5f });
                material.entries.Add("NU_materialHash", new float[] { BitConverter.ToSingle(new byte[] { 0x12, 0xEE, 0x2A, 0x1B }, 0), 0, 0, 0 });
                return material;
            }

            public void CopyTextureIds(Material other)
            {
                // Copies all the texture IDs from the source material to the current material. 
                // This is useful for preserving Tex IDs when using a preset or changing flags. 

                for (int i = 0; i < Math.Min(textures.Count, other.textures.Count); i++)
                {
                    if (hasDiffuse)
                    {
                        textures[i].hash = other.textures[i].hash;
                        continue;
                    }
                    if (hasDiffuse2)
                    {
                        textures[i].hash = other.textures[i].hash;
                        continue;
                    }
                    if (hasDiffuse3)
                    {
                        textures[i].hash = other.textures[i].hash;
                        continue;
                    }
                    if (hasStageMap)
                    {
                        // Don't preserve stageMap ID.
                        continue;
                    }
                    if (hasCubeMap)
                    {
                        textures[i].hash = other.textures[i].hash;
                        continue;
                    }
                    if (hasSphereMap)
                    {
                        textures[i].hash = other.textures[i].hash;
                        continue;
                    }
                    if (hasAoMap)
                    {
                        textures[i].hash = other.textures[i].hash;
                        continue;
                    }
                    if (hasNormalMap)
                    {
                        textures[i].hash = other.textures[i].hash;
                        continue;
                    }
                    if (hasRamp)
                    {
                        textures[i].hash = other.textures[i].hash;
                        continue;
                    }
                    if (hasDummyRamp)
                    {
                        // Dummy ramp should almost always be 0x10080000.
                        continue;
                    }
                }
            }


            public void MakeMetal(int newDifTexId, int newCubeTexId, float[] minGain, float[] refColor, float[] fresParams, float[] fresColor, bool preserveDiffuse = false, bool preserveNrmMap = true)
            {
                float materialHash = -1f;
                if (entries.ContainsKey("NU_materialHash"))
                    materialHash = entries["NU_materialHash"][0];

                anims.Clear();
                entries.Clear();

                // Don't add normal maps to materials that don't use one. 
                if (hasNormalMap && preserveNrmMap)
                    Flags = 0x9601106B;
                else
                    Flags = 0x96011069;

                // The texture ID used for diffuse later. 
                int difTexID = newDifTexId;
                if (preserveDiffuse)
                    difTexID = diffuse1ID;

                // add all the textures
                textures.Clear();
                displayTexId = -1;

                MatTexture diffuse = new MatTexture(difTexID);
                MatTexture cube = new MatTexture(newCubeTexId);
                MatTexture normal = new MatTexture(normalID);
                MatTexture dummyRamp = MatTexture.getDefault();
                dummyRamp.hash = 0x10080000;

                if (hasNormalMap)
                {
                    textures.Add(diffuse);
                    textures.Add(cube);
                    textures.Add(normal);
                    textures.Add(dummyRamp);
                }
                else
                {
                    textures.Add(diffuse);
                    textures.Add(cube);
                    textures.Add(dummyRamp);
                }

                // add material properties
                entries.Add("NU_colorSamplerUV", new float[] { 1, 1, 0, 0 });
                entries.Add("NU_fresnelColor", fresColor);
                entries.Add("NU_blinkColor", new float[] { 0f, 0f, 0f, 0 });
                entries.Add("NU_reflectionColor", refColor);
                entries.Add("NU_aoMinGain", minGain);
                entries.Add("NU_lightMapColorOffset", new float[] { 0f, 0f, 0f, 0 });
                entries.Add("NU_fresnelParams", fresParams);
                entries.Add("NU_alphaBlendParams", new float[] { 0f, 0f, 0f, 0 });
                entries.Add("NU_materialHash", new float[] { materialHash, 0f, 0f, 0 });
            }

            public uint RebuildFlag4thByte()
            {
                int new4thByte = 0;
                if (hasDiffuse)
                    new4thByte |= (int)TextureFlags.DiffuseMap;
                if (hasNormalMap)
                    new4thByte |= (int)TextureFlags.NormalMap;
                if (hasCubeMap || hasRamp)
                    new4thByte |= (int)TextureFlags.RampCubeMap;
                if (hasStageMap || hasAoMap)
                    new4thByte |= (int)TextureFlags.StageAOMap;
                if (hasSphereMap)
                    new4thByte |= (int)TextureFlags.SphereMap;
                if (glow)
                    new4thByte |= (int) TextureFlags.Glow;
                if (hasShadow)
                    new4thByte |= (int) TextureFlags.Shadow;
                if (hasDummyRamp)
                    new4thByte |= (int) TextureFlags.DummyRamp; 
                flag = (uint)(((int)flag & 0xFFFFFF00) | new4thByte);

                return flag;
            }

            private void CheckFlags()
            {
                int intFlags = ((int)flag);
                glow = (intFlags & (int)TextureFlags.Glow) > 0;
                hasShadow = (intFlags & (int)TextureFlags.Shadow) > 0;
                CheckMisc(intFlags);
                CheckTextures(flag);
            }

            private void CheckMisc(int matFlags)
            {
                // Some hacky workarounds until I understand flags better.
                useColorGainOffset = CheckColorGain(flag);
                useDiffuseBlend = (matFlags & 0xD0090000) == 0xD0090000 || (matFlags & 0x90005000) == 0x90005000;
                useVertexColor = CheckVertexColor(flag);
                useReflectionMask = (matFlags & 0xFFFFFF00) == 0xF8820000;
                hasBayoHair = (matFlags & 0x00FF0000) == 0x00420000;
                softLightBrighten = ((matFlags & 0x00FF0000) == 0x00810000 || (matFlags & 0xFFFF0000) == 0xFA600000);
            }

            private bool CheckVertexColor(uint matFlags)
            {
                // Characters and stages use different values for enabling vertex color.
                // Always use vertex color for effect materials for now.
                byte byte1 = (byte) ((matFlags & 0xFF000000) >> 24);
                bool vertexColor = (byte1 == 0x94) || (byte1 == 0x9A) || (byte1 == 0x9C) || (byte1 == 0xA2) 
                    || (byte1 == 0xA4) || (byte1 == 0xB0);

                return vertexColor;
            }

            private bool CheckColorGain(uint matFlags)
            {
                byte byte1 = (byte)((matFlags & 0xFF000000) >> 24);
                byte byte2 = (byte)((matFlags & 0x00FF0000) >> 16);
                byte byte4 = (byte)((matFlags & 0xFF));

                bool hasLightingChannel = (byte1 & 0x0C) == 0x0C;
                bool hasByte2 = (byte2 == 0x61) || (byte2== 0x42) || (byte2 == 0x44);
                bool hasByte4 = (byte4 == 0x61);

                return hasLightingChannel && hasByte2 && hasByte4;
            }

            private void CheckTextures(uint matFlags)
            {
                // Effect materials use 4th byte 00 but often still have a diffuse texture.
                hasDummyRamp = (matFlags & (int)TextureFlags.DummyRamp) > 0;

                hasDiffuse = (matFlags & (int)TextureFlags.DiffuseMap) > 0 || (matFlags & 0xF0000000) == 0xB0000000;

                byte byte3 = (byte)((matFlags & 0x0000FF00) >> 8);
                hasDiffuse3 = (byte3 & 0x91) == 0x91 || (byte3 & 0x96) == 0x96 || (byte3 & 0x99) == 0x99;

                hasDiffuse2 = (matFlags & (int)TextureFlags.RampCubeMap) > 0 && (matFlags & (int)TextureFlags.NormalMap) == 0
                    && hasDummyRamp || hasDiffuse3;

                hasNormalMap = (matFlags & (int)TextureFlags.NormalMap) > 0;
                hasSphereMap = (matFlags & (int)TextureFlags.SphereMap) > 0;
                hasAoMap = (matFlags & (int)TextureFlags.StageAOMap) > 0 && !hasDummyRamp;
                hasStageMap = (matFlags & (int)TextureFlags.StageAOMap) > 0 && hasDummyRamp;
                hasCubeMap = (matFlags & (int)TextureFlags.RampCubeMap) > 0 && (!hasDummyRamp) && (!hasSphereMap);
                hasRamp = (matFlags & (int) TextureFlags.RampCubeMap) > 0 && hasDummyRamp; 
            }
        }

        public class Polygon : TreeNode
        {
            public List<Vertex> vertices = new List<Vertex>();
            public List<int> faces = new List<int>();
            public int displayFaceSize = 0;

            // Material
            public List<Material> materials = new List<Material>();

            // for nud stuff
            public int vertSize = 0x46; // defaults to a basic bone weighted vertex format
            public int UVSize = 0x12;
            public int strip = 0x40;
            public int polflag = 0x04;

            // for drawing
            public bool isTransparent = false;
            public int[] display;
            public int[] selectedVerts;
            public int Offset; // For Rendering

            public Polygon()
            {
                Checked = true;
                Text = "Polygon";
                ImageKey = "polygon";
                SelectedImageKey = "polygon";
            }

            public void AddVertex(Vertex v)
            {
                vertices.Add(v);
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

            public List<DisplayVertex> CreateDisplayVertices()
            {
                // rearrange faces
                display = getDisplayFace().ToArray();

                List<DisplayVertex> displayVertList = new List<DisplayVertex>();

                if (faces.Count <= 3)
                    return displayVertList;
                foreach (Vertex v in vertices)
                {
                    DisplayVertex displayVert = new DisplayVertex()
                    {
                        pos = v.pos,
                        nrm = v.nrm,
                        tan = v.tan.Xyz,
                        bit = v.bitan.Xyz,
                        col = v.col / 127,
                        uv = v.uv.Count > 0 ? v.uv[0] : new Vector2(0, 0),
                        uv2 = v.uv.Count > 1 ? v.uv[1] : new Vector2(0, 0),
                        uv3 = v.uv.Count > 2 ? v.uv[2] : new Vector2(0, 0),
                        node = new Vector4(v.node.Count > 0 ? v.node[0] : -1,
                        v.node.Count > 1 ? v.node[1] : -1,
                        v.node.Count > 2 ? v.node[2] : -1,
                        v.node.Count > 3 ? v.node[3] : -1),
                        weight = new Vector4(v.weight.Count > 0 ? v.weight[0] : 0,
                        v.weight.Count > 1 ? v.weight[1] : 0,
                        v.weight.Count > 2 ? v.weight[2] : 0,
                        v.weight.Count > 3 ? v.weight[3] : 0),
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

                List<int> f = getDisplayFace();
                Vector3[] tanArray = new Vector3[vertices.Count];
                Vector3[] bitanArray = new Vector3[vertices.Count];

                CalculateTanBitanArrays(f, tanArray, bitanArray);
                ApplyTanBitanArray(tanArray, bitanArray);
            }

            public void SetVertexColor(Vector4 intColor)
            {
                // (127, 127, 127, 255) is white.
                foreach (Vertex v in vertices)
                {
                    v.col = intColor;
                }
            }


            private void ApplyTanBitanArray(Vector3[] tanArray, Vector3[] bitanArray)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vertex v = vertices[i];
                    Vector3 newTan = tanArray[i];
                    Vector3 newBitan = bitanArray[i];

                    // The tangent and bitangent should be orthogonal to the normal. 
                    // Bitangents are not calculated with a cross product to prevent flipped shading  with mirrored normal maps.
                    v.tan = new Vector4(Vector3.Normalize(newTan - v.nrm * Vector3.Dot(v.nrm, newTan)), 1);
                    v.bitan = new Vector4(Vector3.Normalize(newBitan - v.nrm * Vector3.Dot(v.nrm, newBitan)), 1);
                    v.bitan *= -1;
                }
            }

            private void CalculateTanBitanArrays(List<int> faces, Vector3[] tanArray, Vector3[] bitanArray)
            {
                for (int i = 0; i < displayFaceSize; i += 3)
                {
                    Vertex v1 = vertices[faces[i]];
                    Vertex v2 = vertices[faces[i + 1]];
                    Vertex v3 = vertices[faces[i + 2]];

                    float x1 = v2.pos.X - v1.pos.X;
                    float x2 = v3.pos.X - v1.pos.X;
                    float y1 = v2.pos.Y - v1.pos.Y;
                    float y2 = v3.pos.Y - v1.pos.Y;
                    float z1 = v2.pos.Z - v1.pos.Z;
                    float z2 = v3.pos.Z - v1.pos.Z;

                    if (v2.uv.Count < 1)
                        break;

                    float s1 = v2.uv[0].X - v1.uv[0].X;
                    float s2 = v3.uv[0].X - v1.uv[0].X;
                    float t1 = v2.uv[0].Y - v1.uv[0].Y;
                    float t2 = v3.uv[0].Y - v1.uv[0].Y;

                    float div = (s1 * t2 - s2 * t1);
                    float r = 1.0f / div;

                    // Fix +/- infinity from division by 0.
                    if (r == float.PositiveInfinity || r == float.NegativeInfinity)
                        r = 1.0f;

                    float sX = t2 * x1 - t1 * x2;
                    float sY = t2 * y1 - t1 * y2;
                    float sZ = t2 * z1 - t1 * z2;
                    Vector3 s = new Vector3(sX, sY, sZ) * r;

                    float tX = s1 * x2 - s2 * x1;
                    float tY = s1 * y2 - s2 * y1;
                    float tZ = s1 * z2 - s2 * z1;
                    Vector3 t = new Vector3(tX, tY, tZ) * r;

                    // Prevents black tangents or bitangents due to having vertices with the same UV coordinates. 
                    float delta = 0.00075f;
                    bool sameU = (Math.Abs(v1.uv[0].X - v2.uv[0].X) < delta) && (Math.Abs(v2.uv[0].X - v3.uv[0].X) < delta);
                    bool sameV = (Math.Abs(v1.uv[0].Y - v2.uv[0].Y) < delta) && (Math.Abs(v2.uv[0].Y - v3.uv[0].Y) < delta);
                    if (sameU || sameV)
                    {
                        // Let's pick some arbitrary tangent vectors.
                        s = new Vector3(1,0,0);
                        t = new Vector3(0,1,0);
                    }

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

                List<int> f = getDisplayFace();

                for (int i = 0; i < displayFaceSize; i += 3)
                {
                    Vertex v1 = vertices[f[i]];
                    Vertex v2 = vertices[f[i+1]];
                    Vertex v3 = vertices[f[i+2]];
                    Vector3 nrm = CalculateNormal(v1,v2,v3);

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

                List<int> f = getDisplayFace();

                for (int i = 0; i < displayFaceSize; i += 3)
                {
                    Vertex v1 = vertices[f[i]];
                    Vertex v2 = vertices[f[i + 1]];
                    Vertex v3 = vertices[f[i + 2]];
                    Vector3 nrm = CalculateNormal(v1, v2, v3);

                    normals[f[i + 0]] += nrm * (nrm.Length / 2);
                    normals[f[i + 1]] += nrm * (nrm.Length / 2);
                    normals[f[i + 2]] += nrm * (nrm.Length / 2);
                }

                for (int i = 0; i < normals.Length; i++)
                    vertices[i].nrm = normals[i].Normalized();
            }

            private Vector3 CalculateNormal(Vertex v1, Vertex v2, Vertex v3)
            {
                Vector3 U = v2.pos - v1.pos;
                Vector3 V = v3.pos - v1.pos;

                // Don't normalize here, so surface area can be calculated. 
                return Vector3.Cross(U, V);
            }

            public void AddDefaultMaterial()
            {
                Material mat = Material.GetDefault();
                materials.Add(mat);
                mat.textures.Add(new MatTexture(0x10000000));
                mat.textures.Add(MatTexture.getDefault());
            }

            public List<int> getDisplayFace()
            {
                if ((strip >> 4) == 4)
                {
                    displayFaceSize = faces.Count;
                    return faces;
                }
                else
                {
                    List<int> f = new List<int>();

                    int startDirection = 1;
                    int p = 0;
                    int f1 = faces[p++];
                    int f2 = faces[p++];
                    int faceDirection = startDirection;
                    int f3;
                    do
                    {
                        f3 = faces[p++];
                        if (f3 == 0xFFFF)
                        {
                            f1 = faces[p++];
                            f2 = faces[p++];
                            faceDirection = startDirection;
                        }
                        else
                        {
                            faceDirection *= -1;
                            if ((f1 != f2) && (f2 != f3) && (f3 != f1))
                            {
                                if (faceDirection > 0)
                                {
                                    f.Add(f3);
                                    f.Add(f2);
                                    f.Add(f1);
                                }
                                else
                                {
                                    f.Add(f2);
                                    f.Add(f3);
                                    f.Add(f1);
                                }
                            }
                            f1 = f2;
                            f2 = f3;
                        }
                    } while (p < faces.Count);

                    displayFaceSize = f.Count;
                    return f;
                }
            }
        }

        // typically a mesh will just have 1 polygon
        // but you can just use the mesh class without polygons
        public class Mesh : TreeNode
        {
            public enum BoneFlags
            {
                NotRigged = 0,
                Rigged = 4,
                SingleBind = 8
            }

            public int boneflag = (int)BoneFlags.Rigged; 
            public short singlebind = -1;
            public int sortBias = 0;
            public bool billboardY = false;
            public bool billboard = false;
            public bool nsc = false;

            public float[] boundingBox = new float[8];

            public Mesh()
            {
                Checked = true;
                ImageKey = "mesh";
                SelectedImageKey = "mesh";
            }

            public void addVertex(Vertex v)
            {
                if (Nodes.Count == 0)
                    Nodes.Add(new Polygon());

                ((Polygon)Nodes[0]).AddVertex(v);
            }

            public void generateBoundingBox()
            {
                Vector3 cen1 = new Vector3(0,0,0), cen2 = new Vector3(0,0,0);
                double rad1 = 0, rad2 = 0;

                //Get first vert
                int vertCount = 0;
                Vector3 vert0 = new Vector3();
                foreach (Polygon p in Nodes)
                {
                    foreach (Vertex v in p.vertices)
                    {
                        vert0 = v.pos;
                        vertCount++;
                        break;
                    }
                    break;
                }

                if (vertCount == 0)
                    return;

                //Calculate average and min/max
                Vector3 min = new Vector3(vert0);
                Vector3 max = new Vector3(vert0);
                
                vertCount = 0;
                foreach (Polygon p in Nodes)
                {
                    foreach(Vertex v in p.vertices)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            min[i] = Math.Min(min[i], v.pos[i]);
                            max[i] = Math.Max(max[i], v.pos[i]);
                        }

                        cen1 += v.pos;
                        vertCount++;
                    }
                }

                cen1 /= vertCount;
                for (int i = 0; i < 3; i++)
                    cen2[i] = (min[i]+max[i])/2;

                //Calculate the radius of each
                double dist1, dist2;
                foreach (Polygon p in Nodes)
                {
                    foreach (Vertex v in p.vertices)
                    {
                        dist1 = ((Vector3)(v.pos - cen1)).Length;
                        if (dist1 > rad1)
                            rad1 = dist1;

                        dist2 = ((Vector3)(v.pos - cen2)).Length;
                        if (dist2 > rad2)
                            rad2 = dist2;
                    }
                }

                // Use the one with the lowest radius.
                Vector3 temp;
                double radius;
                if (rad1 < rad2)
                {
                    temp = cen1;
                    radius = rad1;
                }
                else
                {
                    temp = cen2;
                    radius = rad2;
                }

                // Set
                for (int i = 0; i < 3; i++)
                {
                    boundingBox[i] = temp[i];
                    boundingBox[i+4] = temp[i];
                }
                boundingBox[3] = (float)radius;
                boundingBox[7] = 0;
            }

            public void calculateSortBias()
            {
                if (!(Text.Contains("SORTBIAS")))
                    return;

                // Isolate the integer value from the mesh name.
                string sortBiasText = "";
                for (int i = Text.IndexOf("SORTBIAS") + 8; i < Text.Length; i++)
                {
                    if (Text[i] != '_')
                    {
                        sortBiasText += Text[i];
                    }
                    else
                        break;
                }

                int sortBiasValue = 0;
                int.TryParse(sortBiasText, out sortBiasValue);
                sortBias = sortBiasValue;              
            }
        }

        #endregion

        #region Converters

        public MBN toMBN()
        {
            MBN m = new Smash_Forge.MBN();

            m.setDefaultDescriptor();
            List<MBN.Vertex> vertBank = new List<MBN.Vertex>();

            foreach (Mesh mesh in Nodes)
            {
                MBN.Mesh nmesh = new MBN.Mesh();
                
                int pi = 0;
                int fadd = vertBank.Count;
                nmesh.nodeList = new List<List<int>>();
                nmesh.faces = new List<List<int>>();
                foreach (Polygon p in mesh.Nodes)
                {
                    List<int> nodeList = new List<int>();
                    // vertices
                    foreach(Vertex v in p.vertices)
                    {
                        MBN.Vertex mv = new MBN.Vertex();
                        mv.pos = v.pos;
                        mv.nrm = v.nrm;
                        List<Vector2> uvs = new List<Vector2>();
                        uvs.Add(new Vector2(v.uv[0].X, 1 - v.uv[0].Y));
                        mv.tx = uvs;
                        mv.col = v.col;
                        int n1 = v.node[0];
                        int n2 = v.node.Count > 1 ? v.node[1] : 0;
                        if (!nodeList.Contains(n1)) nodeList.Add(n1);
                        if (!nodeList.Contains(n2)) nodeList.Add(n2);
                        mv.node.Add(nodeList.IndexOf(n1));
                        mv.node.Add(nodeList.IndexOf(n2));
                        mv.weight.Add(v.weight[0]);
                        mv.weight.Add(v.weight.Count > 1 ? v.weight[1] : 0);
                        vertBank.Add(mv);
                    }
                    // Node list 
                    nmesh.nodeList.Add(nodeList);
                    // polygons
                    List<int> fac = new List<int>();
                    nmesh.faces.Add(fac);
                    foreach (int i in p.faces)
                        fac.Add(i + fadd);
                    pi++;
                }

                m.mesh.Add(nmesh);
            }
            m.vertices = vertBank;

            return m;
        }

        public void OptimizeFileSize(bool singleBind = false)
        {
            // Remove Duplicates
            MergePoly();
            MergeDuplicateVertices();
            OptimizeSingleBind(false);
        }

        private void MergeDuplicateVertices()
        {
            // Massive reductions in file size but very slow.

            int MAX_BANK = 30000; //  for speeding this up a little with some loss...

            foreach (Mesh m in Nodes)
            {
                foreach (Polygon p in m.Nodes)
                {
                    List<Vertex> newVertices = new List<Vertex>();
                    List<int> newFaces = new List<int>();

                    List<Vertex> vbank = new List<Vertex>(); // only check oast 50 verts - may miss far apart ones but is faster
                    foreach (int f in p.faces)
                    {
                        int newFaceIndex = -1; 
                        int i = 0;

                        // Has to loop through all the new vertices each time, which is very slow.
                        foreach (Vertex v in vbank)
                        {
                            if (v.Equals(p.vertices[f]))
                            {
                                newFaceIndex = i;
                                break;
                            }
                            else
                                i++;
                        }

                        bool verticesAreEqual = newFaceIndex != -1;
                        if (verticesAreEqual)
                        {
                            newFaces.Add(newVertices.Count + newFaceIndex);
                        }
                        else
                        {
                            vbank.Add(p.vertices[f]);
                            newFaces.Add(newVertices.Count + vbank.Count - 1);
                        }
                        if(vbank.Count > MAX_BANK)
                        {
                            newVertices.AddRange(vbank);
                            vbank.Clear();
                        }
                    }
                    newVertices.AddRange(vbank);

                    p.vertices = newVertices;
                    p.faces = newFaces;
                    p.displayFaceSize = 0;
                }
            }
        }

        private void OptimizeSingleBind(bool useSingleBind)
        {
            // Use single bind to avoid saving weights.
            // The space savings are significant but not as much as merging duplicate vertices. 

            foreach (Mesh m in Nodes)
            {
                bool isSingleBound = true;
                int singleBindBone = -1;

                foreach (Polygon p in m.Nodes)
                {
                    foreach (Vertex v in p.vertices)
                    {
                        if (v.node.Count > 0 && isSingleBound)
                        {
                            // Can't use single bind if some vertices aren't weighted to the same bone. 
                            if (singleBindBone == -1)
                            {
                                singleBindBone = v.node[0];
                            }
                            else if(singleBindBone != v.node[0])
                            {
                                isSingleBound = false;
                                break;
                            }

                            // Vertices bound to a single bone will have a node.Count of 1.
                            if (v.node.Count > 1)
                            {
                                isSingleBound = false;
                                break;
                            }
                     
                        }
                    }
                }

                if (isSingleBound && useSingleBind)
                {
                    SingleBindMesh(m, singleBindBone);
                }
            }
        }

        private static void SingleBindMesh(Mesh m, int singleBindBone)
        {
            m.boneflag = (int)Mesh.BoneFlags.SingleBind;
            m.singlebind = (short)singleBindBone;
            foreach (Polygon p in m.Nodes)
            {
                p.polflag = 0;
                p.vertSize = p.vertSize & 0x0F;
            }
        }

        public void ComputeTangentBitangent()
        {
            foreach (Mesh m in Nodes)
            {
                foreach (Polygon p in m.Nodes)
                    p.CalculateTangentBitangent();
            }
        }

        #endregion

        public List<int> GetTexIds()
        {
            List<int> texIds = new List<int>();
            foreach (Mesh m in Nodes)
                foreach (Polygon poly in m.Nodes)
                    foreach (var mat in poly.materials)
                        if(!texIds.Contains(mat.displayTexId))
                            texIds.Add(mat.displayTexId);
            return texIds;
        }

        public void Dispose()
        {
            if (GL.IsBuffer(ibo_elements))
            {
                GL.DeleteBuffer(ibo_elements);
                GL.DeleteBuffer(vbo_position);
                GL.DeleteBuffer(ubo_bones);
                GL.DeleteBuffer(vbo_select);
            }
            Nodes.Clear();
        }
    }
}

