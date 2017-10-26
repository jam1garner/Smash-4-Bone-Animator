﻿using System;
using System.IO;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Reflection;

namespace Smash_Forge
{

    public class Camera
    {
        Vector3 pos = new Vector3(0, -10, -30);
        float rot = 0, lookup = 0;

        public float mouseSLast, mouseYLast, mouseXLast;

        public Camera()
        {
            mouseSLast = OpenTK.Input.Mouse.GetState().WheelPrecise;
            mouseXLast = OpenTK.Input.Mouse.GetState().X;
            mouseYLast = OpenTK.Input.Mouse.GetState().Y;
        }

        public Matrix4 getViewMatrix()
        {
            return Matrix4.CreateRotationY(0.5f * rot) *
                Matrix4.CreateRotationX(0.2f * lookup) *
                Matrix4.CreateTranslation(pos);
        }

        public void Update()
        {
            float zoomscale = Runtime.zoomspeed;

            if ((OpenTK.Input.Mouse.GetState().RightButton == OpenTK.Input.ButtonState.Pressed))
            {
                pos.Y += 0.025f * (OpenTK.Input.Mouse.GetState().Y - mouseYLast);
                pos.X += 0.025f * (OpenTK.Input.Mouse.GetState().X - mouseXLast);
            }
            if ((OpenTK.Input.Mouse.GetState().LeftButton == OpenTK.Input.ButtonState.Pressed))
            {
                rot += 0.025f * (OpenTK.Input.Mouse.GetState().X - mouseXLast);
                lookup += 0.025f * (OpenTK.Input.Mouse.GetState().Y - mouseYLast);
            }

            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.ShiftLeft) || OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.ShiftRight))
                zoomscale = 6;

            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.Down))
                pos.Z -= 1 * zoomscale;
            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.Up))
                pos.Z += 1 * zoomscale;

            pos.Z += (OpenTK.Input.Mouse.GetState().WheelPrecise - mouseSLast) * zoomscale;
            
        }

        public void TrackMouse()
        {
            mouseXLast = OpenTK.Input.Mouse.GetState().X;
            mouseYLast = OpenTK.Input.Mouse.GetState().Y;
            mouseSLast = OpenTK.Input.Mouse.GetState().WheelPrecise;
        }
    }


    public class Ray
    {
        public Vector3 p1, p2;

        public Ray(Vector3 p1, Vector3 p2)
        {
            this.p1 = p1;
            this.p2 = p2;
        }

        public bool TrySphereHit(Vector3 sphere, float rad, out Vector3 closest)
        {
            return RenderTools.CheckSphereHit(sphere, rad, p1, p2,  out closest);
        }
    }

    public class DirectionalLight
    {
        public float R = 1.0f;
        public float G = 1.0f;
        public float B = 1.0f;
        public float rotX = 0.0f; // in degrees (converted to radians)
        public float rotY = 0.0f; // in degrees (converted to radians)
        public float rotZ = 0.0f; // in degrees (converted to radians)
        public Vector3 direction = new Vector3(0f, 0f, 1f);

        public DirectionalLight(float H, float S, float V, float rotX, float rot, float rotZ)
        {
            // calculate light color
            RenderTools.HSV2RGB(H, S, V, out R, out G, out B);

            // calculate light vector
            Matrix4 lightRotMatrix = Matrix4.CreateFromAxisAngle(Vector3.UnitX, rotX * ((float)Math.PI / 180f))
             * Matrix4.CreateFromAxisAngle(Vector3.UnitY, rotY * ((float)Math.PI / 180f))
             * Matrix4.CreateFromAxisAngle(Vector3.UnitZ, rotZ * ((float)Math.PI / 180f));

            direction = Vector3.Transform(new Vector3(0f, 0f, 1f), lightRotMatrix).Normalized();
        }

        public DirectionalLight(float H, float S, float V, Vector3 lightDirection)
        {
            // calculate light color
            RenderTools.HSV2RGB(H, S, V, out R, out G, out B);

            // calculate light vector
            direction = lightDirection;
        }

        public DirectionalLight()
        {

        }

        public void setDirectionFromXYZAngles(float rotX, float rotY, float rotZ)
        {
            // calculate light vector from 3 rotation angles
            Matrix4 lightRotMatrix = Matrix4.CreateFromAxisAngle(Vector3.UnitX, rotX * ((float)Math.PI / 180f))
             * Matrix4.CreateFromAxisAngle(Vector3.UnitY, rotY * ((float)Math.PI / 180f))
             * Matrix4.CreateFromAxisAngle(Vector3.UnitZ, rotZ * ((float)Math.PI / 180f));

            direction = Vector3.Transform(new Vector3(0f, 0f, 1f), lightRotMatrix).Normalized();
        }

        public void setColorFromHSV(float H, float S, float V)
        {
            RenderTools.HSV2RGB(H, S, V, out R, out G, out B);
        }

    }

    public class AreaLight
    {
        // not sure how this should work exactly
        public string ID = "";
    
        // ambient color
        public float groundR = 1.0f;
        public float groundG = 1.0f;
        public float groundB = 1.0f;

        // diffuse color
        public float skyR = 1.0f;
        public float skyG = 1.0f;
        public float skyB = 1.0f;
        
        // size
        public float scaleX = 1.0f;
        public float scaleY = 1.0f;
        public float scaleZ = 1.0f;

        // position of the center of the region
        public float positionX = 0.0f;
        public float positionY = 0.0f;
        public float positionZ = 0.0f;

        // XYZ angles
        // How should "non directional" area lights work?
        public float rotX = 0.0f; // in degrees (converted to radians)
        public float rotY = 0.0f; // in degrees (converted to radians)
        public float rotZ = 0.0f; // in degrees (converted to radians)
        public Vector3 direction = new Vector3(0f, 0f, 1f);

        public bool noDirectional = false;
        public bool renderBoundingBox = true;

        public AreaLight(string areaLightID)
        {
            ID = areaLightID;
        }

        public AreaLight(string areaLightID, Vector3 groundColor, Vector3 skyColor, Vector3 position, Vector3 scale, Vector3 direction)
        {
            ID = areaLightID;
            groundR = groundColor.X;
            groundG = groundColor.Y;
            groundB = groundColor.Z;

            skyR = skyColor.X;
            skyG = skyColor.Y;
            skyB = skyColor.Z;
        }

        public AreaLight(string areaLightID, Vector3 groundColor, Vector3 skyColor, Vector3 position, Vector3 scale, float rotX, float rotY, float rotZ)
        {
            ID = areaLightID;
        }

        public void setDirectionFromXYZAngles(float rotX, float rotY, float rotZ)
        {
            // calculate light vector from 3 rotation angles
            Matrix4 lightRotMatrix = Matrix4.CreateFromAxisAngle(Vector3.UnitX, rotX * ((float)Math.PI / 180f))
             * Matrix4.CreateFromAxisAngle(Vector3.UnitY, rotY * ((float)Math.PI / 180f))
             * Matrix4.CreateFromAxisAngle(Vector3.UnitZ, rotZ * ((float)Math.PI / 180f));

            direction = Vector3.Transform(new Vector3(0f, 0f, 1f), lightRotMatrix).Normalized();
        }



    }


    public class RenderTools
    {
        public static int defaultTex = -1, userTex;
        public static int cubeTex, cubeTex2;
        public static int defaultRamp;
        public static int UVTestPattern;

        public static int cubeVAO, cubeVBO;

        public static void Setup()
        {
            cubeTex2 = LoadCubeMap(Smash_Forge.Properties.Resources._10101000);
            defaultRamp = NUT.loadImage(Smash_Forge.Properties.Resources._10080000);
            UVTestPattern = NUT.loadImage(Smash_Forge.Properties.Resources.UVPattern);

            if (defaultTex == -1)
            cubeTex = LoadCubeMap(Smash_Forge.Properties.Resources.cubemap);
            if(defaultTex == -1)
            defaultTex = NUT.loadImage(Smash_Forge.Resources.Resources.DefaultTexture);
         
            GL.GenBuffers(1, out cubeVAO);
            GL.GenBuffers(1, out cubeVBO);
        }


        public static void drawTranslator(Matrix4 view)
        {
            Vector3 center = new Vector3(5, 10, 5);

            // check if within range
            {
                Vector3 p1 = Vector3.Transform(center, view).Normalized();
                Vector3 p2 = Vector3.Transform(center + new Vector3(0, 5, 0), view).Normalized();

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
                throw new DivideByZeroException("DrawSphere: Radius cannot be zero.");

            if (precision == 0)
                throw new DivideByZeroException("DrawSphere: Precision of 8 or greater is required.");

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
                throw new DivideByZeroException("DrawSphere: Radius cannot be zero.");

            if (precision == 0)
                throw new DivideByZeroException("DrawSphere: Precision of 8 or greater is required.");

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
            //GL.Disable(EnableCap.CullFace);

            drawSphere(Vector3.Zero, 100, 10);

            GL.StencilMask(0xFF);
            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.CullFace);
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
            //GL.Disable(EnableCap.CullFace);

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
                throw new DivideByZeroException("DrawSphere: Radius cannot be zero.");

            if (precision == 0)
                throw new DivideByZeroException("DrawSphere: Precision of 8 or greater is required.");

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
                    GL.Vertex3(Vector3.Transform(new Vector3(pos.X, pos.Y, pos.Z), transform));

                    norm.X = (float)(Math.Cos(theta1) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta1);
                    norm.Z = (float)(Math.Cos(theta1) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * j * oneThroughPrecision);
                    GL.Vertex3(Vector3.Transform(new Vector3(pos.X, pos.Y, pos.Z), transform));
                }
                GL.End();
            }
        }

        public static void drawWireframeSphereTransformed(Vector3 center, float radius, uint precision, Matrix4 transform)
        {
            if (radius < 0.0f)
                radius = -radius;

            if (radius == 0.0f)
                throw new DivideByZeroException("DrawSphere: Radius cannot be zero.");

            if (precision == 0)
                throw new DivideByZeroException("DrawSphere: Precision of 8 or greater is required.");

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
                    GL.Vertex3(Vector3.Transform(new Vector3(pos.X, pos.Y, pos.Z), transform));

                    norm.X = (float)(Math.Cos(theta1) * Math.Cos(theta3));
                    norm.Y = (float)Math.Sin(theta1);
                    norm.Z = (float)(Math.Cos(theta1) * Math.Sin(theta3));
                    pos.X = center.X + radius * norm.X;
                    pos.Y = center.Y + radius * norm.Y;
                    pos.Z = center.Z + radius * norm.Z;

                    GL.Normal3(norm.X, norm.Y, norm.Z);
                    GL.TexCoord2(i * oneThroughPrecision, 2.0f * j * oneThroughPrecision);
                    GL.Vertex3(Vector3.Transform(new Vector3(pos.X, pos.Y, pos.Z), transform));
                }
                GL.End();
            }
        }

        public static Matrix4 def = Matrix4.CreateTranslation(0,0,0);

        public static void drawHitboxCylinder(Vector3 p1, Vector3 p2, float R)
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

            //GL.Scale(scale.X, scale.Y, scale.Z);
            //double[] f = new double[] {
            //    transform.M11, transform.M12, transform.M13, transform.M14,
            //    transform.M21, transform.M22, transform.M23, transform.M24,
            //    transform.M31, transform.M32, transform.M33, transform.M34,
            //    transform.M41, transform.M42, transform.M43, transform.M44,
            //};
            //GL.MultMatrix(f);
            //Vector3 scale = transform.ExtractScale();
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

            //GL.Scale(scale.X, scale.Y, scale.Z);
            double[] f = new double[] {
                transform.M11, transform.M12, transform.M13, transform.M14,
                transform.M21, transform.M22, transform.M23, transform.M24,
                transform.M31, transform.M32, transform.M33, transform.M34,
                transform.M41, transform.M42, transform.M43, transform.M44,
            };
            GL.MultMatrix(f);
            //Vector3 scale = transform.ExtractScale();
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

            //GL.Scale(scale.X, scale.Y, scale.Z);
            double[] f = new double[] {
                transform.M11, transform.M12, transform.M13, transform.M14,
                transform.M21, transform.M22, transform.M23, transform.M24,
                transform.M31, transform.M32, transform.M33, transform.M34,
                transform.M41, transform.M42, transform.M43, transform.M44,
            };
            GL.MultMatrix(f);
            //Vector3 scale = transform.ExtractScale();
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

        public static void drawCylinder(Vector3 p1, Vector3 p2, float R){
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

        public static void drawWireframeCylinder(Vector3 p1, Vector3 p2, float R)
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
            drawCircle(new Vector3(x, y, -1f), radius, precision);

            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);

            // Back to 3D
            GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix();
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix();
        }

        public static void drawFloor()
        {
            bool solid = Runtime.floorStyle == Runtime.FloorStyle.Solid;
            float s = Runtime.floorSize;

            GL.UseProgram(0);

            // objects shouldn't show through opaque parts of floor
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.Color3(Runtime.floorColor);
            GL.LineWidth(1f);

            if (Runtime.floorStyle == Runtime.FloorStyle.Textured || Runtime.floorStyle == Runtime.FloorStyle.UserTexture)
            {
                GL.Enable(EnableCap.Texture2D);
                GL.ActiveTexture(TextureUnit.Texture0);
                if (Runtime.floorStyle == Runtime.FloorStyle.UserTexture)
                    GL.BindTexture(TextureTarget.Texture2D, userTex);
                else
                    GL.BindTexture(TextureTarget.Texture2D, defaultTex);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)Runtime.floorWrap);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)Runtime.floorWrap);

                GL.Color3(Runtime.floorColor == Color.Gray ? Color.White : Runtime.floorColor);
                GL.Begin(PrimitiveType.Quads);

                GL.TexCoord2(0, 0);
                GL.Vertex3(new Vector3(-s, 0f, -s));
                GL.TexCoord2(0, 2);
                GL.Vertex3(new Vector3(-s, 0f, s));
                GL.TexCoord2(2, 2);
                GL.Vertex3(new Vector3(s, 0f, s));
                GL.TexCoord2(2, 0);
                GL.Vertex3(new Vector3(s, 0f, -s));

                GL.End();
                GL.Disable(EnableCap.Texture2D);
            }
            else
            if (solid)
            {
                GL.Begin(PrimitiveType.Quads);
                GL.Vertex3(-s, 0f, -s);
                GL.Vertex3(-s, 0f, s);
                GL.Vertex3(s, 0f, s);
                GL.Vertex3(s, 0f, -s);
                GL.End();
            }
            else
            {
                GL.Begin(PrimitiveType.Lines);
                for (var i = -s / 2; i <= s / 2; i++)
                {
                    if (i != 0)
                    {
                        GL.Vertex3(-s, 0f, i * 2);
                        GL.Vertex3(s, 0f, i * 2);
                        GL.Vertex3(i * 2, 0f, -s);
                        GL.Vertex3(i * 2, 0f, s);
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
                GL.Vertex3(-s, 0f, 0);
                GL.Vertex3(s, 0f, 0);
                GL.Vertex3(0, 0f, -s);
                GL.Vertex3(0, 0f, s);
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

        public static void drawCircle(float x, float y, float z, float radius, uint precision)
        {
            drawCircle(new Vector3(x, y, z), radius, precision);
        }

        public static void drawCircle(Vector3 center, float radius, uint precision)
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


        public static void drawHitboxCircle(Vector3 pos, float size, uint smooth, Matrix4 view)
        {
            float t = 2 * (float)Math.PI / smooth;
            float tf = (float)Math.Tan(t);

            float rf = (float)Math.Cos(t);

            float x = size;
            float y = 0;

            GL.Begin(PrimitiveType.LineLoop);

            for (int i = 0; i < smooth; i++)
            {
                GL.Vertex3(Vector3.Transform(new Vector3(x + pos.X, y + pos.Y, pos.Z),view));
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
                GL.Vertex3(Vector3.Transform(new Vector3(x, y, 0), transform) + center);

                //apply the rotation matrix
                var temp = x;
                x = cosine * x - sine * y;
                y = sine * temp + cosine * y;
            }
            GL.End();
        }

        public static void drawPyramid(Vector3 center, float scale)
        {
            GL.Begin(PrimitiveType.Quads);

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

        public static void drawPyramidWireframe(Vector3 center, float scale, float lineWidth)
        {
            //GL.Color4(Color.FromArgb(200, Color.Black));
            GL.LineWidth(lineWidth);
            GL.Begin(PrimitiveType.Lines);

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


        public static void drawCube(Vector3 center, float size)
        {
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex3(center.X + size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X + size, center.Y + size, center.Z + size);

            GL.Vertex3(center.X + size, center.Y - size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z - size);
            GL.Vertex3(center.X + size, center.Y - size, center.Z - size);

            GL.Vertex3(center.X + size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z + size);
            GL.Vertex3(center.X + size, center.Y - size, center.Z + size);

            GL.Vertex3(center.X + size, center.Y - size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X + size, center.Y + size, center.Z - size);

            GL.Vertex3(center.X - size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z + size);

            GL.Vertex3(center.X + size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X + size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X + size, center.Y - size, center.Z + size);
            GL.Vertex3(center.X + size, center.Y - size, center.Z - size);
            GL.End();
        }

        public static void drawCubeWireframe(Vector3 center, float size)
        {
            GL.Color3(Color.Red);
            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X + size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X + size, center.Y + size, center.Z + size);

            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X + size, center.Y - size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z - size);
            GL.Vertex3(center.X + size, center.Y - size, center.Z - size);
            GL.End();

            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X + size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z + size);
            GL.Vertex3(center.X + size, center.Y - size, center.Z + size);
            GL.End();

            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X + size, center.Y - size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X + size, center.Y + size, center.Z - size);
            GL.End();

            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X - size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X - size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z - size);
            GL.Vertex3(center.X - size, center.Y - size, center.Z + size);
            GL.End();

            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X + size, center.Y + size, center.Z - size);
            GL.Vertex3(center.X + size, center.Y + size, center.Z + size);
            GL.Vertex3(center.X + size, center.Y - size, center.Z + size);
            GL.Vertex3(center.X + size, center.Y - size, center.Z - size);
            GL.End();
        }

        public static void drawRectangularPrism(Vector3 center, float sizeX, float sizeY, float sizeZ)
        {
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);

            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);

            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);

            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);

            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);

            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.End();
        }

        public static void drawRectangularPrismWireframe(Vector3 center, float sizeX, float sizeY, float sizeZ)
        {
            //GL.Color3(Color.Black);
            GL.LineWidth(2);
            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);
                                                    
            GL.Begin(PrimitiveType.LineLoop);       
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.End();                             
       
            GL.Begin(PrimitiveType.LineLoop);       
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.End();
            
            GL.Begin(PrimitiveType.LineLoop);       
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.End();

            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X - sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.End();

            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z - sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y + sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z + sizeZ);
            GL.Vertex3(center.X + sizeX, center.Y - sizeY, center.Z - sizeZ);
            GL.End();
        }
        #endregion


        public static void drawCircle(Vector3 pos, float r, int smooth)
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

        public static bool intersectCircle(Vector3 pos, float r, int smooth, Vector3 vA, Vector3 vB)
        {
            float t = 2 * (float)Math.PI / smooth;
            float tf = (float)Math.Tan(t);

            float rf = (float)Math.Cos(t);

            float x = r;
            float y = 0;
            
            for (int i = 0; i < smooth; i++)
            {
                Vector3 c;
                Vector3 p = new Vector3(x + pos.X, y +pos.Y, pos.Z);
                if (CheckSphereHit(p, 0.3f, vA, vB, out c))
                    return true;
                float tx = -y;
                float ty = x;
                x += tx * tf;
                y += ty * tf;
                x *= rf;
                y *= rf;
            }

            return false;
        }

        public static bool CheckSphereHit(Vector3 sphere, float rad, Vector3 vA, Vector3 vB, out Vector3 closest)
        {
            Vector3 dirToSphere = sphere - vA;
            Vector3 vLineDir = (vB - vA).Normalized();
            float fLineLength = 100;
            /*(float)Math.Sqrt(Math.Pow(vA.X - vB.X, 2)
                + Math.Pow(vA.Y - vB.Y, 2)
                + Math.Pow(vA.Z - vB.Z, 2));*/
            float t = Vector3.Dot(dirToSphere, vLineDir);

            if (t <= 0.0f)
                closest = vA;
            else if (t >= fLineLength)
                closest = vB;
            else
                closest = vA + vLineDir * t;

            return (Math.Pow(sphere.X - closest.X, 2)
                + Math.Pow(sphere.Y - closest.Y, 2)
                + Math.Pow(sphere.Z - closest.Z, 2) <= rad * rad);
        }

        #region FileRendering


        public static void DrawModel(ModelContainer m, Matrix4 v)
        {
            if (m.dat_melee != null)
            {
                m.dat_melee.Render(v);
            }

            if (m.nud != null)
            {
                m.nud.Render(v, m.vbn);
                m.nud.DrawPoints(v, m.vbn);
            }
        }

        public static void DrawBones()
        {
            if (Runtime.ModelContainers.Count > 0)
            {
                foreach (ModelContainer m in Runtime.ModelContainers)
                {
                    DrawVBN(m.vbn);
                    if (m.bch != null)
                    {
                        DrawVBN(m.bch.models[0].skeleton);
                    }

                    if (m.dat_melee != null)
                    {
                        DrawVBN(m.dat_melee.bones);
                    }
                }
            }
        }

        public static void DrawVBN(VBN vbn)
        {
            if (vbn != null)
            {
                Bone selectedBone = null;
                foreach (Bone bone in vbn.bones)
                {
                    if (!bone.IsSelected)
                        bone.Draw();
                    else
                        selectedBone = bone;

                    // if swing bones then draw swing radius
                    /*if (vbn.swingBones.bones.Count > 0 && Runtime.renderSwag)
                    {
                        SB.SBEntry sb = null;
                        vbn.swingBones.TryGetEntry(bone.boneId, out sb);
                        if (sb != null)
                        {
                            // draw
                            if (bone.ParentBone != null)
                            {
                                int i = bone.parentIndex;
                                float degtorad = (float)(Math.PI / 180);
                                Vector3 pos_sb = Vector3.Transform(Vector3.Zero,
                                    Matrix4.CreateTranslation(new Vector3(3, 3, 3))
                                    * Matrix4.CreateScale(bone.sca)
                                    * Matrix4.CreateFromQuaternion(VBN.FromEulerAngles(sb.rx1 * degtorad, sb.ry1 * degtorad, sb.rz1 * degtorad))
                                    * Matrix4.CreateTranslation(bone.pos)
                                    * vbn.bones[i].transform);

                                Vector3 pos_sb2 = Vector3.Transform(Vector3.Zero,
                                    Matrix4.CreateTranslation(new Vector3(3, 3, 3))
                                    * Matrix4.CreateScale(bone.sca)
                                    * Matrix4.CreateFromQuaternion(VBN.FromEulerAngles(sb.rx2 * degtorad, sb.ry2 * degtorad, sb.rz2 * degtorad))
                                    * Matrix4.CreateTranslation(bone.pos)
                                    * vbn.bones[i].transform);

                                GL.Color3(Color.ForestGreen);
                                GL.Begin(PrimitiveType.LineLoop);
                                GL.Vertex3(pos_c);
                                GL.Vertex3(pos_sb);
                                GL.Vertex3(pos_sb2);
                                GL.End();
                            }
                        }
                    }*/
                }
                if (selectedBone != null)
                {
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                    selectedBone.Draw();
                }
            }
        }

        public static void DrawTexturedQuad(int texture, bool renderR, bool renderG, bool renderB, bool renderAlpha, bool alphaOverride)             
        {
            // draw RGB and alpha channels of texture to screen quad

            Shader shader = Runtime.shaders["Texture"];
            GL.UseProgram(shader.programID);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.Uniform1(shader.getAttribute("image"), 0);

            GL.Uniform1(shader.getAttribute("renderR"), renderR ? 1 : 0);
            GL.Uniform1(shader.getAttribute("renderG"), renderG ? 1 : 0);
            GL.Uniform1(shader.getAttribute("renderB"), renderB ? 1 : 0);
            GL.Uniform1(shader.getAttribute("renderAlpha"), renderAlpha ? 1 : 0);
            GL.Uniform1(shader.getAttribute("alphaOverride"), alphaOverride ? 1 : 0);



            GL.Disable(EnableCap.DepthTest);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3); // draw full screen "quad" (big triangle)
            GL.BindVertexArray(0);
        }

        public static void HSV2RGB(float h, float s, float v, out float R, out float G, out float B) 
        {

            R = 1.0f;
            G = 1.0f;
            B = 1.0f;

            // values have to be 0-360 to work properly
            while (h > 360) { h -= 360; }
            while (h < 0) { h += 360; }

            if (s > 1.0f)
                s = 1.0f;

            float hf = h / 60.0f;
            int i = (int)Math.Floor(hf);
            float f = hf - i;
            float pv = v * (1 - s);
            float qv = v * (1 - s * f);
            float tv = v * (1 - s * (1 - f));

            switch (i)
            {
                // Red is the dominant color

                case 0:
                    R = v;
                    G = tv;
                    B = pv;
                    break;

                // Green is the dominant color

                case 1:
                    R = qv;
                    G = v;
                    B = pv;
                    break;
                case 2:
                    R = pv;
                    G = v;
                    B = tv;
                    break;

                // Blue is the dominant color

                case 3:
                    R = pv;
                    G = qv;
                    B = v;
                    break;
                case 4:
                    R = tv;
                    G = pv;
                    B = v;
                    break;

                // Red is the dominant color

                case 5:
                    R = v;
                    G = pv;
                    B = qv;
                    break;

                case 6:
                    R = v;
                    G = tv;
                    B = pv;
                    break;
            
            }

        }

        public static void RGB2HSV(float R, float G, float B, out float h, out float s, out float v) // need to check weird values for errors
        {
            h = 360.0f;
            s = 1.0f;
            v = 1.0f;

            float cMax = Math.Max(Math.Max(R, G), B);
            float cMin = Math.Min(Math.Min(R, G), B);
            float delta = cMax - cMin;

            v = cMax;

            if (delta == 0)
                h = 0;

            if (v == 0)
                s = 0.0f;
            else
                s = delta / v;

            if (R == cMax)
                h = 60.0f * (((G - B) / delta));

            else if (G == cMax)
                h = 60.0f * (((B - R) / delta) + 2);

            else if (B == cMax)
                h = 60.0f * (((R - G) / delta) + 4);

            while (h < 0.0f)
                h += 360.0f;

        }

        public static void ColorTemp2RGB(float temp, out float R, out float G, out float B)
        {
            // adapted from a cool algorithm by Tanner Helland
            // http://www.tannerhelland.com/4435/convert-temperature-rgb-algorithm-code/ 

            R = 1.0f;
            G = 1.0f;
            B = 1.0f;

            // use doubles for calculations and convert to float at the end
            // no need for double precision floating point colors on GPU
            double Red = 255.0;
            double Green = 255.0;
            double Blue = 255.0;

            temp = temp / 100.0f;

            // Red calculations
            if (temp <= 66.0f)
                Red = 255.0f;
            else
            {
                Red = temp - 60.0;
                Red = 329.698727446 * Math.Pow(Red, -0.1332047592);
                if (Red < 0.0)
                    Red = 0.0;
                if (Red > 255.0)
                    Red = 255.0;
            }

            // Green calculations
            if (temp <= 66.0)
            {
                Green = temp;
                Green = 99.4708025861 * Math.Log(Green) - 161.1195681661;
                if (Green < 0.0)
                    Green = 0.0;
                if (Green > 255.0)
                    Green = 255.0;
            }
            else
            {
                Green = temp - 60.0;
                Green = 288.1221695283 * Math.Pow(Green, -0.0755148492);
                if (Green < 0)
                    Green = 0;
                if (Green > 255)
                    Green = 255;
            }
                
            // Blue calculations
            if (temp >= 66.0)
                Blue = 255.0;
            else if (temp <= 19.0)
                Blue = 0.0;
            else
            {
                Blue = temp - 10;
                Blue = 138.5177312231 * Math.Log(Blue) - 305.0447927307;
                if (Blue < 0.0)
                    Blue = 0.0;
                if (Blue > 255)
                    Blue = 255;
            }

            Red = Red / 255.0;
            Green = Green / 255.0;
            Blue = Blue / 255.0;

            R = (float)Red;
            G = (float)Green;
            B = (float)Blue;
        }


        public static int ClampInt(int i) // Restricts RGB values to 0 to 255
        {
            if (i > 255)
                return 255;
            if (i < 0)
                return 0;
            else
                return i;
        }

        public static float ClampFloat(float i) // Restricts RGB values to 0.0 to 1.0
        {
            if (i > 1.0f)
                return 1.0f;
            if (i < 0.0f)
                return 0.0f;
            else
                return i;
        }

        public static int Float2RGBClamp(float i) 
        {
            // converts input color to int and restricts values to 0 to 255
            // useful for setting colors of form UI stuff

            i *= 255;
            i = (int)i;
            if (i > 255)
                return 255;
            if (i < 0)
                return 0;
            return (int)i;
        }


        #endregion

        #region Other
        public static int LoadCubeMap(Bitmap b)
        {
            int id;
            GL.GenBuffers(1, out id);

            GL.ActiveTexture(0);
            
            GL.BindTexture(TextureTarget.TextureCubeMap, id);

            Bitmap bmp = b;

            Rectangle[] srcRect = new Rectangle[] {
            new Rectangle(0, 0, 128, 128),
            new Rectangle(0, 128, 128, 128),
            new Rectangle(0, 256, 128, 128),
            new Rectangle(0, 384, 128, 128),
            new Rectangle(0, 512, 128, 128),
            new Rectangle(0, 640, 128, 128),
            }; 

            for(int i = 0; i < 6; i++)
            {
                Bitmap image = (Bitmap)bmp.Clone(srcRect[i], bmp.PixelFormat);
                BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
                image.UnlockBits(data);
            }

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.TextureCubeMap, 0);

            return id;
        }

        private static float[] cube_vertices = new float[]{
      -1.0f,  1.0f,  1.0f,
      -1.0f, -1.0f,  1.0f,
       1.0f, -1.0f,  1.0f,
       1.0f,  1.0f,  1.0f,
      -1.0f,  1.0f, -1.0f,
      -1.0f, -1.0f, -1.0f,
       1.0f, -1.0f, -1.0f,
       1.0f,  1.0f, -1.0f,
    };

        private static short[] cube_indices = new short[]{
	  0, 1, 2, 3,
	  3, 2, 6, 7,
	  7, 6, 5, 4,
	  4, 5, 1, 0,
	  0, 3, 7, 4,
	  1, 2, 6, 5,
	};

        public static string cubevs = @"#version 330
