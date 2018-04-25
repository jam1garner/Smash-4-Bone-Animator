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

namespace Smash_Forge
{
    public class BFRES : FileBase
    {
        public override Endianness Endian { get; set; }

        public List<string> stringContainer = new List<string>();
        public List<FMDL_Model> models = new List<FMDL_Model>();
        public List<WU.FMDL_Model> WiiU_models = new List<WU.FMDL_Model>();
        public static Shader shader = null;
        public List<Matrix4> BoneFixArray = new List<Matrix4>();
        public Dictionary<string, FTEX> textures = new Dictionary<string, FTEX>();
        public int FSKACount;


        public TreeNode Models = new TreeNode() { Text = "Models" };
        public TreeNode MaterialAnim = new TreeNode() { Text = "Material Animations" };
        public TreeNode SkeletalAnim = new TreeNode() { Text = "Skeletal Animations" };
        public TreeNode VisualAnim = new TreeNode() { Text = "Visual Animations" };
        public TreeNode ShapeAnim = new TreeNode() { Text = "Shape Animations" };
        public TreeNode SceneAnim = new TreeNode() { Text = "Scene Animations" };
        public TreeNode Embedded = new TreeNode() { Text = "Embedded Files" };

        public int readOffset(FileData f)
        { 
            return f.pos() + f.readInt();
        }

        // gl buffer objects
        int vbo_position;
        int vbo_color;
        int vbo_nrm;
        int vbo_uv0;
        int vbo_uv1;
        int vbo_weight;
        int vbo_bone;
        int ibo_elements;

        Vector2[] uvdata0, uvdata1;
        Vector3[] vertdata, nrmdata;
        int[] facedata;
        Vector4[] bonedata, coldata, weightdata;

        #region Render BFRES

        public BFRES()
        {
            Nodes.Add(Models);
            Nodes.Add(SkeletalAnim);
            Nodes.Add(MaterialAnim);
            Nodes.Add(VisualAnim);
            Nodes.Add(ShapeAnim);
            Nodes.Add(SceneAnim);
            Nodes.Add(Embedded);
            ImageKey = "bfres";
            SelectedImageKey = "bfres";

            GL.GenBuffers(1, out vbo_position);
            GL.GenBuffers(1, out vbo_color);
            GL.GenBuffers(1, out vbo_nrm);
            GL.GenBuffers(1, out vbo_uv0);
            GL.GenBuffers(1, out vbo_uv1);
            GL.GenBuffers(1, out vbo_bone);
            GL.GenBuffers(1, out vbo_weight);
            GL.GenBuffers(1, out ibo_elements);

            if (!Runtime.shaders.ContainsKey("BFRES"))
            {
                Rendering.ShaderTools.CreateShader("BFRES", "/lib/Shader/Legacy/", "/lib/Shader/");
            }

            Runtime.shaders["BFRES"].DisplayCompilationWarning("BFRES");
        }
        public BFRES(string fname) : this()
        {
            Text = Path.GetFileName(fname);
            Read(fname);
        }

        public void Destroy()
        {
            GL.DeleteBuffer(vbo_position);
            GL.DeleteBuffer(vbo_color);
            GL.DeleteBuffer(vbo_nrm);
            GL.DeleteBuffer(vbo_uv0);
            GL.DeleteBuffer(vbo_uv1);
            GL.DeleteBuffer(vbo_weight);
            GL.DeleteBuffer(vbo_bone);
        }
        public void PreRender()
        {
            List<Vector3> vert = new List<Vector3>();
            List<Vector2> u0 = new List<Vector2>();
            List<Vector2> u1 = new List<Vector2>();
            List<Vector4> col = new List<Vector4>();
            List<Vector3> nrm = new List<Vector3>();
            List<Vector4> bone = new List<Vector4>();
            List<Vector4> weight = new List<Vector4>();
            List<int> face = new List<int>();

            int i = 0;
            foreach (FMDL_Model fmdl in models)
            {
                foreach(Mesh m in fmdl.poly) { 
               // Console.WriteLine(m.vertices.Count);
               // Console.WriteLine(m.faces.Count);
                if (m.faces.Count <= 3)
                    continue;
                foreach (Vertex v in m.vertices)
                {
                    vert.Add(v.pos);
                   // col.Add(new Vector4(v.col.X * v.col.W, v.col.Y * v.col.W, v.col.Z * v.col.W, 1f));
                    col.Add(new Vector4(0.9f, 0.9f, 0.9f, 1f)); //Vertex colors disabled atm due to some being used for other effects making the model black
                    nrm.Add(v.nrm);

                        if (v.tx.Count > 0) { 
                            u0.Add(v.tx[0]);
                            //u1.Add(v.tx[1]);
                        }
                        else { 
                            u0.Add(new Vector2(0, 0));
                            //u1.Add(new Vector2(0, 0));
                        }

                        while (v.node.Count < 4)
                    {
                        v.node.Add(0);
                        v.weight.Add(0);
                    }
                    bone.Add(new Vector4(-1, 0, 0, 0));
                    weight.Add(new Vector4(-1, 0, 0, 0));
                  //   bone.Add(new Vector4(v.node[0], v.node[1], v.node[2], v.node[3]));
                   //  weight.Add(new Vector4(v.weight[0], v.weight[1], v.weight[2], v.weight[3]));
                    }

                    foreach (List<int> l in m.faces)
                    {
                        //face.AddRange(l);
                        // rearrange faces
                        int[] ia = l.ToArray();
                        for (int j = 0; j < ia.Length; j++)
                        {
                            ia[j] += i;
                        }
                        face.AddRange(ia);
                    }
                    i += m.vertices.Count;
                }
            }
            vertdata = vert.ToArray();
            coldata = col.ToArray();
            nrmdata = nrm.ToArray();
            uvdata0 = u0.ToArray();
            //uvdata1 = u1.ToArray();
            facedata = face.ToArray();
            bonedata = bone.ToArray();
            weightdata = weight.ToArray();

            if (Runtime.shaders["BFRES"].CompiledSuccessfully())
                SetupShader();

        }

