﻿using System.Windows.Forms;
using Smash_Forge.Rendering;
using OpenTK.Graphics.OpenGL;
using Smash_Forge.Rendering.Meshes;
using SFGraphics.GLObjects.Shaders;
using OpenTK;
using SFGraphics.GLObjects.Textures;
using SFGraphics.GLObjects.Framebuffers;
using SFGraphics.GLObjects.GLObjectManagement;

namespace Smash_Forge.GUI.Menus
{
    public partial class LMUVViewer : Form
    {
        public Lumen lumen;
        public NUT nut;
        public int shapeIndex, graphicIndex;
        public Matrix4 a;
        public Framebuffer pngExportFramebuffer;
        Mesh3D screenTriangle;

        public LMUVViewer(Lumen Lm, NUT Nut, int ShapeIndex, int GraphicIndex)
        {
            lumen = Lm;
            nut = Nut;
            shapeIndex = ShapeIndex;
            graphicIndex = GraphicIndex;
            
            InitializeComponent();
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            RenderTexture();
            DrawPolygonUvs();
            GLObjectManager.DeleteUnusedGLObjects();
        }

        private void RenderTexture()
        {
            int hash;
            glControl1.MakeCurrent();

            GL.Viewport(glControl1.ClientRectangle);

            if (lumen.Endian == 0)
            {
                foreach (NutTexture nutTexture in nut.Nodes)
                {
                    hash = nutTexture.HashId;
                    Texture texture = nut.glTexByHashId[hash];
                    int width = nutTexture.Width;
                    int height = nutTexture.Height;

                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

                    ScreenDrawing.DrawTexturedQuad(texture, width, height, screenTriangle);
                }
                glControl1.SwapBuffers();
            }
            else
            {
                foreach (NutTexture nutTexture in nut.Nodes)
                {
                    hash = nutTexture.HashId;
                    Texture texture = nut.glTexByHashId[hash];
                    int width = nutTexture.Width;
                    int height = nutTexture.Height;

                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

                    ScreenDrawing.DrawTexturedQuad(texture, width, height, screenTriangle);
                }
                glControl1.SwapBuffers();
            }
        }

        private void DrawPolygonUvs()
        {
            Shader shader = OpenTKSharedResources.shaders["UV"];
            shader.UseProgram();
            Matrix4 matrix = Matrix4.CreateOrthographicOffCenter(0, 1, 1, 0, -1, 1);
            shader.SetMatrix4x4("mvpMatrix", ref matrix);
            // lumen.Shapes[shapeIndex].Graphics[graphicIndex].Verts

            /* foreach (Lumen.Vertex vert in lumen.Shapes[shapeIndex].Graphics[graphicIndex].Verts)
            {
                
            } */
        }

        private void glControl1_Load(object sender, System.EventArgs e)
        {
            OpenTKSharedResources.InitializeSharedResources();
            if (OpenTKSharedResources.SetupStatus == OpenTKSharedResources.SharedResourceStatus.Initialized)
            {
                nut.RefreshGlTexturesByHashId();
                pngExportFramebuffer = new Framebuffer(FramebufferTarget.Framebuffer, glControl1.Width, glControl1.Height);
                screenTriangle = ScreenDrawing.CreateScreenTriangle();
            }
        }
    }
}