in vec3 position;
out vec3 uv;

uniform mat3 view;

void main()
{
    gl_Position =  vec4(position, 1.0);  
    uv = position;
}";

        public static string cubefs = @"#version 330
in vec3 uv;

uniform samplerCube skycube;

void main()
{    
    gl_FragColor = textureCube(skycube, uv);
}";
        

        public static void RenderCubeMap(Matrix4 view)
        {
            Shader shader = Runtime.shaders["SkyBox"];
            //------------------------------------------------------
            GL.UseProgram(shader.programID);
            
            shader.enableAttrib();
            //------------------------------------------------------
            Matrix3 v = new Matrix3(view);
            GL.UniformMatrix3(shader.getAttribute("view"), false, ref v);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, RenderTools.cubeTex);
            GL.Uniform1(shader.getAttribute("skycube"), 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, cubeVBO);
            GL.BufferData<float>(BufferTarget.ArrayBuffer, (IntPtr)(cube_vertices.Length * sizeof(float)), cube_vertices, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.getAttribute("position"), 3, VertexAttribPointerType.Float, false, sizeof(float) * 3, 0);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, cubeVAO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(cube_indices.Length * sizeof(short)), cube_indices, BufferUsageHint.StaticDraw);

            GL.DrawElements(BeginMode.Quads, cube_indices.Length, DrawElementsType.UnsignedShort, 0);
            //Console.WriteLine(GL.GetError());

            //------------------------------------------------------
            shader.disableAttrib();

            GL.UseProgram(0);
            //------------------------------------------------------
        }

        #endregion

        #region Ray Trace Controls

        public static Ray createRay(Matrix4 v, Vector2 m)
        {
            Vector4 va = Vector4.Transform(new Vector4(m.X, m.Y, -1.0f, 1.0f), v.Inverted());
            Vector4 vb = Vector4.Transform(new Vector4(m.X, m.Y, 1.0f, 1.0f), v.Inverted());

            Vector3 p1 = va.Xyz;
            Vector3 p2 = p1 - (va - (va + vb)).Xyz * 100;
            Ray r = new Ray(p1, p2);

            return r;
        }

        #endregion

        #region Shaders

        #region point shader
        public static string vs_Point = @"#version 330

