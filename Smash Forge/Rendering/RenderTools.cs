﻿using System;
using System.IO;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Reflection;
using SALT.PARAMS;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using SFGraphics.GLObjects.Textures;
using SFGraphics.GLObjects.Shaders;
using SFGraphics.GLObjects;
using SFGraphics.Cameras;


namespace Smash_Forge.Rendering
{
    class RenderTools
    {
        public static Texture defaultTex;
        public static Texture floorTexture;
        public static Texture backgroundTexture;

        // A triangle that extends past the screen.
        // Avoids the need for a second triangle to fill a rectangular screen.
        public static BufferObject screenQuadVbo;
        private static float[] screenQuadVertices = 
        {
            -1f, -1f, 0.0f,
             3f, -1f, 0.0f,
            -1f,  3f, 0.0f
        };

        public static Dictionary<NUD.DummyTextures, Texture> dummyTextures = new Dictionary<NUD.DummyTextures, Texture>(); 

        public static Texture uvTestPattern;
        public static Texture boneWeightGradient;
        public static Texture boneWeightGradient2;

        // Nud Material Sphere Textures.
        public static Texture sphereDifTex;
        public static Texture sphereNrmMapTex;

        // Nud Material Sphere Vert Attribute Textures.
        private static Texture sphereNrmTex;
        private static Texture sphereUvTex;
        private static Texture sphereTanTex;
        private static Texture sphereBitanTex;
        private static ForgeCamera nudSphereCamera = new ForgeCamera();

        // Keep a context around to avoid setting up after making each context.
        public static GameWindow dummyResourceWindow;
        private static bool hasSetUpOpenTK = false;

        public static void SetUpOpenTkRendering()
        {
            if (hasSetUpOpenTK)
                return;

            // Make a dummy context so shaders, textures, etc don't become unloaded.
            GraphicsContext.ShareContexts = true;
            SetUpDummyResourceContext();

            nudSphereCamera.UpdateFromMouse(); // Update matrices for shader.
            LoadTextures();
            screenQuadVbo = CreateScreenQuadBuffer();
            GetOpenGLSystemInfo();
            ShaderTools.SetupShaders();

            hasSetUpOpenTK = true;
        }

        private static void SetUpDummyResourceContext()
        {
            GraphicsMode mode = new GraphicsMode(new ColorFormat(8, 8, 8, 8), 24, 0, 0, ColorFormat.Empty, 1);
            dummyResourceWindow = new GameWindow(640, 480, mode, "", OpenTK.GameWindowFlags.Default, OpenTK.DisplayDevice.Default, 3, 3, GraphicsContextFlags.Default);
            dummyResourceWindow.Visible = false;
            dummyResourceWindow.MakeCurrent();
        }

        public static BufferObject CreateScreenQuadBuffer()
        {
            // Create buffer for vertex positions. The data won't change, so only initialize once.
            BufferObject screenQuad = new BufferObject(BufferTarget.ArrayBuffer);
            screenQuad.Bind();
            GL.BufferData(screenQuad.BufferTarget, (IntPtr)(sizeof(float) * screenQuadVertices.Length), 
                screenQuadVertices, BufferUsageHint.StaticDraw);
            return screenQuad;
        }

        public static void LoadTextures()
        {
            dummyTextures = CreateNudDummyTextures();

            LoadMaterialSphereTextures();

            // Helpful textures. 
            uvTestPattern = new Texture2D(Properties.Resources.UVPattern);
            uvTestPattern.TextureWrapS = TextureWrapMode.Repeat;
            uvTestPattern.TextureWrapT = TextureWrapMode.Repeat;

            boneWeightGradient = new Texture2D(Properties.Resources.boneWeightGradient);
            boneWeightGradient2 = new Texture2D(Properties.Resources.boneWeightGradient2);

            defaultTex = new Texture2D(Resources.Resources.DefaultTexture);

            try
            {
                floorTexture = new Texture2D(new Bitmap(Runtime.floorTexFilePath));
                backgroundTexture = new Texture2D(new Bitmap(Runtime.backgroundTexFilePath));
            }
            catch (Exception)
            {
                // File paths are incorrect or never set. 
            }
        }

        public static Dictionary<NUD.DummyTextures, Texture> CreateNudDummyTextures()
        {
            Dictionary<NUD.DummyTextures, Texture> dummyTextures = new Dictionary<NUD.DummyTextures, Texture>();

            // Dummy textures. 
            Texture stageMapHigh = new TextureCubeMap(Properties.Resources._10102000, 128);
            dummyTextures.Add(NUD.DummyTextures.StageMapHigh, stageMapHigh);

            Texture stageMapLow = new TextureCubeMap(Properties.Resources._10101000, 128);
            dummyTextures.Add(NUD.DummyTextures.StageMapLow, stageMapLow);

            Texture dummyRamp = new Texture2D(Properties.Resources._10080000);
            dummyTextures.Add(NUD.DummyTextures.DummyRamp, dummyRamp);

            Texture pokemonStadiumDummyTex = new Texture2D(Properties.Resources._10040001);
            dummyTextures.Add(NUD.DummyTextures.PokemonStadium, pokemonStadiumDummyTex);

            Texture punchOutDummyTex = new Texture2D(Properties.Resources._10040000);
            dummyTextures.Add(NUD.DummyTextures.PunchOut, punchOutDummyTex);

            Texture shadowMapDummyTex = new Texture2D(Properties.Resources._10100000);
            dummyTextures.Add(NUD.DummyTextures.ShadowMap, shadowMapDummyTex);

            return dummyTextures;
        }

        public static void LoadMaterialSphereTextures()
        {
            // Sphere Default Textures.
            sphereDifTex = new Texture2D(Properties.Resources.defaultDif);
            sphereNrmMapTex = new Texture2D(Properties.Resources.nrmMap);
            // Sphere Mesh Attributes.
            sphereNrmTex = new Texture2D(Properties.Resources.nrm);
            sphereUvTex = new Texture2D(Properties.Resources.uv);
            sphereTanTex = new Texture2D(Properties.Resources.tan);
            sphereBitanTex = new Texture2D(Properties.Resources.bitan);
        }

        private static void GetOpenGLSystemInfo()
        {
            Runtime.renderer = GL.GetString(StringName.Renderer);
            Runtime.openGLVersion = GL.GetString(StringName.Version);
            Runtime.GLSLVersion = GL.GetString(StringName.ShadingLanguageVersion);
        }