        private void SetupShader()
        {
            int maxUniformBlockSize = GL.GetInteger(GetPName.MaxUniformBlockSize);
            if (shader == null)
            {
                shader = new Shader();
                shader = Runtime.shaders["BFRES"];
            }
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        public void Render(Matrix4 view)
        {
            shader = Runtime.shaders["BFRES"];
            GL.UseProgram(shader.programID);

            shader.enableAttrib();

            GL.UniformMatrix4(shader.getAttribute("modelview"), false, ref view);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_position);
            GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, (IntPtr)(vertdata.Length * Vector3.SizeInBytes), vertdata, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.getAttribute("vPosition"), 3, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_color);
            GL.BufferData<Vector4>(BufferTarget.ArrayBuffer, (IntPtr)(coldata.Length * Vector4.SizeInBytes), coldata, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.getAttribute("vColor"), 4, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_nrm);
            GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, (IntPtr)(nrmdata.Length * Vector3.SizeInBytes), nrmdata, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.getAttribute("vNormal"), 3, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_uv0);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(uvdata0.Length * Vector2.SizeInBytes), uvdata0, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.getAttribute("vUV0"), 2, VertexAttribPointerType.Float, false, 0, 0);

            //GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_uv0);
            //GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(uvdata0.Length * Vector2.SizeInBytes), uvdata0, BufferUsageHint.StaticDraw);
            //GL.VertexAttribPointer(shader.getAttribute("vUV1"), 2, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_bone);
            GL.BufferData<Vector4>(BufferTarget.ArrayBuffer, (IntPtr)(bonedata.Length * Vector4.SizeInBytes), bonedata, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.getAttribute("vBone"), 4, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_weight);
            GL.BufferData<Vector4>(BufferTarget.ArrayBuffer, (IntPtr)(weightdata.Length * Vector4.SizeInBytes), weightdata, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.getAttribute("vWeight"), 4, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo_elements);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(facedata.Length * sizeof(int)), facedata, BufferUsageHint.StaticDraw);

            int indiceat = 0;
            foreach (FMDL_Model fmdl in models)
            {
                Matrix4[] f = fmdl.skeleton.getShaderMatrix();

                int[] bind = fmdl.Node_Array; //Now bind each bone
                GL.UniformMatrix4(BFRES.shader.getAttribute("bones"), f.Length, false, ref f[0].Row0.X);
                if (bind.Length != 0)
                {
                    GL.Uniform1(shader.getAttribute("boneList"), bind.Length, ref bind[0]);
                }

                foreach (Mesh m in fmdl.poly)
                    {
                    GL.Uniform4(shader.getAttribute("colorSamplerUV"), new Vector4(1, 1, 0, 0));

                    if (m.texHashs.Count > 0 ) {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, m.texHashs[0]);
                        GL.Uniform1(shader.getAttribute("tex0"), 0);
                        /*if (m.texHashs.Count == 4)
                        {
                            GL.ActiveTexture(TextureUnit.Texture1);
                            GL.BindTexture(TextureTarget.Texture2D, m.texHashs[3]);
                            GL.Uniform1(shader.getAttribute("tex1"), 1);
                            GL.ActiveTexture(TextureUnit.Texture2);
                            GL.BindTexture(TextureTarget.Texture2D, m.texHashs[2]);
                            GL.Uniform1(shader.getAttribute("spl"), 2);
                            GL.ActiveTexture(TextureUnit.Texture3);
                            GL.BindTexture(TextureTarget.Texture2D, m.texHashs[1]);
                            GL.Uniform1(shader.getAttribute("nrm"), 3);
                        }*/
                    }
                    //GL.BindTexture(TextureTarget.Texture2D, m.texId);
                    //GL.Uniform1(shader.getAttribute("tex"), 0);
                    GL.Disable(EnableCap.CullFace);
                    foreach (List<int> l in m.faces)
                {
                    if (fmdl.isVisible)
                        GL.DrawElements(PrimitiveType.Triangles, l.Count, DrawElementsType.UnsignedInt, indiceat * sizeof(int));

                    indiceat += l.Count;
                }
                }
            }
            shader.disableAttrib();
        }