in vec3 vPosition;
in vec4 vBone;
in vec4 vWeight;
in int vSelected;

flat out int selected;

uniform mat4 mvpMatrix;
uniform bones
{
    mat4 transforms[200];
} bones_;

vec4 skin(vec3 po, ivec4 index)
{
    vec4 oPos = vec4(po.xyz, 1.0);

    oPos = bones_.transforms[index.x] * vec4(po, 1.0) * vWeight.x;
    oPos += bones_.transforms[index.y] * vec4(po, 1.0) * vWeight.y;
    oPos += bones_.transforms[index.z] * vec4(po, 1.0) * vWeight.z;
    oPos += bones_.transforms[index.w] * vec4(po, 1.0) * vWeight.w;
    
    selected = vSelected;

    return oPos;
}

void main()
{
    vec4 objPos = vec4(vPosition.xyz, 1.0);
    ivec4 bi = ivec4(vBone); 

    if(vBone.x != -1.0) objPos = skin(vPosition, bi);

    selected = vSelected;

    gl_Position = mvpMatrix * vec4(objPos.xyz, 1.0f);
}
";
        public static string fs_Point = @"#version 330

flat in int selected;

void main()
{
    if(selected == 0)
        gl_FragColor = vec4(1,1,1,1);
    else if(selected > 0)
        gl_FragColor = vec4(1,1,0,1);
    else
        gl_FragColor = vec4(1,0,1,1);
}
";

        #endregion

        #region Shadow Shader

        public static string vs_Shadow = @"#version 330