        public static void drawTranslator(Matrix4 view)
        {
            Vector3 center = new Vector3(5, 10, 5);

            // check if within range
            {
                Vector3 p1 = Vector3.TransformPosition(center, view).Normalized();
                Vector3 p2 = Vector3.TransformPosition(center + new Vector3(0, 5, 0), view).Normalized();

                // check if mouse is within range
                
            }

            GL.Color3(Color.Green);
            GL.LineWidth(1f);
            GL.Begin(PrimitiveType.Lines);
            GL.Vertex3(center);
            GL.Vertex3(center + new Vector3(0, 5, 0));
            GL.End();

            GL.Color3(Color.Red);
            GL.LineWidth(1f);
            GL.Begin(PrimitiveType.Lines);
            GL.Vertex3(center);
            GL.Vertex3(center + new Vector3(5, 0, 0));
            GL.End();

            GL.Color3(Color.Blue);
            GL.LineWidth(1f);
            GL.Begin(PrimitiveType.Lines);
            GL.Vertex3(center);
            GL.Vertex3(center + new Vector3(0, 0, 5));
            GL.End();
        }

        #region Taken from Brawllib render TKContext.cs
        public static void drawSphere(Vector3 center, float radius, uint precision)
        {

            if (radius < 0.0f)
                radius = -radius;

            if (radius == 0.0f)
                return;

            if (precision == 0)
                return;

            float halfPI = (float)(Math.PI * 0.5);
            float oneThroughPrecision = 1.0f / precision;
            float twoPIThroughPrecision = (float)(Math.PI * 2.0 * oneThroughPrecision);

            float theta1, theta2, theta3;
            Vector3 norm = new Vector3(), pos = new Vector3();

            for (uint j = 0; j < precision / 2; j++)
            {
                theta1 = (j * twoPIThroughPrecision) - halfPI;
                theta2 = ((j + 1) * twoPIThroughPrecision) - halfPI;

                GL.Begin(PrimitiveType.TriangleStrip);
                for (uint i = 0; i <= precision; i++)
                {
                    theta3 = i * twoPIThroughPrecision;

                    norm.X = (float)(Math.Cos(theta2) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta2);
                    norm.Z = (float)(Math.Cos(theta2) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * (j + 1) * oneThroughPrecision);
                    GL.Vertex3(pos.X, pos.Y, pos.Z);

                    norm.X = (float)(Math.Cos(theta1) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta1);
                    norm.Z = (float)(Math.Cos(theta1) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * j * oneThroughPrecision);
                    GL.Vertex3(pos.X, pos.Y, pos.Z);
                }
                GL.End();
            }
        }

        public static void drawWireframeSphere(Vector3 center, float radius, uint precision)
        {

            if (radius < 0.0f)
                radius = -radius;

            if (radius == 0.0f)
                return;

            if (precision == 0)
                return;

            float halfPI = (float)(Math.PI * 0.5);
            float oneThroughPrecision = 1.0f / precision;
            float twoPIThroughPrecision = (float)(Math.PI * 2.0 * oneThroughPrecision);

            float theta1, theta2, theta3;
            Vector3 norm = new Vector3(), pos = new Vector3();

            for (uint j = 0; j < precision / 2; j++)
            {
                theta1 = (j * twoPIThroughPrecision) - halfPI;
                theta2 = ((j + 1) * twoPIThroughPrecision) - halfPI;

                GL.Begin(PrimitiveType.LineStrip);
                for (uint i = 0; i <= precision; i++)
                {
                    theta3 = i * twoPIThroughPrecision;

                    norm.X = (float)(Math.Cos(theta2) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta2);
                    norm.Z = (float)(Math.Cos(theta2) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * (j + 1) * oneThroughPrecision);
                    GL.Vertex3(pos.X, pos.Y, pos.Z);

                    norm.X = (float)(Math.Cos(theta1) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta1);
                    norm.Z = (float)(Math.Cos(theta1) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * j * oneThroughPrecision);
                    GL.Vertex3(pos.X, pos.Y, pos.Z);
                }
                GL.End();
            }
        }

        public static void beginTopLevelStencil()
        {
            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);
        }

