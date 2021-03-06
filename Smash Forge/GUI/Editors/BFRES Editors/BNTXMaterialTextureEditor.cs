﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SFGraphics.GLObjects.Textures;

namespace SmashForge
{
    public partial class BntxMaterialTextureEditor : Form
    {
        //This editor will allow UV viewing while
        //also being able to edit tiling, filtering and some other things.


        public Texture texture = null;
        public bool loaded;
        public Matrix4 mvpMatrix = Matrix4.Identity;
        public BFRES.Mesh mesh = null;
        public BFRES.MaterialData mat = null;
        public BFRES.MatTexture mattex = null;

        public Vector3 position = new Vector3(0, 0, 0);
        public float mouseSLast = 0;
        public float mouseYLast = 0;
        public float mouseXLast = 0;
        public float mouseTranslateSpeed = 0.0005f;

        public BntxMaterialTextureEditor()
        {
            InitializeComponent();
            comboBox1.Items.Add("Repeat");
            comboBox1.Items.Add("Mirror");
            comboBox1.Items.Add("Clamp");
            comboBox1.Items.Add("Repeat Mirror");


            comboBox2.Items.Add("Repeat");
            comboBox2.Items.Add("Mirror");
            comboBox2.Items.Add("Clamp");
            comboBox2.Items.Add("Repeat Mirror");
        }
        public void LoadTexture(BFRES.Mesh m, Texture texture, string texName)
        {
            //Todo go back and redo this editor
          //  RenderTexture(m, texture, TexName);
        }

        float zoom = -1f;

        private void RenderTexture(BFRES.Mesh m, Texture texture, string texName)
        {
            this.texture = texture;
            mesh = m;
            mat = mesh.material;

            foreach (BFRES.MatTexture te in mat.textures)
            {
                if (this.texture != null)
                {
                    if (te.Name == texName)
                    {
                        mattex = te;
                        comboBox1.SelectedIndex = te.wrapModeS;
                        comboBox2.SelectedIndex = te.wrapModeT;
                    }
                }       
            }
        }
        public void Setup2DRendering()
        {
            // Setup OpenGL settings for basic 2D rendering.
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            int w = glControl1.Width;
            int h = glControl1.Height;

            float orthoW = w * zoom;
            float orthoH = h * zoom;

            GL.Viewport(0, 0, w, h);
            //    GL.Ortho(-w * zoom, w * zoom, -h * zoom, h * zoom, -1, 1);
            //      GL.Translate(position.X, -position.Y, 0);

            Matrix4 v = Matrix4.CreateTranslation(position.X, -position.Y, zoom) * Matrix4.CreatePerspectiveFieldOfView(1.3f, glControl1.Width / (float)glControl1.Height, 0.1f, 1000000);

            GL.MatrixMode(MatrixMode.Projection);

            GL.LoadMatrix(ref v);

            //       GL.Ortho(-1.5 + zoom, 1.0 - zoom, -2.0 + zoom, 0.5 - zoom, -1.0, 3.5); // Changed some of the signs here


            //         GL.MatrixMode(MatrixMode.Projection);
            //    GL.LoadMatrix(ref translation);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            // Allow for alpha blending.
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        public struct DisplayVertex
        {
            // Used for rendering.
            public Vector3 pos;
            public Vector2 texcoord;

            public static int size = 4 * (3 + 2);
        }
        private void SetupCursorXyz()
        {
            mouseXLast = OpenTK.Input.Mouse.GetState().X;
            mouseYLast = OpenTK.Input.Mouse.GetState().Y;
            mouseSLast = OpenTK.Input.Mouse.GetState().WheelPrecise;

        }
        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (zoom >= -1)
            {
                if (e.Delta > 0 && zoom > -1000) zoom -= 0.1f;
                if (e.Delta < 0 && zoom < -0.2) zoom += 0.1f;
            }
            else
            {
                if (e.Delta > 0 && zoom > -1000) zoom -= 0.3f;
                if (e.Delta < 0 && zoom < -0.2) zoom += 0.3f;
            }

   

            Console.WriteLine(zoom);

        //    Setup2DRendering();
            glControl1.Invalidate();
        }

        private void glControl1_Load(object sender, EventArgs e)
        {
            loaded = true;
            GL.ClearColor(Color.SkyBlue); 

          //  Setup2DRendering();
        }