#endregion
        public static int verNumA, verNumB, verNumC, verNumD, SwitchCheck; 


        public override void Read(string filename)
        {
            FileData f = new FileData(filename);
            f.Endian = Endianness.Big;
            f.seek(0);



            f.seek(4); // magic check
            SwitchCheck = f.readInt(); //Switch version only has padded magic
            verNumD = f.readByte();
            verNumC = f.readByte();
            verNumB = f.readByte();
            verNumA = f.readByte();
            if (SwitchCheck == 0x20202020)
            {
                Console.WriteLine("Version = " + verNumA + "." + verNumB + "." + verNumC + "." + verNumD);
                if (f.readShort() == 0xFEFF)
                    f.Endian = Endianness.Big;
                else f.Endian = Endianness.Little;
                f.skip(2);  //Size Headeer
                f.skip(4); //File Name Direct
                int fileAlignment = f.readInt();
                int RelocationTableOffset = f.readInt();
                int BfresSize = f.readInt();

                string name = f.readString(readOffset(f) + 2, -1);

                f.skip(4); // Padding
                long FMDLOffset = f.readInt64();
                long FMDLDict = f.readInt64();
                long FSKAOffset = f.readInt64();
                long FSKADict = f.readInt64();
                long FMAAOffset = f.readInt64();
                long FMAADict = f.readInt64();
                long FVISOffset = f.readInt64();
                long FVISDict = f.readInt64();
                long FSHUOffset = f.readInt64();
                long FSHUDict = f.readInt64();
                long FSCNOffset = f.readInt64();
                long FSCNDict = f.readInt64();
                long BuffMemPool = f.readInt64();
                long BuffMemPoolInfo = f.readInt64();
                long EMBOffset = f.readInt64();
                long EMBDict = f.readInt64();
                f.skip(8); // Padding
                long StringTableOffset = f.readInt64();
                int unk11 = f.readInt();
                int FMDLCount = f.readShort();
                FSKACount = f.readShort();
                int FMAACount = f.readShort();
                int FVISCount = f.readShort();
                int FSHUCount = f.readShort();
                int FSCNCount = f.readShort();
                int EMBCount = f.readShort();
                f.skip(12); // Padding
                            // Console.WriteLine($"FMDLOffset {FMDLOffset} FMDLCount {FMDLCount} FMDLDict {FMDLDict} FSKAOffset {FSKAOffset} FSKADict {FSKADict}");
                            //  Console.WriteLine($"FMAAOffset {FMAAOffset} FMAADict {FMAADict} FVISOffset {FVISOffset} FSHUOffset {FSKAOffset} FSKADict {FSHUDict}");

                //FMDLs -Models-
                for (int i = 0; i < EMBCount; i++)
                {
                    f.seek((int)EMBOffset + (i * 16));
                    int DataOffset = f.readInt();
                    f.seek(DataOffset);
                    string EmMagic = f.readString(f.pos(), 4);


                    if (EmMagic.Equals("BNTX")) //Textures
                    {
                        int temp = f.pos();
                        BNTX t = new BNTX();
                        t.ReadBNTX(f);
                        Embedded.Nodes.Add(t);

                    }
                }
                f.seek((int)FMDLOffset);
                for (int i = 0; i < FMDLCount; i++)
                {
                    //   Console.WriteLine("Reading FMDL....");

                    FMDL_Model model = new FMDL_Model();



                    //FMDL modelTest = new FMDL();
                    //modelTest.Read(f);
                    f.skip(16);

                    FMDLheader fmdl_info = new FMDLheader
                    {

                        name = f.readString(f.readInt() + 2, -1),
                        padding = f.readInt(),
                        eofString = f.readInt64(),
                        fsklOff = f.readInt64(),
                        fvtxArrOff = f.readInt64(),
                        fshpOffset = f.readInt64(),
                        fshpIndx = f.readInt64(),
                        fmatOffset = f.readInt64(),
                        fmatIndx = f.readInt64(),
                        UserDataOffset = f.readInt64(),
                        padding1 = f.readInt64(),
                        padding2 = f.readInt64(),
                        fvtxCount = f.readShort(),
                        fshpCount = f.readShort(),
                        fmatCount = f.readShort(),
                        paramCount = f.readShort(),
                        VertCount = f.readInt(),
                        unk2 = f.readInt(),
                    };
                    int NextFMDL = f.pos();

                    Models.Nodes.Add(fmdl_info.name);

                    //   Console.WriteLine($" Name {fmdl_info.name} eofString {fmdl_info.eofString} fsklOff {fmdl_info.fsklOff}");
                    //  Console.WriteLine(fmdl_info.fvtxCount);

                    List<FVTXH> FVTXArr = new List<FVTXH>();
                    f.seek((int)fmdl_info.fvtxArrOff);
                    for (int vtx = 0; vtx < fmdl_info.fvtxCount; vtx++)
                    {
                        //   Console.WriteLine("Reading FVTX....");
                        f.skip(16);
                        FVTXArr.Add(new FVTXH
                        {
                            attArrOff = f.readInt64(),
                            attIndxOff = f.readInt64(),
                            unk1 = f.readInt64(),
                            unk2 = f.readInt64(),
                            unk3 = f.readInt64(),
                            buffSizeOff = f.readInt64(),
                            buffStrideSizeOff = f.readInt64(),
                            buffArrOff = f.readInt64(),
                            buffOff = f.readInt(),
                            attCount = f.readByte(),
                            buffCount = f.readByte(),
                            sectIndx = f.readShort(),
                            vertCount = f.readInt(),
                            SkinWeightInfluence = f.readInt()
                        });
                        //  Console.WriteLine($"attCount {FVTXArr[vtx].attCount}");
                    }


                    f.seek((int)fmdl_info.fmatOffset);
                    List<FMATH> FMATheaders = new List<FMATH>();
                    for (int mat = 0; mat < fmdl_info.fmatCount; mat++)
                    {
                        //    Console.WriteLine("Reading FMAT....");
                        f.skip(16);


                        FMATH fmat_info = new FMATH
                        {
                            name = f.readString((int)f.readInt64() + 2, -1),
                            renderInfoOff = f.readInt64(),
                            renderInfoIndx = f.readInt64(),
                            shaderAssignOff = f.readInt64(),
                            u1 = f.readInt64(),
                            texSelOff = f.readInt64(),
                            u2 = f.readInt64(),
                            texAttSelOff = f.readInt64(),
                            texAttIndxOff = f.readInt64(),
                            matParamArrOff = f.readInt64(),
                            matParamIndxOff = f.readInt64(),
                            matParamOff = f.readInt64(),
                            userDataOff = f.readInt64(),
                            userDataIndxOff = f.readInt64(),
                            volatileFlagOffset = f.readInt64(),
                            u3 = f.readInt64(),
                            samplerSlotOff = f.readInt64(),
                            textureSlotOff = f.readInt64(),
                            flags = f.readInt(), //This toggles material visabilty
                            sectIndx = f.readShort(),
                            rendParamCount = f.readShort(),
                            texSelCount = f.readByte(),
                            texAttSelCount = f.readByte(),
                            matParamCount = f.readShort(),
                            u4 = f.readShort(),
                            matParamSize = f.readShort(),
                            rawParamDataSize = f.readShort(),
                            userDataCount = f.readShort(),
                            padding = f.readInt(),

                        };
                        string FMATNameOffset = fmat_info.name;
                        // Console.WriteLine($"{FMATNameOffset} {fmat_info.texSelCount} ");
                        FMATheaders.Add(fmat_info);
                    }

                    f.seek((int)fmdl_info.fsklOff + 16);
                    // Console.WriteLine("Reading FSKL....");
                    FSKLH fskl_info = new FSKLH
                    {
                        boneIndxOff = f.readInt64(),
                        boneArrOff = f.readInt64(),
                        invIndxArrOff = f.readInt64(),
                        invMatrArrOff = f.readInt64(),
                        padding1 = f.readInt64(),
                        fsklType = f.readInt(), //flags
                        boneArrCount = f.readShort(),
                        invIndxArrCount = f.readShort(),
                        exIndxCount = f.readShort(),
                        u1 = f.readInt(),
                    };

                    f.seek((int)fmdl_info.fsklOff + 16);
                    FSKLH fskl_infov8 = new FSKLH
                    {
                        boneIndxOff = f.readInt64(),
                        boneArrOff = f.readInt64(),
                        invIndxArrOff = f.readInt64(),
                        invMatrArrOff = f.readInt64(),
                        padding1 = f.readInt64(),
                        padding2 = f.readInt64(),
                        padding3 = f.readInt64(),
                        fsklType = f.readInt(), //flags
                        boneArrCount = f.readShort(),
                        invIndxArrCount = f.readShort(),
                        exIndxCount = f.readShort(),
                        u1 = f.readInt(),
                    };
                    //  Console.WriteLine($"Bone Count {fskl_info.boneArrCount}");

                    if (verNumB == 8)
                    {
                        model.Node_Array = new int[fskl_infov8.invIndxArrCount + fskl_infov8.exIndxCount];
                        f.seek((int)fskl_infov8.invIndxArrOff);
                        for (int nodes = 0; nodes < fskl_infov8.invIndxArrCount + fskl_infov8.exIndxCount; nodes++)
                        {
                            model.Node_Array[nodes] = (f.readShort());
                        }
                    }
                    else
                    {
                        model.Node_Array = new int[fskl_info.invIndxArrCount + fskl_info.exIndxCount];
                        f.seek((int)fskl_info.invIndxArrOff);
                        for (int nodes = 0; nodes < fskl_info.invIndxArrCount + fskl_info.exIndxCount; nodes++)
                        {
                            model.Node_Array[nodes] = (f.readShort());
                        }
                    }



                    List<FSHPH> FSHPArr = new List<FSHPH>();
                    // Console.WriteLine("Reading FSHP....");
                    f.seek((int)fmdl_info.fshpOffset);
                    for (int shp = 0; shp < fmdl_info.fshpCount; shp++)
                    {
                        f.skip(16);
                        FSHPArr.Add(new FSHPH
                        {
                            polyNameOff = f.readInt(),
                            u1 = f.readInt(),
                            fvtxOff = f.readInt64(),
                            lodMdlOff = f.readInt64(),
                            fsklIndxArrOff = f.readInt64(),
                            u3 = f.readInt64(),
                            u4 = f.readInt64(),
                            boundingBoxOff = f.readInt64(),
                            radiusOff = f.readInt64(),
                            padding = f.readInt64(),
                            flags = f.readInt(),
                            sectIndx = f.readShort(),
                            fmatIndx = f.readShort(),
                            fsklIndx = f.readShort(),
                            fvtxIndx = f.readShort(),
                            fsklIndxArrCount = f.readShort(),
                            matrFlag = f.readByte(),
                            lodMdlCount = f.readByte(),
                            visGrpCount = f.readInt(),
                            visGrpIndxOff = f.readShort(),
                            visGrpNodeOff = f.readShort(),
                        });
                    }
                    int BoneCount;
                    // Console.WriteLine("Reading Bones....");
                    if (verNumB == 8)
                    { f.seek((int)fskl_infov8.boneArrOff); BoneCount = fskl_infov8.boneArrCount; }
                    else
                    { f.seek((int)fskl_info.boneArrOff); BoneCount = fskl_info.boneArrCount; }
                    for (int bn = 0; bn < BoneCount; bn++)
                    {
                        Bone bone = new Bone(model.skeleton);
                        bone.Text = f.readString(f.readInt() + 2, -1);
                        if (verNumB == 8)
                        {
                            f.skip(36);
                        }
                        else
                        {
                            f.skip(20);
                        }
                        bone.boneId = (uint)f.readShort();
                        int parIndx1 = (short)f.readShort();
                        int parIndx2 = f.readShort();
                        int parIndx3 = f.readShort();
                        int parIndx4 = f.readShort();
                        bone.parentIndex = parIndx1;

                        f.skip(6);
                        bone.scale = new float[3];
                        bone.rotation = new float[4];
                        bone.position = new float[3];
                        bone.scale[0] = f.readFloat();
                        bone.scale[1] = f.readFloat();
                        bone.scale[2] = f.readFloat();
                        bone.rotation[0] = f.readFloat();
                        bone.rotation[1] = f.readFloat();
                        bone.rotation[2] = f.readFloat();
                        bone.rotation[3] = f.readFloat();
                        bone.position[0] = f.readFloat();
                        bone.position[1] = f.readFloat();
                        bone.position[2] = f.readFloat();

                        model.skeleton.bones.Add(bone);
                    }
                    model.skeleton.reset();
                    model.skeleton.update();
                    // Console.WriteLine("Reading FSHP Array....");

                    //MeshTime!!
                    TreeNode Shapes = new TreeNode() { Text = "Shapes" };
                    Models.Nodes[i].Nodes.Add(Shapes);
                    for (int m = 0; m < FSHPArr.Count; m++)
                    {

                        Mesh poly = new Mesh();


                        poly.name = f.readString(FSHPArr[m].polyNameOff + 2, -1);

                        Shapes.Nodes.Add(poly.name);
                        //    Console.WriteLine("Polygon = " + poly.name);

                        List<attdata> AttrArr = new List<attdata>();
                        f.seek((int)FVTXArr[FSHPArr[m].fvtxIndx].attArrOff);
                        for (int att = 0; att < FVTXArr[FSHPArr[m].fvtxIndx].attCount; att++)
                        {
                            string AttType = f.readString(f.readInt() + 2, -1);

                            f.skip(4); //padding  
                            f.Endian = Endianness.Big;
                            int vertType = f.readShort();
                            f.skip(2);
                            f.Endian = Endianness.Little;
                            int buffOff = f.readShort();
                            int buffIndx = f.readShort();
                            //   Console.WriteLine($"{AttType} Type = {vertType} Offset = {buffOff} Index = {buffIndx} ");
                            AttrArr.Add(new attdata { attName = AttType, buffIndx = buffIndx, buffOff = buffOff, vertType = vertType });
                        }


                        //Get RLT real quick for buffer offset
                        f.seek(0x18);
                        int RTLOffset = f.readInt();

                        f.seek(RTLOffset);
                        f.skip(0x030);
                        int DataStart = f.readInt();

                        // Console.WriteLine($"RLT {DataStart}");


                        List<buffData> BuffArr = new List<buffData>();
                        f.seek((int)FVTXArr[FSHPArr[m].fvtxIndx].buffArrOff);
                        for (int buff = 0; buff < FVTXArr[FSHPArr[m].fvtxIndx].buffCount; buff++)
                        {
                            buffData data = new buffData();
                            f.seek((int)FVTXArr[FSHPArr[m].fvtxIndx].buffSizeOff + ((buff) * 0x10));
                            data.buffSize = f.readInt();
                            f.seek((int)FVTXArr[FSHPArr[m].fvtxIndx].buffStrideSizeOff + ((buff) * 0x10));
                            data.strideSize = f.readInt();

                            //So these work by grabbing the RLT offset first and then adding the buffer offset. Then they keep adding each other by their buffer sizes
                            if (buff == 0) data.DataOffset = (DataStart + FVTXArr[FSHPArr[m].fvtxIndx].buffOff);
                            if (buff > 0) data.DataOffset = BuffArr[buff - 1].DataOffset + BuffArr[buff - 1].buffSize;
                            if (data.DataOffset % 8 != 0) data.DataOffset = data.DataOffset + (8 - (data.DataOffset % 8));

                            BuffArr.Add(data);
                            //   Console.WriteLine("Data Offset = " + data.DataOffset + " Vertex Buffer Size =" + data.buffSize + " Index = " + buff + " vertexStrideSize size =" + data.strideSize);
                        }

                        for (int v = 0; v < FVTXArr[FSHPArr[m].fvtxIndx].vertCount; v++)
                        {
                            Vertex vert = new Vertex();
                            for (int attr = 0; attr < AttrArr.Count; attr++)
                            {
                                f.seek(((BuffArr[AttrArr[attr].buffIndx].DataOffset) + (AttrArr[attr].buffOff) + (BuffArr[AttrArr[attr].buffIndx].strideSize * v)));
                                switch (AttrArr[attr].attName)
                                {
                                    case "_p0":
                                        if (AttrArr[attr].vertType == 1301)
                                            vert.pos = new Vector3 { X = f.readHalfFloat(), Y = f.readHalfFloat(), Z = f.readHalfFloat() };
                                        if (AttrArr[attr].vertType == 1304)
                                            vert.pos = new Vector3 { X = f.readFloat(), Y = f.readFloat(), Z = f.readFloat() };
                                        break;
                                    case "_c0":
                                        if (AttrArr[attr].vertType == 1301)
                                            vert.col = new Vector4(f.readHalfFloat(), f.readHalfFloat(), f.readHalfFloat(), f.readHalfFloat());
                                        if (AttrArr[attr].vertType == 2067)
                                            vert.col = new Vector4 { X = f.readFloat(), Y = f.readFloat(), Z = f.readFloat(), W = f.readFloat() };
                                        if (AttrArr[attr].vertType == 267)
                                            vert.col = new Vector4(f.readByte() / 128, f.readByte() / 128, f.readByte() / 128, f.readByte() / 128);
                                        break;
                                    case "_n0":
                                        if (AttrArr[attr].vertType == 526)
                                        {
                                            uint normVal = (uint)f.readInt();
                                            //Thanks RayKoopa!
                                            vert.nrm = new Vector3 { X = ((normVal & 0x3FC00000) >> 22) / 511, Y = ((normVal & 0x000FF000) >> 12) / 511, Z = ((normVal & 0x000003FC) >> 2) / 511 };
                                        }
                                        break;
                                    case "_u0":
                                    case "color":
                                    case "_t0":
                                    case "_b0":
                                    case "_u1":
                                    case "_u2":
                                    case "_u3":
                                        if (AttrArr[attr].vertType == 265 || AttrArr[attr].vertType == 521)
                                            vert.tx.Add(new Vector2 { X = ((float)f.readByte()) / 255, Y = ((float)f.readByte()) / 255 });
                                        if (AttrArr[attr].vertType == 274)
                                            vert.tx.Add(new Vector2 { X = ((float)f.readShort()) / 65535, Y = ((float)f.readShort()) / 65535 });
                                        if (AttrArr[attr].vertType == 530)
                                            vert.tx.Add(new Vector2 { X = ((float)f.readShort()) / 32767, Y = ((float)f.readShort()) / 32767 });
                                        if (AttrArr[attr].vertType == 1298)
                                            vert.tx.Add(new Vector2 { X = f.readHalfFloat(), Y = f.readHalfFloat() });
                                        if (AttrArr[attr].vertType == 1303)
                                            vert.tx.Add(new Vector2 { X = f.readFloat(), Y = f.readFloat() });
                                        break;
                                    case "_i0":
                                        if (AttrArr[attr].vertType == 770)
                                        {
                                            vert.node.Add(f.readByte());
                                            vert.weight.Add((float)1.0);
                                        }
                                        if (AttrArr[attr].vertType == 777)
                                        {
                                            vert.node.Add(f.readByte());
                                            vert.node.Add(f.readByte());
                                        }
                                        if (AttrArr[attr].vertType == 779)
                                        {
                                            vert.node.Add(f.readByte());
                                            vert.node.Add(f.readByte());
                                            vert.node.Add(f.readByte());
                                            vert.node.Add(f.readByte());
                                        }
                                        break;
                                    case "_w0":
                                        if (AttrArr[attr].vertType == 265)
                                        {
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                        }
                                        if (AttrArr[attr].vertType == 267)
                                        {
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                        }
                                        break;
                                    default:
                                        //     Console.WriteLine(AttrArr[attr].attName + " Unknown type " + AttrArr[attr].vertType.ToString("x") + " 0x");
                                        break;
                                }
                            }
                            poly.vertices.Add(vert);
                        }
                        int LoadLOD = 0;

                        f.seek((int)FSHPArr[m].lodMdlOff);
                        for (int lod = 0; lod < FSHPArr[m].lodMdlCount; lod++)
                        {
                            long SubMeshOff = f.readInt64();
                            long unk1 = f.readInt64();
                            long unk2 = f.readInt64();
                            long indxBuffOff = f.readInt64();
                            int FaceBuffer = f.readInt();
                            int PrimativefaceType = f.readInt();
                            int faceType = f.readInt();
                            int FaceCount = f.readInt();
                            int elmSkip = f.readInt();
                            int subMeshCount = f.readInt();

                            int temp = f.pos();



                            f.seek(FaceBuffer + DataStart);
                            if (faceType == 1)
                                FaceCount = FaceCount / 3;
                            if (faceType == 2)
                                FaceCount = FaceCount / 6;


                            if (lod == LoadLOD)
                            {
                                for (int face = 0; face < FaceCount; face++)
                                {
                                    if (faceType == 1)
                                        poly.faces.Add(new List<int> { elmSkip + f.readShort(), elmSkip + f.readShort(), elmSkip + f.readShort() });
                                    else if (faceType == 2)
                                        poly.faces.Add(new List<int> { elmSkip + f.readInt(), elmSkip + f.readInt(), elmSkip + f.readInt() });
                                    else
                                        Console.Write("UnkFaceFormat");
                                }
                            }

                            f.seek(temp);
                        }



                        f.seek((int)FMATheaders[FSHPArr[m].fmatIndx].texSelOff);
                        for (int tex = 0; FMATheaders[FSHPArr[m].fmatIndx].texAttSelCount > tex; tex++)
                        {
                            string TextureName = f.readString((int)f.readInt64() + 2, -1);
                            //Console.WriteLine("TexName = " + TextureName);
                            //This works decently for now. I tried samplers but Kirby Star Allies doesn't map with samplers properly? 
                            if (TextureName.Contains("Alb") | TextureName.Contains("alb") | TextureName.Contains("Base") | TextureName.Contains("Eye"))
                            {
                                try
                                {
                                    poly.texHashs.Add(BNTX.textured[TextureName].texture.display);
                                }
                                catch
                                {
                                    poly.texHashs.Add(0);
                                }
                            }
                            foreach (var b in model.skeleton.bones)
                            {
                                BoneFixArray.Add(b.transform);
                            }
                            if (FSHPArr[m].matrFlag == 0)
                            {

                            }
                            else if (FSHPArr[m].matrFlag == 1)
                            {
                                foreach (var b in model.skeleton.bones)
                                {
                                    b.transform = Matrix4.Identity; //Reset bones to 0, 0 ,0 to be replaced to fix wong position models
                                }
                                for (int b = 0; b < model.skeleton.bones.Count; b++)
                                {
                                    //   model.skeleton.bones[b].transform = BoneFixArray[b];
                                }
                            }
                        }

                        model.poly.Add(poly);
                    }
                    models.Add(model);
                    f.seek(NextFMDL);
                    for (int b = 1; models[0].skeleton.bones.Count > b; b++) //This places the bones back but doesn't move the meshes yet. Need to figureo out a fix
                    {
                        models[0].skeleton.bones[b].transform = BoneFixArray[b];
                    }
                }
                PreRender();



            }
            else //If not switch then run as Wii U bfres
            {
                f.seek(0); //Seek back to start of file


                f.seek(4);
                int highVersion = f.readByte();
                int lowVersion = f.readByte();
                int overallVerstion = f.readShort();
                short BOM = (short)f.readShort();
                f.skip(6);
                int fileAlignment = f.readInt();
                string name = f.readString(readOffset(f), -1);
                int strTblLenth = f.readInt();
                int strTblOffset = readOffset(f);

                int FMDLOffset = readOffset(f);
                int FTEXOffset = readOffset(f);
                int FSKAOffset = readOffset(f);
                int FSHU0Offset = readOffset(f);
                int FSHU1Offset = readOffset(f);
                int FSHU2Offset = readOffset(f);
                int FTXPOffset = readOffset(f);
                int FVIS0Offset = readOffset(f);
                int FVIS1Offset = readOffset(f);
                int FSHAOffset = readOffset(f);
                int FSCNOffset = readOffset(f);
                int EMBOffset = readOffset(f);

                int FMDLCount = f.readShort();
                int FTEXCount = f.readShort();
                int FSKACount = f.readShort();
                int FSHU0Count = f.readShort();
                int FSHU1Count = f.readShort();
                int FSHU2Count = f.readShort();
                int FTXPCount = f.readShort();
                int FVIS0Count = f.readShort();
                int FVIS1Count = f.readShort();
                int FSHACount = f.readShort();
                int FSCNCount = f.readShort();
                int EMBCount = f.readShort();

                //FTEX -Texures-
                f.seek(FTEXOffset + 0x18);
                for (int i = 0; i < FTEXCount; i++)
                {
                    f.skip(0x8);
                    string TextureName = f.readString(readOffset(f), -1);
                    int offset = readOffset(f);
                    int NextFTEX = f.pos();
                    f.seek(offset + 8);
                    FTEX texture = new FTEX();
                    texture.ReadFTEX(f);
                    textures.Add(TextureName, texture);
                    f.seek(NextFTEX);
                }

                //FMDLs -Models-
                f.seek(FMDLOffset + 0x18);
                for (int i = 0; i < FMDLCount; i++)
                {

                    FMDL_Model model = new FMDL_Model();


                    f.skip(0xC);
                    int offset = readOffset(f);
                    int NextFMDL = f.pos();
                    f.seek(offset);
                    //FMDL modelTest = new FMDL();
                    //modelTest.Read(f);
                    f.seek(offset + 4);
                    WU.FMDLheader fmdl_info = new WU.FMDLheader
                    {
                        name = f.readString(readOffset(f), -1),
                        eofString = readOffset(f),
                        fsklOff = readOffset(f),
                        fvtxArrOff = readOffset(f),
                        fshpIndx = readOffset(f),
                        fmatIndx = readOffset(f),
                        paramOff = readOffset(f),
                        fvtxCount = f.readShort(),
                        fshpCount = f.readShort(),
                        fmatCount = f.readShort(),
                        paramCount = f.readShort(),

                    };

                    List<WU.FVTXH> FVTXArr = new List<WU.FVTXH>();
                    f.seek(fmdl_info.fvtxArrOff);
                    for (int vtx = 0; vtx < fmdl_info.fvtxCount; vtx++)
                    {
                        f.skip(4);
                        FVTXArr.Add(new WU.FVTXH
                        {
                            attCount = f.readByte(),
                            buffCount = f.readByte(),
                            sectIndx = f.readShort(),
                            vertCount = f.readInt(),
                            u1 = f.readShort(),
                            u2 = f.readShort(),
                            attArrOff = readOffset(f),
                            attIndxOff = readOffset(f),
                            buffArrOff = readOffset(f)
                        });
                        f.skip(4);
                    }


                    f.seek(fmdl_info.fmatIndx + 0x18);
                    List<WU.FMATH> FMATheaders = new List<WU.FMATH>();
                    for (int mat = 0; mat < fmdl_info.fmatCount; mat++)
                    {
                        f.skip(8);
                        string FMATNameOffset = f.readString(readOffset(f), -1);
                        int rtn = f.pos() + 4;
                        f.seek(readOffset(f) + 4);
                        WU.FMATH fmat_info = new WU.FMATH
                        {
                            name = FMATNameOffset,
                            matOff = readOffset(f),
                            u1 = f.readInt(),
                            sectIndx = f.readShort(),
                            rendParamCount = f.readShort(),
                            texSelCount = f.readByte(),
                            texAttSelCount = f.readByte(),
                            matParamCount = f.readShort(),
                            matParamSize = f.readInt(),
                            u2 = f.readInt(),
                            rendParamIndx = readOffset(f),
                            unkMatOff = readOffset(f),
                            shadeOff = readOffset(f),
                            texSelOff = readOffset(f),
                            texAttSelOff = readOffset(f),
                            texAttIndxOff = readOffset(f),
                            matParamArrOff = readOffset(f),
                            matParamIndxOff = readOffset(f),
                            matParamOff = readOffset(f),
                            shadParamIndxOff = readOffset(f)
                        };
                        f.seek(rtn);
                        FMATheaders.Add(fmat_info);
                    }

                    f.seek(fmdl_info.fsklOff + 6);
                    WU.FSKLH fskl_info = new WU.FSKLH
                    {
                        fsklType = f.readShort(),
                        boneArrCount = f.readShort(),
                        invIndxArrCount = f.readShort(),
                        exIndxCount = f.readShort(),
                        u1 = f.readShort(),
                        boneIndxOff = readOffset(f),
                        boneArrOff = readOffset(f),
                        invIndxArrOff = readOffset(f),
                        invMatrArrOff = readOffset(f)
                    };

                    model.Node_Array = new int[fskl_info.invIndxArrCount];
                    f.seek(fskl_info.invIndxArrOff);
                    for (int nodes = 0; nodes < fskl_info.invIndxArrCount; nodes++)
                    {
                        model.Node_Array[i] = (f.readShort());
                    }

                    List<WU.FSHPH> FSHPArr = new List<WU.FSHPH>();
                    f.seek(fmdl_info.fshpIndx + 24);
                    for (int shp = 0; shp < fmdl_info.fshpCount; shp++)
                    {
                        f.skip(12);
                        int rtn2 = f.pos() + 4;
                        f.seek(readOffset(f) + 4);
                        FSHPArr.Add(new WU.FSHPH
                        {
                            polyNameOff = readOffset(f),
                            u1 = f.readInt(),
                            fvtxIndx = f.readShort(),
                            fmatIndx = f.readShort(),
                            fsklIndx = f.readShort(),
                            sectIndx = f.readShort(),
                            fsklIndxArrCount = f.readShort(),
                            matrFlag = f.readByte(),
                            lodMdlCount = f.readByte(),
                            visGrpCount = f.readInt(),
                            u3 = f.readInt(),
                            fvtxOff = readOffset(f),
                            lodMdlOff = readOffset(f),
                            fsklIndxArrOff = readOffset(f),
                            u4 = f.readInt(),
                            visGrpNodeOff = readOffset(f),
                            visGrpRangeOff = readOffset(f),
                            visGrpIndxOff = readOffset(f)
                        });
                        f.seek(rtn2);
                    }

                    f.seek(fskl_info.boneArrOff);
                    for (int bn = 0; bn < fskl_info.boneArrCount; bn++)
                    {
                        Bone bone = new Bone(model.skeleton);
                        bone.Text = f.readString(readOffset(f), -1);
                        bone.boneId = (uint)f.readShort();
                        int parIndx1 = (short)f.readShort();
                        int parIndx2 = f.readShort();
                        int parIndx3 = f.readShort();
                        int parIndx4 = f.readShort();
                        bone.parentIndex = parIndx1;

                        f.skip(6);
                        bone.scale = new float[3];
                        bone.rotation = new float[4];
                        bone.position = new float[3];
                        bone.scale[0] = f.readFloat();
                        bone.scale[1] = f.readFloat();
                        bone.scale[2] = f.readFloat();
                        bone.rotation[0] = f.readFloat();
                        bone.rotation[1] = f.readFloat();
                        bone.rotation[2] = f.readFloat();
                        bone.rotation[3] = f.readFloat();
                        bone.position[0] = f.readFloat();
                        bone.position[1] = f.readFloat();
                        bone.position[2] = f.readFloat();
                        f.skip(4);

                        model.skeleton.bones.Add(bone);

                        if (lowVersion < 4)
                            f.skip(48);
                    }
                    model.skeleton.reset();


                    //MeshTime!!
                    for (int m = 0; m < FSHPArr.Count; m++)
                    {
                        Mesh poly = new Mesh();


                        poly.name = f.readString(FSHPArr[m].polyNameOff, -1);

                        List<attdata> AttrArr = new List<attdata>();
                        f.seek(FVTXArr[FSHPArr[m].fvtxIndx].attArrOff);
                        for (int att = 0; att < FVTXArr[FSHPArr[m].fvtxIndx].attCount; att++)
                        {
                            string AttType = f.readString(readOffset(f), -1);
                            int buffIndx = f.readByte();
                            f.skip(1);
                            int buffOff = f.readShort();
                            int vertType = f.readInt();
                            AttrArr.Add(new attdata { attName = AttType, buffIndx = buffIndx, buffOff = buffOff, vertType = vertType });
                        }


                        List<WU.buffData> BuffArr = new List<WU.buffData>();
                        f.seek(FVTXArr[FSHPArr[m].fvtxIndx].buffArrOff);
                        for (int buf = 0; buf < FVTXArr[FSHPArr[m].fvtxIndx].buffCount; buf++)
                        {
                            WU.buffData data = new WU.buffData();
                            f.skip(4);
                            data.buffSize = f.readInt();
                            f.skip(4);
                            data.strideSize = f.readShort();
                            f.skip(6);
                            data.dataOffset = readOffset(f);
                            BuffArr.Add(data);
                        }
                        for (int v = 0; v < FVTXArr[FSHPArr[m].fvtxIndx].vertCount; v++)
                        {
                            Vertex vert = new Vertex();
                            for (int attr = 0; attr < AttrArr.Count; attr++)
                            {
                                f.seek(((BuffArr[AttrArr[attr].buffIndx].dataOffset) + (AttrArr[attr].buffOff) + (BuffArr[AttrArr[attr].buffIndx].strideSize * v)));
                                switch (AttrArr[attr].attName)
                                {
                                    case "_p0":
                                        if (AttrArr[attr].vertType == 2063)
                                            vert.pos = new Vector3 { X = f.readHalfFloat(), Y = f.readHalfFloat(), Z = f.readHalfFloat() };
                                        if (AttrArr[attr].vertType == 2065)
                                            vert.pos = new Vector3 { X = f.readFloat(), Y = f.readFloat(), Z = f.readFloat() };
                                        break;
                                    case "_c0":
                                        if (AttrArr[attr].vertType == 2063)
                                            vert.col = new Vector4(f.readHalfFloat(), f.readHalfFloat(), f.readHalfFloat(), f.readHalfFloat());
                                        if (AttrArr[attr].vertType == 2067)
                                            vert.col = new Vector4 { X = f.readFloat(), Y = f.readFloat(), Z = f.readFloat(), W = f.readFloat() };
                                        if (AttrArr[attr].vertType == 10)
                                            vert.col = new Vector4(f.readByte() / 128, f.readByte() / 128, f.readByte() / 128, f.readByte() / 128);
                                        break;
                                    case "_n0":
                                        if (AttrArr[attr].vertType == 0x20b)
                                        {
                                            uint normVal = (uint)f.readInt();
                                            //Thanks RayKoopa!
                                            vert.nrm = new Vector3 { X = ((normVal & 0x3FC00000) >> 22) / 511, Y = ((normVal & 0x000FF000) >> 12) / 511, Z = ((normVal & 0x000003FC) >> 2) / 511 };
                                        }
                                        break;
                                    case "_u0":
                                    case "color":
                                    case "_u1":
                                    case "_u2":
                                    case "_u3":
                                        if (AttrArr[attr].vertType == 4 || AttrArr[attr].vertType == 516)
                                            vert.tx.Add(new Vector2 { X = ((float)f.readByte()) / 255, Y = ((float)f.readByte()) / 255 });
                                        if (AttrArr[attr].vertType == 7)
                                            vert.tx.Add(new Vector2 { X = ((float)f.readShort()) / 65535, Y = ((float)f.readShort()) / 65535 });
                                        if (AttrArr[attr].vertType == 519)
                                            vert.tx.Add(new Vector2 { X = ((float)f.readShort()) / 32767, Y = ((float)f.readShort()) / 32767 });
                                        if (AttrArr[attr].vertType == 2056)
                                            vert.tx.Add(new Vector2 { X = f.readHalfFloat(), Y = f.readHalfFloat() });
                                        if (AttrArr[attr].vertType == 2061)
                                            vert.tx.Add(new Vector2 { X = f.readFloat(), Y = f.readFloat() });
                                        break;
                                    case "_i0":
                                        if (AttrArr[attr].vertType == 256)
                                        {
                                            vert.node.Add(f.readByte());
                                            vert.weight.Add((float)1.0);
                                        }
                                        if (AttrArr[attr].vertType == 260)
                                        {
                                            vert.node.Add(f.readByte());
                                            vert.node.Add(f.readByte());
                                        }
                                        if (AttrArr[attr].vertType == 266)
                                        {
                                            vert.node.Add(f.readByte());
                                            vert.node.Add(f.readByte());
                                            vert.node.Add(f.readByte());
                                            vert.node.Add(f.readByte());
                                        }
                                        break;
                                    case "_w0":
                                        if (AttrArr[attr].vertType == 4)
                                        {
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                        }
                                        if (AttrArr[attr].vertType == 10)
                                        {
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                            vert.weight.Add(((float)f.readByte()) / 255);
                                        }
                                        break;
                                }
                            }
                            poly.vertices.Add(vert);
                        }

                        f.seek(FSHPArr[m].lodMdlOff + 4);
                        int faceType = f.readInt();
                        f.skip(0xC);
                        int indxBuffOff = readOffset(f) + 4;
                        int elmSkip = f.readInt();
                        f.seek(indxBuffOff);
                        int FaceCount = f.readInt();
                        f.skip(0xC);
                        f.seek(readOffset(f));
                        if (faceType == 4)
                            FaceCount = FaceCount / 6;
                        if (faceType == 9)
                            FaceCount = FaceCount / 12;
                        for (int face = 0; face < FaceCount; face++)
                        {
                            if (faceType == 4)
                                poly.faces.Add(new List<int> { elmSkip + f.readShort(), elmSkip + f.readShort(), elmSkip + f.readShort() });
                            else if (faceType == 9)
                                poly.faces.Add(new List<int> { elmSkip + f.readInt(), elmSkip + f.readInt(), elmSkip + f.readInt() });
                            else
                                Console.Write("UnkFaceFormat");

                        }

                        f.seek(FMATheaders[FSHPArr[m].fmatIndx].texSelOff);
                        for (int tex = 0; FMATheaders[FSHPArr[m].fmatIndx].texAttSelCount > tex; tex++)
                        {
                            string TextureName = f.readString(readOffset(f), -1);
                            try
                            {
                                poly.texHashs.Add(textures[TextureName].texture.display);
                            }
                            catch
                            {
                                poly.texHashs.Add(0);
                            }
                            f.skip(4);
                        }
                        model.poly.Add(poly);
                    }
                    models.Add(model);
                    f.seek(NextFMDL);
                }
                PreRender();
            }
        }

        public override byte[] Rebuild()
        {
            throw new NotImplementedException();
        }

        public class FMDLheader
        {
            public string magic;
            public int headerLength1;
            public long headerLength2;
            public string name;
            public long eofString;
            public long fsklOff;
            public long fvtxArrOff;
            public long fvtxOff;
            public long matrOff;
            public long fshpOffset;
            public long fshpIndx;
            public long fmatOffset;
            public long fmatIndx;
            public long UserDataOffset;
            public int fvtxCount;
            public int fshpCount;
            public int fmatCount;
            public int paramCount;
            public int VertCount;
            public int un;
            public int unk2;
            public int padding;
            public long padding1;
            public long padding2;
            public int padding3;
        }
        public class FVTXH
        {
            public int magic = 0x46565458;//FVTX
            public int attCount;
            public int buffCount;
            public int sectIndx;
            public int vertCount;
            public int u1;
            public long attArrOff;
            public long attIndxOff;
            public long buffArrOff;
            public long buffSizeOff;
            public long buffStrideSizeOff;
            public int buffOff;
            public long unk1;
            public long unk2;
            public long unk3;
            public int SkinWeightInfluence;
        }
        public class FMATH
        {
            public string name;
            public long renderInfoOff;
            public long renderInfoIndx;
            public long shaderAssignOff;
            public long u1;
            public long texSelOff;
            public long u2;
            public long texAttSelOff;
            public long texAttIndxOff;
            public long matParamArrOff;
            public long matParamIndxOff;
            public long matParamOff;
            public long userDataOff;
            public long userDataIndxOff;
            public int padding;
            public long volatileFlagOffset;
            public long u3;
            public long samplerSlotOff;
            public long textureSlotOff;
            public int flags;
            public int sectIndx;
            public int rendParamCount;
            public int texSelCount;
            public int texAttSelCount;
            public int matParamCount;
            public int matParamSize;
            public int rawParamDataSize;
            public int userDataCount;
            public int u4;
        }
        public class FSKLH
        {
            public string magic;
            public int HeaderLength1;
            public long HeaderLenght2;
            public long boneIndxOff;
            public long boneArrOff;
            public long invMatrArrOff;
            public long invIndxArrOff;
            public int fsklType; //Flags
            public int boneArrCount;
            public int invIndxArrCount;
            public int exIndxCount;
            public long padding1;
            public long padding2;
            public long padding3;
            public long padding4;
            public long padding5;
            public int u1;
        }
        public class FSHPH
        {
            public string Magic;
            public int polyNameOff;
            public long fvtxOff;
            public long lodMdlOff;
            public long fsklIndxArrOff; //Skin bone index list
            public long boundingBoxOff;
            public long radiusOff;
            public int flags;
            public int sectIndx;
            public int matrFlag;
            public int fmatIndx;
            public int fsklIndx;
            public int fvtxIndx;
            public int u1;
            public int fsklIndxArrCount;
            public int lodMdlCount;
            public int visGrpCount;
            public long u3;
            public long u4;
            public int visGrpNodeOff;
            public int visGrpRangeOff;
            public int visGrpIndxOff;
            public long padding;
            public int VertexSkinCount;
            public int padding2;
            public int[] Node_Array;
        }
        public class attdata
        {
            public string attName;
            public int buffIndx;
            public int buffOff;
            public int vertType;
        }
        public class buffData
        {
            public int buffSize;
            public int strideSize;
            public int DataOffset;
        }
        public class lodmdl
        {
            public int u1;
            public int faceType;
            public int dCount;
            public int visGrpCount;
            public int u3;
            public int visGrpOff;
            public int indxBuffOff;
            public int elmSkip;
        }
        public class Vertex
        {
            public Vector3 pos = new Vector3(0, 0, 0), nrm = new Vector3(0, 0, 0);
            public Vector4 col = new Vector4(2, 2, 2, 2);
            public List<Vector2> tx = new List<Vector2>();
            public List<int> node = new List<int>();
            public List<float> weight = new List<float>();
        }
        public class Mesh
        {
            public List<List<int>> faces = new List<List<int>>();
            public List<Vertex> vertices = new List<Vertex>();
            public List<int> texHashs = new List<int>();
            public string name;
        }
        public class FMDL_Model
        {
            private VBN vbn = new VBN();
            public VBN skeleton
            {
                get
                {
                    return vbn;
                }
                set
                {
                    vbn = value;
                }
            }
            public List<Mesh> poly = new List<Mesh>();
            public bool isVisible = true;
            public int[] Node_Array;
        }

        public class WU //Wii U BFRES Parse. Temp till i mess with Syroots lib
        {
            public class FMDLheader
            {
                public string FMDL;
                public string name;
                public int eofString;
                public int fsklOff;
                public int fvtxArrOff;
                public int fshpIndx;
                public int fmatIndx;
                public int paramOff;
                public int fvtxCount;
                public int fshpCount;
                public int fmatCount;
                public int paramCount;
            }
            public class FVTXH
            {
                public int attCount;
                public int buffCount;
                public int sectIndx;
                public int vertCount;
                public int u1;
                public int u2;
                public int attArrOff;
                public int attIndxOff;
                public int buffArrOff;
            }
            public class FMATH
            {
                public string name;
                public int matOff;
                public int u1;
                public int sectIndx;
                public int rendParamCount;
                public int texSelCount;
                public int texAttSelCount;
                public int matParamCount;
                public int matParamSize;
                public int u2;
                public int rendParamIndx;
                public int unkMatOff;
                public int shadeOff;
                public int texSelOff;
                public int texAttSelOff;
                public int texAttIndxOff;
                public int matParamArrOff;
                public int matParamIndxOff;
                public int matParamOff;
                public int shadParamIndxOff;
            }
            public class FSKLH
            {
                public int fsklType;
                public int boneArrCount;
                public int invIndxArrCount;
                public int exIndxCount;
                public int u1;
                public int boneIndxOff;
                public int boneArrOff;
                public int invIndxArrOff;
                public int invMatrArrOff;
            }
            public class FSHPH
            {
                public int polyNameOff;
                public int u1;
                public int fvtxIndx;
                public int fmatIndx;
                public int fsklIndx;
                public int sectIndx;
                public int fsklIndxArrCount;
                public int matrFlag;
                public int lodMdlCount;
                public int visGrpCount;
                public int u3;
                public int fvtxOff;
                public int lodMdlOff;
                public int fsklIndxArrOff;
                public int u4;
                public int visGrpNodeOff;
                public int visGrpRangeOff;
                public int visGrpIndxOff;
            }
            public class attdata
            {
                public string attName;
                public int buffIndx;
                public int buffOff;
                public int vertType;
            }
            public class buffData
            {
                public int buffSize;
                public int strideSize;
                public int dataOffset;
            }
            public class lodmdl
            {
                public int u1;
                public int faceType;
                public int dCount;
                public int visGrpCount;
                public int u3;
                public int visGrpOff;
                public int indxBuffOff;
                public int elmSkip;
            }
            public class Vertex
            {
                public Vector3 pos = new Vector3(0, 0, 0), nrm = new Vector3(0, 0, 0);
                public Vector4 col = new Vector4(2, 2, 2, 2);
                public List<Vector2> tx = new List<Vector2>();
                public List<int> node = new List<int>();
                public List<float> weight = new List<float>();
            }
            public class Mesh
            {
                public List<List<int>> faces = new List<List<int>>();
                public List<Vertex> vertices = new List<Vertex>();
                public List<int> texHashs = new List<int>();
                public string name;
            }
            public class FMDL_Model
            {
                public VBN skeleton
                {
                    get
                    {
                        return vbn;
                    }
                    set
                    {
                        vbn = value;
                    }
                }
                private VBN vbn = new VBN();
                public List<Mesh> poly = new List<Mesh>();
                public bool isVisible = true;
            }
        }

        public void readFSKA()
        {

        }


    }
}