        // The same as beginTopLevelStencil but writes 0s instead of 1s
        // Also it does not clear the stencil buffer
        public static void beginTopLevelAntiStencil()
        {
            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 0, 0xFF);
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.DepthTest);
            GL.ColorMask(false, false, false, false);
        }

        public static void endTopLevelStencilAndDraw()
        {
            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, 1, 0xFF);
            GL.StencilMask(0x00);

            drawSphere(Vector3.Zero, 100, 10);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
        }

        public static void resetStencil()
        {
            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, 1, 0xFF);
            GL.StencilMask(0x00);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
        }

        public static void drawSphereTransformedVisible(Vector3 center, float radius, uint precision, Matrix4 transform)
        {
            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);

               drawSphereTransformed(center, radius, precision, transform);

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, 1, 0xFF);
            GL.StencilMask(0x00);
            GL.Disable(EnableCap.CullFace);

            drawSphere(Vector3.Zero, 100, 10);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Enable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
        }

        public static void drawWireframeSphereTransformedVisible(Vector3 center, float radius, uint precision, Matrix4 transform)
        {
            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);

            drawWireframeSphereTransformed(center, radius, precision, transform);

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, 1, 0xFF);
            GL.StencilMask(0x00);
            GL.Disable(EnableCap.CullFace);

            drawSphere(Vector3.Zero, 100, 10);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Enable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
        }

        public static void drawSphereTransformed(Vector3 center, float radius, uint precision, Matrix4 transform)
        {
            if (radius < 0.0f)
                radius = -radius;

            if (radius == 0.0f)
                return;

            if (precision == 0)
                return;

            float halfPI = (float)(Math.PI * 0.5);
            float oneThroughPrecision = 1.0f / precision;
            float twoPIThroughPrecision = (float)(Math.PI * 2.0 * oneThroughPrecision);

            float theta1, theta2, theta3;
            Vector3 norm = new Vector3(), pos = new Vector3();

            for (uint j = 0; j < precision / 2; j++)
            {
                theta1 = (j * twoPIThroughPrecision) - halfPI;
                theta2 = ((j + 1) * twoPIThroughPrecision) - halfPI;

                GL.Begin(PrimitiveType.TriangleStrip);
                for (uint i = 0; i <= precision; i++)
                {
                    theta3 = i * twoPIThroughPrecision;

                    norm.X = (float)(Math.Cos(theta2) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta2);
                    norm.Z = (float)(Math.Cos(theta2) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * (j + 1) * oneThroughPrecision);
                    GL.Vertex3(Vector3.TransformPosition(new Vector3(pos.X, pos.Y, pos.Z), transform));

                    norm.X = (float)(Math.Cos(theta1) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta1);
                    norm.Z = (float)(Math.Cos(theta1) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * j * oneThroughPrecision);
                    GL.Vertex3(Vector3.TransformPosition(new Vector3(pos.X, pos.Y, pos.Z), transform));
                }
                GL.End();
            }
        }

        public static void drawWireframeSphereTransformed(Vector3 center, float radius, uint precision, Matrix4 transform)
        {
            if (radius < 0.0f)
                radius = -radius;

            if (radius == 0.0f)
                return;

            if (precision == 0)
                return;

            float halfPI = (float)(Math.PI * 0.5);
            float oneThroughPrecision = 1.0f / precision;
            float twoPIThroughPrecision = (float)(Math.PI * 2.0 * oneThroughPrecision);

            float theta1, theta2, theta3;
            Vector3 norm = new Vector3(), pos = new Vector3();

            for (uint j = 0; j < precision / 2; j++)
            {
                theta1 = (j * twoPIThroughPrecision) - halfPI;
                theta2 = ((j + 1) * twoPIThroughPrecision) - halfPI;

                GL.Begin(PrimitiveType.LineStrip);
                for (uint i = 0; i <= precision; i++)
                {
                    theta3 = i * twoPIThroughPrecision;

                    norm.X = (float)(Math.Cos(theta2) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta2);
                    norm.Z = (float)(Math.Cos(theta2) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * (j + 1) * oneThroughPrecision);
                    GL.Vertex3(Vector3.TransformPosition(new Vector3(pos.X, pos.Y, pos.Z), transform));

                    norm.X = (float)(Math.Cos(theta1) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta1);
                    norm.Z = (float)(Math.Cos(theta1) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * j * oneThroughPrecision);
                    GL.Vertex3(Vector3.TransformPosition(new Vector3(pos.X, pos.Y, pos.Z), transform));
                }
                GL.End();
            }
        }

        public static void DrawHitboxCylinder(Vector3 p1, Vector3 p2, float R)
        {
            Vector3 yAxis = new Vector3(0, 1, 0);
            Vector3 d = p2 - p1;
            float height = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z) / 2;

            Vector3 mid = (p1 + p2) / 2;  // midpoint

            Vector3 axis = Vector3.Cross(d, yAxis);
            float angle = (float)Math.Acos(Vector3.Dot(d.Normalized(), yAxis));
            

            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);

            drawSphere(p1, R, 20);
            drawSphere(p2, R, 20);

            //  sides
            GL.PushMatrix();
            GL.Translate(mid);
            GL.Rotate(-(float)(angle * (180 / Math.PI)), axis);

            GL.Begin(PrimitiveType.QuadStrip);
            for (int j = 0; j <= 8 * 3; j += 1)
            {
                GL.Vertex3((float)Math.Cos(j) * R, +height, (float)Math.Sin(j) * R);
                GL.Vertex3((float)Math.Cos(j) * R, -height, (float)Math.Sin(j) * R);
            }
            GL.End();

            GL.PopMatrix();

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, 1, 0xFF);
            GL.StencilMask(0x00);
            GL.Disable(EnableCap.CullFace);

            drawSphere(Vector3.Zero, 100, 10);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Enable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
        }

        public static void drawReducedCylinderTransformed(Vector3 p1, Vector3 p2, float R, Matrix4 transform)
        {
            Vector3 yAxis = new Vector3(0, 1, 0);
            Vector3 d = p2 - p1;
            float height = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z) / 2;

            Vector3 mid = (p1 + p2) / 2;

            Vector3 axis = Vector3.Cross(d, yAxis);
            float angle = (float)Math.Acos(Vector3.Dot(d.Normalized(), yAxis));
            
            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);
                
            drawSphereTransformed(p1, R, 20, transform);
            drawSphereTransformed(p2, R, 20, transform);

            //  sides
            GL.PushMatrix();

            GL.MultMatrix(ref transform);
            GL.Translate(mid);
            GL.Rotate(-(float)(angle * (180 / Math.PI)), axis);

            GL.Begin(PrimitiveType.QuadStrip);
            for (int j = 0; j <= 8 * 3; j += 1)
            {
                GL.Vertex3((float)Math.Cos(j) * R, +height, (float)Math.Sin(j) * R);
                GL.Vertex3((float)Math.Cos(j) * R, -height, (float)Math.Sin(j) * R);
            }
            GL.End();
        
            GL.PopMatrix();

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, 1, 0xFF);
            GL.StencilMask(0x00);
            GL.Disable(EnableCap.CullFace);

            drawSphere(Vector3.Zero, 100, 10);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Enable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
        }

        public static void drawWireframeCylinderTransformed(Vector3 p1, Vector3 p2, float R, Matrix4 transform)
        {
            Vector3 yAxis = new Vector3(0, 1, 0);
            Vector3 d = p2 - p1;
            float height = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z) / 2;

            Vector3 mid = (p1 + p2) / 2;

            Vector3 axis = Vector3.Cross(d, yAxis);
            float angle = (float)Math.Acos(Vector3.Dot(d.Normalized(), yAxis));

            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);

            drawWireframeSphereTransformed(p1, R, 10, transform);
            drawWireframeSphereTransformed(p2, R, 10, transform);

            //  sides
            GL.PushMatrix();

            double[] f = new double[] {
                transform.M11, transform.M12, transform.M13, transform.M14,
                transform.M21, transform.M22, transform.M23, transform.M24,
                transform.M31, transform.M32, transform.M33, transform.M34,
                transform.M41, transform.M42, transform.M43, transform.M44,
            };
            GL.MultMatrix(f);
            GL.Translate(mid);
            GL.Rotate(-(float)(angle * (180 / Math.PI)), axis);

            GL.Begin(PrimitiveType.LineStrip);
            for (int j = 0; j <= 8 * 3; j += 1)
            {
                GL.Vertex3((float)Math.Cos(j) * R, +height, (float)Math.Sin(j) * R);
                GL.Vertex3((float)Math.Cos(j) * R, -height, (float)Math.Sin(j) * R);
            }
            GL.End();

            GL.PopMatrix();

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, 1, 0xFF);
            GL.StencilMask(0x00);
            GL.Disable(EnableCap.CullFace);

            drawSphere(Vector3.Zero, 100, 10);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Enable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
        }

        public static void DrawCylinder(Vector3 p1, Vector3 p2, float R){
            int q = 8, p = 20;

            Vector3 yAxis = new Vector3 (0, 1, 0);
            Vector3 d = p2 - p1;
            float height = (float)Math.Sqrt (d.X*d.X + d.Y*d.Y + d.Z*d.Z) / 2;

            Vector3 mid = (p1 + p2) / 2;

            Vector3 axis = Vector3.Cross (d, yAxis);
            float angle = (float)Math.Acos (Vector3.Dot(d.Normalized(), yAxis));

            GL.PushMatrix ();
            GL.Translate(p1);
            GL.Rotate (-(float)((angle) * (180/Math.PI)), axis);
            for(int j = 0; j < q; j++)
            {
                GL.Begin(PrimitiveType.TriangleStrip);
                for(int i = 0; i <= p; i++)
                {
                    GL.Vertex3( R * Math.Cos( (float)(j+1)/q * Math.PI/2.0 ) * Math.Cos( 2.0 * (float)i/p * Math.PI ),
                        -R * Math.Sin( (float)(j+1)/q * Math.PI/2.0 ),
                        R * Math.Cos( (float)(j+1)/q * Math.PI/2.0 ) * Math.Sin( 2.0 * (float)i/p * Math.PI ) );
                    GL.Vertex3( R * Math.Cos( (float)j/q * Math.PI/2.0 ) * Math.Cos( 2.0 * (float)i/p * Math.PI ),
                        -R * Math.Sin( (float)j/q * Math.PI/2.0 ),
                        R * Math.Cos( (float)j/q * Math.PI/2.0 ) * Math.Sin( 2.0 * (float)i/p * Math.PI ) );         
                }
                GL.End();
            }
            GL.PopMatrix ();

            GL.PushMatrix ();
            GL.Translate(p2);
            GL.Rotate (-(float)(angle * (180/Math.PI)), axis);
            for(int j = 0; j < q; j++)
            {
                GL.Begin(PrimitiveType.TriangleStrip);
                for(int i = 0; i <= p; i++)
                {
                    GL.Vertex3( R * Math.Cos( (float)(j+1)/q * Math.PI/2.0 ) * Math.Cos( 2.0 * (float)i/p * Math.PI ),
                        R * Math.Sin( (float)(j+1)/q * Math.PI/2.0 ),
                        R * Math.Cos( (float)(j+1)/q * Math.PI/2.0 ) * Math.Sin( 2.0 * (float)i/p * Math.PI ) );
                    GL.Vertex3( R * Math.Cos( (float)j/q * Math.PI/2.0 ) * Math.Cos( 2.0 * (float)i/p * Math.PI ),
                        R * Math.Sin( (float)j/q * Math.PI/2.0 ),
                        R * Math.Cos( (float)j/q * Math.PI/2.0 ) * Math.Sin( 2.0 * (float)i/p * Math.PI ) );         
                }
                GL.End();
            }
            GL.PopMatrix ();


            /*  sides */
            GL.PushMatrix ();

            GL.Translate(mid);
            GL.Rotate (-(float)(angle * (180/Math.PI)), axis);

            GL.Begin(PrimitiveType.QuadStrip);
            for (int j=0;j<=360;j+=1) {
                GL.Vertex3((float)Math.Cos(j)*R,+height, (float)Math.Sin(j)*R);
                GL.Vertex3((float)Math.Cos(j)*R,-height, (float)Math.Sin(j)*R);
            }
            GL.End();

            GL.PopMatrix ();
        }

        public static void DrawWireframeCylinder(Vector3 p1, Vector3 p2, float R)
        {
            int q = 8, p = 20;

            Vector3 yAxis = new Vector3(0, 1, 0);
            Vector3 d = p2 - p1;
            float height = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z) / 2;

            Vector3 mid = (p1 + p2) / 2;

            Vector3 axis = Vector3.Cross(d, yAxis);
            float angle = (float)Math.Acos(Vector3.Dot(d.Normalized(), yAxis));

            GL.PushMatrix();
            GL.Translate(p1);
            GL.Rotate(-(float)((angle) * (180 / Math.PI)), axis);
            for (int j = 0; j < q; j++)
            {
                GL.Begin(PrimitiveType.LineStrip);
                for (int i = 0; i <= p; i++)
                {
                    GL.Vertex3(R * Math.Cos((float)(j + 1) / q * Math.PI / 2.0) * Math.Cos(2.0 * (float)i / p * Math.PI),
                        -R * Math.Sin((float)(j + 1) / q * Math.PI / 2.0),
                        R * Math.Cos((float)(j + 1) / q * Math.PI / 2.0) * Math.Sin(2.0 * (float)i / p * Math.PI));
                    GL.Vertex3(R * Math.Cos((float)j / q * Math.PI / 2.0) * Math.Cos(2.0 * (float)i / p * Math.PI),
                        -R * Math.Sin((float)j / q * Math.PI / 2.0),
                        R * Math.Cos((float)j / q * Math.PI / 2.0) * Math.Sin(2.0 * (float)i / p * Math.PI));
                }
                GL.End();
            }
            GL.PopMatrix();

            GL.PushMatrix();
            GL.Translate(p2);
            GL.Rotate(-(float)(angle * (180 / Math.PI)), axis);
            for (int j = 0; j < q; j++)
            {
                GL.Begin(PrimitiveType.LineStrip);
                for (int i = 0; i <= p; i++)
                {
                    GL.Vertex3(R * Math.Cos((float)(j + 1) / q * Math.PI / 2.0) * Math.Cos(2.0 * (float)i / p * Math.PI),
                        R * Math.Sin((float)(j + 1) / q * Math.PI / 2.0),
                        R * Math.Cos((float)(j + 1) / q * Math.PI / 2.0) * Math.Sin(2.0 * (float)i / p * Math.PI));
                    GL.Vertex3(R * Math.Cos((float)j / q * Math.PI / 2.0) * Math.Cos(2.0 * (float)i / p * Math.PI),
                        R * Math.Sin((float)j / q * Math.PI / 2.0),
                        R * Math.Cos((float)j / q * Math.PI / 2.0) * Math.Sin(2.0 * (float)i / p * Math.PI));
                }
                GL.End();
            }
            GL.PopMatrix();


            /*  sides */
            GL.PushMatrix();

            GL.Translate(mid);
            GL.Rotate(-(float)(angle * (180 / Math.PI)), axis);

            GL.Begin(PrimitiveType.LineStrip);
            for (int j = 0; j <= 45; j += 1)
            {
                GL.Vertex3((float)Math.Cos(j) * R, +height, (float)Math.Sin(j) * R);
                GL.Vertex3((float)Math.Cos(j) * R, -height, (float)Math.Sin(j) * R);
            }
            GL.End();

            GL.PopMatrix();
        }

        //Alternate drawCylinder method that tries to keep opacity uniform by reducing sides iterations, used for hurtboxes so model can still be visible
        public static void drawReducedSidesCylinder(Vector3 p1, Vector3 p2, float R)
        {
            int q = 8, p = 20;

            Vector3 yAxis = new Vector3(0, 1, 0);
            Vector3 d = p2 - p1;
            float height = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z) / 2;

            Vector3 mid = (p1 + p2) / 2;

            Vector3 axis = Vector3.Cross(d, yAxis);
            float angle = (float)Math.Acos(Vector3.Dot(d.Normalized(), yAxis));

            GL.PushMatrix();
            GL.Translate(p1);
            GL.Rotate(-(float)((angle) * (180 / Math.PI)), axis);
            for (int j = 0; j < q; j++)
            {
                GL.Begin(PrimitiveType.TriangleStrip);
                for (int i = 0; i <= p; i++)
                {
                    GL.Vertex3(R * Math.Cos((float)(j + 1) / q * Math.PI / 2.0) * Math.Cos(2.0 * (float)i / p * Math.PI),
                        -R * Math.Sin((float)(j + 1) / q * Math.PI / 2.0),
                        R * Math.Cos((float)(j + 1) / q * Math.PI / 2.0) * Math.Sin(2.0 * (float)i / p * Math.PI));
                    GL.Vertex3(R * Math.Cos((float)j / q * Math.PI / 2.0) * Math.Cos(2.0 * (float)i / p * Math.PI),
                        -R * Math.Sin((float)j / q * Math.PI / 2.0),
                        R * Math.Cos((float)j / q * Math.PI / 2.0) * Math.Sin(2.0 * (float)i / p * Math.PI));
                }
                GL.End();
            }
            GL.PopMatrix();

            GL.PushMatrix();
            GL.Translate(p2);
            GL.Rotate(-(float)(angle * (180 / Math.PI)), axis);
            for (int j = 0; j < q; j++)
            {
                GL.Begin(PrimitiveType.TriangleStrip);
                for (int i = 0; i <= p; i++)
                {
                    GL.Vertex3(R * Math.Cos((float)(j + 1) / q * Math.PI / 2.0) * Math.Cos(2.0 * (float)i / p * Math.PI),
                        R * Math.Sin((float)(j + 1) / q * Math.PI / 2.0),
                        R * Math.Cos((float)(j + 1) / q * Math.PI / 2.0) * Math.Sin(2.0 * (float)i / p * Math.PI));
                    GL.Vertex3(R * Math.Cos((float)j / q * Math.PI / 2.0) * Math.Cos(2.0 * (float)i / p * Math.PI),
                        R * Math.Sin((float)j / q * Math.PI / 2.0),
                        R * Math.Cos((float)j / q * Math.PI / 2.0) * Math.Sin(2.0 * (float)i / p * Math.PI));
                }
                GL.End();
            }
            GL.PopMatrix();


            /*  sides */
            GL.PushMatrix();

            GL.Translate(mid);
            GL.Rotate(-(float)(angle * (180 / Math.PI)), axis);

            GL.Begin(PrimitiveType.QuadStrip);
            for (int j = 0; j <= q * 3; j += 1) //Reduced iterations to make quadstrips do a cylinder but keeping opacity low
            {
                GL.Vertex3(Math.Cos(j) * R, +height, Math.Sin(j) * R);
                GL.Vertex3(Math.Cos(j) * R, -height, Math.Sin(j) * R);
            }
            GL.End();

            GL.PopMatrix();
        }

        public static void draw2DCircle(float x, float y, float radius, Color color, int screenWidth, int screenHeight)
        {

            // No shaders
            GL.UseProgram(0);

            // Go to 2D
            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0.0f, screenWidth, screenHeight, 0.0f, -1.0f, 10.0f);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadIdentity();

            // Allow transparency
            GL.Enable(EnableCap.Blend);

            // Draw over everything
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Draw here
            GL.Color4(color);
            uint precision = 30;  // force particular method overload
            DrawCircle(new Vector3(x, y, -1f), radius, precision);

            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);

            // Back to 3D
            GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix();
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix();
        }

        public static void DrawFloor(Matrix4 mvpMatrix)
        {
            float scale = Runtime.floorSize;

            GL.UseProgram(0);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref mvpMatrix);

            // objects shouldn't show through opaque parts of floor
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.Color3(Runtime.floorColor);
            GL.LineWidth(1f);

            // THe user textured mode is currently broken.
            if (Runtime.floorStyle == Runtime.FloorStyle.UserTexture)
            {
                GL.Enable(EnableCap.Texture2D);
                GL.ActiveTexture(TextureUnit.Texture0);
                if (Runtime.floorStyle == Runtime.FloorStyle.UserTexture)
                    floorTexture.Bind();
                else
                    defaultTex.Bind();

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)Runtime.floorWrap);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)Runtime.floorWrap);

                GL.Color3(Runtime.floorColor == Color.Gray ? Color.White : Runtime.floorColor);
                GL.Begin(PrimitiveType.Quads);

                GL.TexCoord2(0, 0);
                GL.Vertex3(new Vector3(-scale, 0f, -scale));
                GL.TexCoord2(0, 2);
                GL.Vertex3(new Vector3(-scale, 0f, scale));
                GL.TexCoord2(2, 2);
                GL.Vertex3(new Vector3(scale, 0f, scale));
                GL.TexCoord2(2, 0);
                GL.Vertex3(new Vector3(scale, 0f, -scale));

                GL.End();
                GL.Disable(EnableCap.Texture2D);
            }
            else if (Runtime.floorStyle == Runtime.FloorStyle.Solid)
            {
                GL.Begin(PrimitiveType.Quads);
                GL.Vertex3(-scale, 0f, -scale);
                GL.Vertex3(-scale, 0f, scale);
                GL.Vertex3(scale, 0f, scale);
                GL.Vertex3(scale, 0f, -scale);
                GL.End();
            }
            else
            {
                GL.Begin(PrimitiveType.Lines);
                for (var i = -scale / 2; i <= scale / 2; i++)
                {
                    if (i != 0)
                    {
                        GL.Vertex3(-scale, 0f, i * 2);
                        GL.Vertex3(scale, 0f, i * 2);
                        GL.Vertex3(i * 2, 0f, -scale);
                        GL.Vertex3(i * 2, 0f, scale);
                    }
                }
                GL.End();
            }

            if (Runtime.renderFloorLines)
            {
                GL.Disable(EnableCap.DepthTest);
                GL.Begin(PrimitiveType.Lines);
                GL.Color3(Color.White);
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(-scale, 0f, 0);
                GL.Vertex3(scale, 0f, 0);
                GL.Vertex3(0, 0f, -scale);
                GL.Vertex3(0, 0f, scale);
                GL.End();
                GL.Enable(EnableCap.DepthTest);

                GL.Disable(EnableCap.DepthTest);
                GL.Color3(Color.LightGray);
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(0, 5, 0);
                GL.Vertex3(0, 0, 0);

                GL.Color3(Color.OrangeRed);
                GL.Vertex3(0f, 0f, 0);
                GL.Color3(Color.OrangeRed);
                GL.Vertex3(5f, 0f, 0);

                GL.Color3(Color.Olive);
                GL.Vertex3(0, 0f, 0f);
                GL.Color3(Color.Olive);
                GL.Vertex3(0, 0f, 5f);

                GL.End();
            }

            GL.Enable(EnableCap.DepthTest);
        }

        public static bool PolyContainsTextureHash(int selectedTextureHash, NUD.Polygon poly)
        {
            foreach (NUD.Material material in poly.materials)
            {
                foreach (NUD.MatTexture matTex in material.textures)
                {
                    if (selectedTextureHash == matTex.hash)
                        return true;
                }
            }

            return false;
        }

        private static void DrawUVTriangleAndGrid(NUD.Vertex v1, NUD.Vertex v2, NUD.Vertex v3, int divisions, Color uvColor, float lineWidth, Color gridColor)
        {
            // No shaders
            GL.UseProgram(0);

            float bounds = 1;
            Vector2 scaleUv = new Vector2(1, 1);

            SetUpUvRendering(lineWidth);

            // Draw Grid
            GL.Color3(gridColor);
            DrawHorizontalGrid(divisions, bounds, scaleUv);
            DrawVerticalGrid(divisions, bounds, scaleUv);
        }

        private static void SetUpUvRendering(float lineWidth)
        {
            // Go to 2D
            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0, 1, 1, 0, 0, 1);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.LineWidth(lineWidth);

            // Draw over everything
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Clear(ClearBufferMask.DepthBufferBit);
        }

        private static void DrawVerticalGrid(int divisions, float bounds, Vector2 scaleUv)
        {
            int verticalCount = divisions;
            for (int i = 0; i < verticalCount * bounds; i++)
            {
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex2(new Vector2((1.0f / verticalCount) * i, -bounds) * scaleUv);
                GL.Vertex2(new Vector2((1.0f / verticalCount) * i, bounds) * scaleUv);
                GL.End();
            }
        }

        private static void DrawHorizontalGrid(int divisions, float bounds, Vector2 scaleUv)
        {
            int horizontalCount = divisions;
            for (int i = 0; i < horizontalCount * bounds; i++)
            {
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex2(new Vector2(-bounds, (1.0f / horizontalCount) * i) * scaleUv);
                GL.Vertex2(new Vector2(bounds, (1.0f / horizontalCount) * i) * scaleUv);
                GL.End();
            }
        }

        public static void DrawCircle(float x, float y, float z, float radius, uint precision)
        {
            DrawCircle(new Vector3(x, y, z), radius, precision);
        }

        public static void DrawCircle(Vector3 center, float radius, uint precision)
        {
            float theta = 2.0f * (float)Math.PI / precision;
            float cosine = (float)Math.Cos(theta);
            float sine = (float)Math.Sin(theta);

            float x = radius;
            float y = 0;

            GL.Begin(PrimitiveType.TriangleFan);
            for (int i = 0; i < precision; i++)
            {
                GL.Vertex2(x + center.X, y + center.Y);

                //apply the rotation matrix
                var temp = x;
                x = cosine * x - sine * y;
                y = sine * temp + cosine * y;
            }
            GL.End();
        }


        public static void DrawHitboxCircle(Vector3 pos, float size, uint smooth, Matrix4 view)
        {
            float t = 2 * (float)Math.PI / smooth;
            float tf = (float)Math.Tan(t);

            float rf = (float)Math.Cos(t);

            float x = size;
            float y = 0;

            GL.Begin(PrimitiveType.LineLoop);

            for (int i = 0; i < smooth; i++)
            {
                GL.Vertex3(Vector3.TransformPosition(new Vector3(x + pos.X, y + pos.Y, pos.Z),view));
                float tx = -y;
                float ty = x;
                x += tx * tf;
                y += ty * tf;
                x *= rf;
                y *= rf;
            }

            GL.End();
        }

        public static void drawCircleOutline(Vector3 center, float radius, uint precision)
        {
            float theta = 2.0f * (float)Math.PI / precision;
            float cosine = (float)Math.Cos(theta);
            float sine = (float)Math.Sin(theta);

            float x = radius;
            float y = 0;

            GL.Begin(PrimitiveType.LineStrip);
            for (int i = 0; i < precision; i++)
            {
                GL.Vertex3(x + center.X, y + center.Y, center.Z);

                //apply the rotation matrix
                var temp = x;
                x = cosine * x - sine * y;
                y = sine * temp + cosine * y;
            }
            GL.End();
        }

        public static void drawCircleOutline(Vector3 center, float radius, uint precision, Matrix4 transform)
        {
            float theta = 2.0f * (float)Math.PI / precision;
            float cosine = (float)Math.Cos(theta);
            float sine = (float)Math.Sin(theta);

            float x = radius;
            float y = 0;

            GL.Begin(PrimitiveType.LineStrip);
            for (int i = 0; i < precision; i++)
            {
                GL.Vertex3(Vector3.TransformPosition(new Vector3(x, y, 0), transform) + center);

                //apply the rotation matrix
                var temp = x;
                x = cosine * x - sine * y;
                y = sine * temp + cosine * y;
            }
            GL.End();
        }

        public static void DrawPyramid(Vector3 center, float scale, bool useWireFrame)
        {
            PrimitiveType primitiveType = PrimitiveType.Quads;
            if (useWireFrame)
            {
                primitiveType = PrimitiveType.LineLoop;
            }

            GL.Begin(primitiveType);

            GL.Vertex3(center.X - scale, center.Y, 0);
            GL.Vertex3(center.X, center.Y - scale, 0);
            GL.Vertex3(center.X + scale, center.Y, 0);
            GL.Vertex3(center.X, center.Y - scale, 0);

            GL.Vertex3(center.X, center.Y, -scale);
            GL.Vertex3(center.X, center.Y - scale, 0);
            GL.Vertex3(center.X, center.Y, scale);
            GL.Vertex3(center.X, center.Y - scale, 0);

            GL.Vertex3(center.X, center.Y, -scale);
            GL.Vertex3(center.X + scale, center.Y, 0);
            GL.Vertex3(center.X, center.Y, -scale);
            GL.Vertex3(center.X - scale, center.Y, 0);

            GL.Vertex3(center.X, center.Y, scale);
            GL.Vertex3(center.X + scale, center.Y, 0);
            GL.Vertex3(center.X, center.Y, scale);
            GL.Vertex3(center.X - scale, center.Y, 0);

            GL.End();
        }

        public static void DrawCube(Vector3 center, float size, bool useWireFrame = false)
        {
            DrawRectangularPrism(center, size, size, size, useWireFrame);
        }

        public static void DrawRectangularPrism(Vector3 center, float sizeX, float sizeY, float sizeZ, bool useWireFrame = false)
        {
            PrimitiveType primitiveType = PrimitiveType.Quads;
            if (useWireFrame)
            {
                GL.LineWidth(2);
                primitiveType = PrimitiveType.LineLoop;
            }

            GL.Begin(primitiveType);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.End();

            GL.Begin(primitiveType);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.End();

            GL.Begin(primitiveType);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.End();

            GL.Begin(primitiveType);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.End();

            GL.Begin(primitiveType);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.End();

            GL.Begin(primitiveType);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.End();
        }
        #endregion


        public void DrawVBNDiamond(VBN vbn)
        {
            if (vbn != null && Runtime.renderBones)
            {
                foreach (Bone bone in vbn.bones)
                {
                    float offset = 0.1f;
                    // first calcuate the point and draw a point
                    GL.Color3(Color.DarkGray);
                    GL.PointSize(1f);

                    Vector3 pos_c = Vector3.TransformPosition(Vector3.Zero, bone.transform);

                    GL.Begin(PrimitiveType.LineLoop);
                    GL.Vertex3(new Vector3(pos_c.X - offset, pos_c.Y, pos_c.Z - offset));
                    GL.Vertex3(new Vector3(pos_c.X + offset, pos_c.Y, pos_c.Z - offset));
                    GL.Vertex3(new Vector3(pos_c.X + offset, pos_c.Y, pos_c.Z + offset));
                    GL.Vertex3(new Vector3(pos_c.X - offset, pos_c.Y, pos_c.Z + offset));
                    GL.End();

                    Vector3 pos_p = pos_c;
                    if (bone.parentIndex != 0x0FFFFFFF && bone.parentIndex != -1)
                    {
                        int i = bone.parentIndex;
                        pos_p = Vector3.TransformPosition(Vector3.Zero, vbn.bones[i].transform);
                    }

                    GL.Color3(Color.Gray);
                    GL.Begin(PrimitiveType.Lines);
                    GL.Vertex3(new Vector3(pos_c.X - offset, pos_c.Y, pos_c.Z - offset));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y + 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X + offset, pos_c.Y, pos_c.Z - offset));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y + 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X + offset, pos_c.Y, pos_c.Z + offset));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y + 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X - offset, pos_c.Y, pos_c.Z + offset));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y + 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X - offset, pos_c.Y, pos_c.Z - offset));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y - 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X + offset, pos_c.Y, pos_c.Z - offset));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y - 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X + offset, pos_c.Y, pos_c.Z + offset));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y - 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X - offset, pos_c.Y, pos_c.Z + offset));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y - 0.25f, pos_c.Z));

                    GL.Vertex3(new Vector3(pos_c.X - offset, pos_c.Y, pos_c.Z - offset));
                    GL.Vertex3(pos_p);
                    GL.Vertex3(new Vector3(pos_c.X + offset, pos_c.Y, pos_c.Z - offset));
                    GL.Vertex3(pos_p);
                    GL.Vertex3(new Vector3(pos_c.X + offset, pos_c.Y, pos_c.Z + offset));
                    GL.Vertex3(pos_p);
                    GL.Vertex3(new Vector3(pos_c.X - offset, pos_c.Y, pos_c.Z + offset));
                    GL.Vertex3(pos_p);

                    GL.End();
                }
            }
        }

        public static void DrawCircle(Vector3 pos, float r, int smooth)
        {
            float t = 2 * (float)Math.PI / smooth;
            float tf = (float)Math.Tan(t);

            float rf = (float)Math.Cos(t);

            float x = r;
            float y = 0;

            GL.Begin(PrimitiveType.LineLoop);

            for (int i = 0; i < smooth; i++)
            {
                GL.Vertex3(x + pos.X, y + pos.Y, pos.Z);
                float tx = -y;
                float ty = x;
                x += tx * tf;
                y += ty * tf;
                x *= rf;
                y *= rf;
            }

            GL.End();
        }

        public static void SetUp3DFixedFunctionRendering(Matrix4 mvpMatrix)
        {
            GL.UseProgram(0);

            // Manually set up the matrix for immediate mode.
            Matrix4 matrix = mvpMatrix;
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref matrix);

            GL.Enable(EnableCap.LineSmooth); // This is Optional 
            GL.Enable(EnableCap.Normalize);  // This is critical to have
            GL.Enable(EnableCap.RescaleNormal);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Gequal, 0.1f);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Front);

            GL.Enable(EnableCap.LineSmooth);

            GL.Enable(EnableCap.StencilTest);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
        }

        public static void DrawBones(List<ModelContainer> con)
        {
            if (con.Count > 0)
            {
                foreach (ModelContainer m in con)
                {
                    DrawVBN(m.VBN);
                    if (m.BCH != null)
                    {
                        //DrawVBN(m.bch.Models.Nodes[0].skeleton);
                    }

                    if (m.DatMelee != null)
                    {
                        DrawVBN(m.DatMelee.bones);
                    }
                }
            }
        }

        public static void DrawVBN(VBN vbn)
        {
            float ToRad = (float)Math.PI / 180;
            int swinganim = 0;

            swinganim++;
            if (swinganim > 100) swinganim = 0;
            if (vbn != null)
            {
                Bone selectedBone = null;
                foreach (Bone bone in vbn.bones)
                {
                    if (!bone.IsSelected)
                        bone.Draw();
                    else
                        selectedBone = bone;

                    if (vbn.SwingBones != null && (Runtime.renderSwagY || Runtime.renderSwagZ))
                    {
                        SB.SBEntry sb = null;
                        vbn.SwingBones.TryGetEntry(bone.boneId, out sb);
                        if (sb != null)
                        {
                            float sf = Math.Abs(((swinganim - 50) / 50f));
                            float sz = (sb.rz1 + (sb.rz2 - sb.rz1) * sf) * ToRad;
                            float sy = (sb.ry1 + (sb.ry2 - sb.ry1) * sf) * ToRad;
                            if (!Runtime.renderSwagY)
                                sy = 0;
                            if (!Runtime.renderSwagZ)
                                sz = 0;
                            bone.rot = VBN.FromEulerAngles(bone.rotation[2], bone.rotation[1], bone.rotation[0]) *
                                VBN.FromEulerAngles(sz, sy, 0);
                        }

                    }
                }

                if (selectedBone != null)
                {
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                    selectedBone.Draw();
                }

                if (vbn.SwingBones != null && (Runtime.renderSwagY || Runtime.renderSwagZ))
                    vbn.update();
            }
        }

        public static void DrawQuadGradient(Vector3 topColor, Vector3 bottomColor, BufferObject screenVbo)
        {
            // draw RGB and alpha channels of texture to screen quad
            Shader shader = Runtime.shaders["Gradient"];
            GL.UseProgram(shader.Id);

            SetUp2DRendering();

            shader.SetVector3("topColor", topColor);
            shader.SetVector3("bottomColor", bottomColor);

            DrawScreenTriangle(shader, screenVbo);          
        }

        public static void DrawTexturedQuad(int texture, int width, int height, bool renderR = true, bool renderG = true, bool renderB = true,
            bool renderA = false, bool keepAspectRatio = false, float intensity = 1, int currentMipLevel = 0)
        {
            // Draws RGB and alpha channels of texture to screen quad.
            Shader shader = Runtime.shaders["Texture"];
            GL.UseProgram(shader.Id);

            SetUp2DRendering();

            // Single texture uniform.
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
            shader.SetTexture("image", texture, TextureTarget.Texture2D, 0);

            // Channel toggle uniforms. 
            shader.SetBoolToInt("renderR",     renderR);
            shader.SetBoolToInt("renderG",     renderG);
            shader.SetBoolToInt("renderB",     renderB);
            shader.SetBoolToInt("renderAlpha", renderA);

            shader.SetFloat("intensity", intensity);

            bool alphaOverride = renderA && !renderR && !renderG && !renderB;
            shader.SetBoolToInt("alphaOverride", alphaOverride);

            // Perform aspect ratio calculations in shader. 
            // This only works properly if the viewport is square.
            shader.SetBoolToInt("preserveAspectRatio", keepAspectRatio);
            float aspectRatio = (float)width / (float)height;
            shader.SetInt("width", width);
            shader.SetInt("height", height);

            // Display certain mip levels.
            shader.SetInt("currentMipLevel", currentMipLevel);

            // Draw full screen "quad" (big triangle)
            DrawScreenTriangle(shader, screenQuadVbo);
        }

        public static void DrawTexturedQuad(int texture, float intensity)
        {
            DrawTexturedQuad(texture, 1, 1, true, true, true, true, false, intensity, 0);
        }

        public static void DrawScreenQuadPostProcessing(int texture0, int texture1)
        {
            // Draws RGB and alpha channels of texture to screen quad.
            Shader shader = Runtime.shaders["ScreenQuad"];
            GL.UseProgram(shader.Id);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
            shader.SetTexture("image0", texture0, TextureTarget.Texture2D, 0);


            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, texture1);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
            shader.SetTexture("image1", texture1, TextureTarget.Texture2D, 1);

            shader.SetBoolToInt("renderBloom", Runtime.renderBloom);
            shader.SetFloat("bloomIntensity", Runtime.bloomIntensity);

            ShaderTools.SystemColorVector3Uniform(shader, Runtime.backgroundGradientBottom, "backgroundBottomColor");
            ShaderTools.SystemColorVector3Uniform(shader, Runtime.backgroundGradientTop, "backgroundTopColor");

            // Draw full screen "quad" (big triangle)
            DrawScreenTriangle(shader, screenQuadVbo);
        }

        public static void DrawNudMaterialSphere(NUD.Material material, BufferObject screenVbo, Dictionary<NUD.DummyTextures, Texture> dummyTextures)
        {
            if (!Runtime.shaders["NudSphere"].ProgramCreatedSuccessfully())
                return;

            Shader shader = Runtime.shaders["NudSphere"];
            GL.UseProgram(shader.Id);

            // Use the same uniforms as the NUD shader. 
            NUD.SetMaterialPropertyUniforms(shader, material);
            NUD.SetStageLightingUniforms(shader, 0);
            ModelContainer.SetRenderSettingsUniforms(shader);
            ModelContainer.SetLightingUniforms(shader, nudSphereCamera);
            ModelContainer.SetCameraMatrixUniforms(nudSphereCamera, shader);

            // Use default textures rather than textures from the NUT.
            NUD.SetTextureUniformsNudMatSphere(shader, material, dummyTextures);

            // These values aren't needed in the shader currently.
            shader.SetVector3("cameraPosition", 0, 0, 0);
            shader.SetFloat("zBufferOffset", 0);
            shader.SetFloat("bloomThreshold", Runtime.bloomThreshold);

            bool isTransparent = (material.srcFactor > 0) || (material.dstFactor > 0) || (material.alphaFunction > 0) || (material.alphaTest > 0);
            shader.SetBoolToInt("isTransparent", isTransparent);

            // Set texture uniforms for the mesh attributes. 
            shader.SetTexture("normalTex", sphereNrmTex.Id, TextureTarget.Texture2D, 15);
            shader.SetTexture("uvTex", sphereUvTex.Id, TextureTarget.Texture2D, 16);
            shader.SetTexture("tanTex", sphereTanTex.Id, TextureTarget.Texture2D, 17);
            shader.SetTexture("bitanTex", sphereBitanTex.Id, TextureTarget.Texture2D, 18);

            // Draw full screen "quad" (big triangle)
            DrawScreenTriangle(shader, screenVbo);
        }

        public static void SetUp2DRendering()
        {
            // Set up OpenGL settings for basic 2D rendering.
            GL.ClearColor(Color.White);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            // Allow for alpha blending.
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
        }

        private static void DrawScreenTriangle(Shader shader, BufferObject vbo)
        {
            shader.EnableVertexAttributes();
            vbo.Bind();

            // Set everytime because multiple shaders use this for drawing.
            GL.VertexAttribPointer(shader.GetVertexAttributeUniformLocation("position"), 3, VertexAttribPointerType.Float, false, sizeof(float) * 3, 0);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            shader.DisableVertexAttributes();
        }

        public static byte[] DXT5ScreenShot(GLControl gc, int x, int y, int width, int height)
        {
            int newtex;
            //x = gc.Width - x - width;
            y = gc.Height - y - height;
            GL.GenTextures(1, out newtex);
            GL.BindTexture(TextureTarget.Texture2D, newtex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.CompressedRgba, width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            GL.CopyTexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.CompressedRgba, x, y, width, height, 0);

            int size;
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureCompressedImageSize, out size);

            byte[] data = new byte[size];
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 0, pointer);
            pinnedArray.Free();

            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.DeleteTexture(newtex);

            return data;
        }

        public static void SetCameraValuesFromParam(Camera camera, ParamFile stprm)
        {
            if (stprm == null)
                return;

            camera.FovDegrees = (float)Params.ParamTools.GetParamValue(stprm, 0, 0, 6);
            camera.FarClipPlane = (float)Params.ParamTools.GetParamValue(stprm, 0, 0, 77);
        }

        public static void DrawPhotoshoot(GLControl glControl1, float shootX, float shootY, float shootWidth, float shootHeight)
        {
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, glControl1.Width, glControl1.Height, 0, -1, 1);

            GL.Disable(EnableCap.DepthTest);

            GL.Color4(1f, 1f, 1f, 0.5f);
            GL.Begin(PrimitiveType.Quads);

            // top
            GL.Vertex2(0, 0);
            GL.Vertex2(glControl1.Width, 0);
            GL.Vertex2(glControl1.Width, shootY);
            GL.Vertex2(0, shootY);

            //bottom
            GL.Vertex2(0, shootY + shootHeight);
            GL.Vertex2(glControl1.Width, shootY + shootHeight);
            GL.Vertex2(glControl1.Width, glControl1.Height);
            GL.Vertex2(0, glControl1.Height);

            // left
            GL.Vertex2(0, 0);
            GL.Vertex2(shootX, 0);
            GL.Vertex2(shootX, glControl1.Height);
            GL.Vertex2(0, glControl1.Height);

            // right
            GL.Vertex2(shootX + shootWidth, 0);
            GL.Vertex2(glControl1.Width, 0);
            GL.Vertex2(glControl1.Width, glControl1.Height);
            GL.Vertex2(shootX + shootWidth, glControl1.Height);

            GL.End();
        }

        public static Ray CreateRay(Matrix4 v, Vector2 m)
        {
            Vector4 va = Vector4.Transform(new Vector4(m.X, m.Y, -1.0f, 1.0f), v.Inverted());
            Vector4 vb = Vector4.Transform(new Vector4(m.X, m.Y, 1.0f, 1.0f), v.Inverted());

            Vector3 p1 = va.Xyz;
            Vector3 p2 = p1 - (va - (va + vb)).Xyz * 100;
            Ray r = new Ray(p1, p2);

            return r;
        }
    }
}