        private void glControl1_Resize(object sender, EventArgs e)
        {
            if (!loaded)
                return;

            base.OnResize(e);
            glControl1.MakeCurrent();

       

            // Setup2DRendering();
            glControl1.Invalidate();
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (!loaded || glControl1 == null)
                return;

            glControl1.MakeCurrent();

            GL.ClearColor(Color.DimGray);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            GL.Enable(EnableCap.DepthTest);
            GL.ClearDepth(1.0);


            GL.Disable(EnableCap.Texture2D);

            int w = glControl1.Width;
            int h = glControl1.Height;

            Setup2DRendering();

            DrawTex2();
            //   DrawFloor();
            DrawUVs(mesh);

            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            glControl1.SwapBuffers();


            
        }

        public static float width;
        public static float height;

        public void DrawTex2()
        {
            if (this.texture == null && mattex == null)
            {
                glControl1.SwapBuffers();
                return;
            }


            GL.Enable(EnableCap.Texture2D);

            int texture = 0;
     


            // Single texture uniform.
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapmode[mattex.wrapModeS]);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapmode[mattex.wrapModeT]);

            float scaleX = 16f;
            float scaleY = 16f;
            float posX = 1f;
            float posY = 0.5f;
            float uVposX = 1f;
            float uVposY = 0.5f;
            float tileX = 32;
            float tileY = 32;