in vec3 vPosition;
out vec4 outPosition;
uniform mat4 lightSpaceMatrix;
uniform mat4 modelMatrix;
uniform mat4 eyeview; // modelview matrix

void main()
{
    gl_Position = lightSpaceMatrix * modelMatrix * vec4(vPosition, 1.0f); //lightSpaceMatrix * eyeview * vec4(vPosition, 1.0f);
    outPosition = gl_Position;
}";
        public static string fs_Shadow = @"#version 330
//out vec4 outColor;
in vec4 outPosition;
void main()
{   
    float depth = outPosition.z;
    depth *= 0.01;
    //outColor = vec4(vec3(depth), 1);
}";


        #endregion






        #region blur shader
        // basically identical to "quad" shader but with guassian blur
        public static string vs_blur = @"#version 330

out vec2 TexCoords;
 
void main()
{
    float x = -1.0 + float((gl_VertexID & 1) << 2);
    float y = -1.0 + float((gl_VertexID & 2) << 1);
    TexCoords.x = (x+1.0)*0.5;
    TexCoords.y = (y+1.0)*0.5;
    gl_Position = vec4(x, y, 0, 1);
}";
        public static string fs_blur = @"#version 330 core
out vec4 FragColor;
  
in vec2 TexCoords;

uniform sampler2D image;
  
uniform int horizontal;
uniform float weight[5] = float[] (0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);

void main()
{             
    vec2 tex_offset = 1.0 / textureSize(image, 0); // gets size of single texel
    vec3 result = texture(image, TexCoords).rgb * weight[0]; // current fragment's contribution
    if(horizontal == 1)
    {
        for(int i = 1; i < 5; ++i)
        {
            result += texture(image, TexCoords + vec2(tex_offset.x * i, 0.0)).rgb * weight[i];
            result += texture(image, TexCoords - vec2(tex_offset.x * i, 0.0)).rgb * weight[i];
        }
    }
    else
    {
        for(int i = 1; i < 5; ++i)
        {
            result += texture(image, TexCoords + vec2(0.0, tex_offset.y * i)).rgb * weight[i];
            result += texture(image, TexCoords - vec2(0.0, tex_offset.y * i)).rgb * weight[i];
        }
    }
    result = texture(image, TexCoords).rgb;
    FragColor = vec4(result, 1.0);
    //FragColor = vec4(TexCoords.x, TexCoords.y, 1, 1);
}";

        #endregion


        #endregion
    }
}