            if (width > height)
            {
                float scale = width / height;
             //   TileX = TileX * scale;
                scaleX = scaleX * scale;
                uVposX = uVposX * scale;
            }
            if (height > width)
            {
                float scale = height / width;
             //   TileY = TileY * scale;
                scaleY = scaleY * scale;
                uVposY = uVposY * scale;
            }

            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(1 * tileX + uVposX, 1 * tileY + uVposY);
            GL.Vertex2(1 * scaleX + posX, -1 * scaleY + posY);
            GL.TexCoord2(0 * tileX + uVposX, 1 * tileY + uVposY);
            GL.Vertex2(-1 * scaleX + posX, -1 * scaleY + posY);
            GL.TexCoord2(0 * tileX + uVposX, 0 * tileY + uVposY);
            GL.Vertex2(-1 * scaleX + posX, 1 * scaleY + posY);
            GL.TexCoord2(1 * tileX + uVposX, 0 * tileY + uVposY);
            GL.Vertex2(1 * scaleX + posX, 1 * scaleY + posY);
            GL.End();

        }
        static Dictionary<int, TextureWrapMode> wrapmode = new Dictionary<int, TextureWrapMode>(){
                    { 0x00, TextureWrapMode.Repeat},
                    { 0x01, TextureWrapMode.MirroredRepeat},
                    { 0x02, TextureWrapMode.ClampToEdge},
                    { 0x03, TextureWrapMode.MirroredRepeat},
        };

        public static void DrawUVs(BFRES.Mesh p)
        {
            List<int> f = p.lodMeshes[p.DisplayLODIndex].getDisplayFace();
            GL.Disable(EnableCap.Texture2D);

            float scaleX = 1;
            float scaleY = 1;

            if (width > height)
            {
                scaleX = width / height;
            }
            if (height > width)
            {
                scaleY = height / width;
            }


            GL.Scale(scaleX, scaleY, 1);
            int divisionsX = (int)scaleY * 4;
            int divisionsY = (int)scaleX * 4;

            for (int i = 0; i < p.lodMeshes[p.DisplayLODIndex].displayFaceSize; i += 3)
            {
                BFRES.Vertex v1 = p.vertices[f[i]];
                BFRES.Vertex v2 = p.vertices[f[i + 1]];
                BFRES.Vertex v3 = p.vertices[f[i + 2]];

                if (Runtime.uvChannel == Runtime.UvChannel.Channel1)
                    BFRES_DrawUVTriangleAndGrid(v1.uv0, v2.uv0, v3.uv0, divisionsX, divisionsY, Color.Red, 1, Color.White, p.material);
                else if (Runtime.uvChannel == Runtime.UvChannel.Channel2)
                    BFRES_DrawUVTriangleAndGrid(v1.uv1, v2.uv1, v3.uv1, divisionsX, divisionsY, Color.Red, 1, Color.White, p.material);
                else if (Runtime.uvChannel == Runtime.UvChannel.Channel3)
                    BFRES_DrawUVTriangleAndGrid(v1.uv2, v2.uv2, v3.uv2, divisionsX, divisionsY, Color.Red, 1, Color.White, p.material);
            }
        }
        private static void BFRES_DrawUVTriangleAndGrid(Vector2 v1, Vector2 v2, Vector2 v3, int divisionsX, int divisionsY, Color uvColor, float lineWidth, Color gridColor, BFRES.MaterialData mat)
        {
            // No shaders
            GL.UseProgram(0);

            float bounds = 1;
            Vector2 scaleUv = new Vector2(1, 1);
            Vector2 transUv = new Vector2(0, 0);

            if (Runtime.uvChannel == Runtime.UvChannel.Channel2)
            {
                if (mat.matparam.ContainsKey("gsys_bake_st0"))
                {
                    scaleUv = mat.matparam["gsys_bake_st0"].Value_float4.Xy;
                    transUv = mat.matparam["gsys_bake_st0"].Value_float4.Zw;
                }
            }


            SetupUvRendering(lineWidth);

            BFRES_DrawUvTriangle(v1, v2, v3, uvColor, scaleUv, transUv);

            // Draw Grid
            GL.Color3(gridColor);
            DrawHorizontalGrid(divisionsX, bounds, scaleUv);
            DrawVerticalGrid(divisionsY, bounds, scaleUv);
        }
        private static void DrawHorizontalGrid(int divisions, float bounds, Vector2 scaleUv)
        {
            int horizontalCount = divisions;
            for (int i = 0; i < horizontalCount * bounds + 1; i++)
            {
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex2(new Vector2(0, (1.0f / horizontalCount) * i) * scaleUv);
                GL.Vertex2(new Vector2(1, (1.0f / horizontalCount) * i) * scaleUv);
                GL.End();
            }
        }
        private static void DrawVerticalGrid(int divisions, float bounds, Vector2 scaleUv)
        {
            int verticalCount = divisions;
            for (int i = 0; i < verticalCount * bounds + 1; i++)
            {
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex2(new Vector2((1.0f / verticalCount) * i, 0) * scaleUv);
                GL.Vertex2(new Vector2((1.0f / verticalCount) * i, 1) * scaleUv);
                GL.End();
            }
        }
        private static void BFRES_DrawUvTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color uvColor, Vector2 scaleUv, Vector2 transUv)
        {
            GL.Color3(uvColor);
            GL.Begin(PrimitiveType.Lines);
            GL.Vertex2(v1 * scaleUv + transUv);
            GL.Vertex2(v2 * scaleUv + transUv);
            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Vertex2(v2 * scaleUv + transUv);
            GL.Vertex2(v3 * scaleUv + transUv);
            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Vertex2(v3 * scaleUv + transUv);
            GL.Vertex2(v1 * scaleUv + transUv);
            GL.End();
        }
        private static void SetupUvRendering(float lineWidth)
        {
            // Go to 2D
       /*     GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0, 1, 1, 0, 0, 1);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadIdentity();*/
            GL.LineWidth(lineWidth);

            // Draw over everything
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Clear(ClearBufferMask.DepthBufferBit);
        }
        public static void DrawFloor()
        {

            GL.PushMatrix();
            int grdiSize = 50;
            int gridGap = 2;
            GL.Rotate(90f, 1, 0, 0);
            GL.LineWidth(1f);
            GL.Color3(Color.LightGray);
            GL.Begin(PrimitiveType.Lines);
            for (var i = -grdiSize; i <= grdiSize; i++)
            {
                GL.Vertex3(new Vector3(-grdiSize * gridGap, 0f, i * gridGap));
                GL.Vertex3(new Vector3(grdiSize * gridGap, 0f, i * gridGap));
                GL.Vertex3(new Vector3(i * gridGap, 0f, -grdiSize * gridGap));
                GL.Vertex3(new Vector3(i * gridGap, 0f, grdiSize * gridGap));
            }
            GL.End();
            GL.Color3(Color.Transparent);

        }

        private void glControl1_MouseUp(object sender, MouseEventArgs e)
        {

            // Left click drag to rotate. Right click drag to pan.
            if (e.Button == MouseButtons.Right)
            {
                position.Y += mouseTranslateSpeed * (e.Y - mouseYLast);
                position.X += mouseTranslateSpeed * (e.X - mouseXLast);
            }

            SetupCursorXyz();
            glControl1.Invalidate();
        }

        // Vertical
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox2.SelectedItem != null)
            {
                mattex.wrapModeT = comboBox2.SelectedIndex;
            }
            glControl1.Invalidate();
        }

        // Horizontal
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem != null)
            {
                mattex.wrapModeS = comboBox1.SelectedIndex;
            }
            glControl1.Invalidate();
        }

        private void BNTXMaterialTextureEditor_Load(object sender, EventArgs e)
        {

        }
    }
}
