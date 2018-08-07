﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using System.Diagnostics;
using Smash_Forge.Rendering;
using SFGraphics.Cameras;

namespace Smash_Forge
{
    class DropOutStack<T>
    {
        private T[] items;
        private int top = 0;
        public int Count
        {
            get
            {
                return _count;
            }
        }
        private int _count = 0;

        public DropOutStack(int capacity)
        {
            items = new T[capacity];
        }

        public void Push(T item)
        {
            items[top] = item;
            top = (top + 1) % items.Length;
            _count++;
            if (_count > 19) _count = 19;
        }
        public T Pop()
        {
            if (_count == 0) return default(T);
            top = (items.Length + top - 1) % items.Length;
            _count--;
            if (_count < 0) _count = 0;
            return items[top];
        }
    }

    public class Sca
    {
        public Vector3 scale;
        public Sca(Vector3 s)
        {
            scale = s;
        }
    }

    public class TransformTool
    {
        public Bone b
        {
            get
            {
                return _b;
            }
            set
            {
                Undo.Push(_b);
                _b = value;
            }
        }
        private Bone _b;
        public bool hit = false;
        public float Size = 1.5f;

        DropOutStack<object> Undo = new DropOutStack<object>(20);

        Vector2 PrevPoint = new Vector2();
        public int state = 0;

        bool _hiX = false;
        bool _hiY = false;
        bool _hiZ = false;

        public ToolTypes Type = ToolTypes.POSITION;

        public bool Changed = false;

        public enum ToolTypes
        {
            POSITION,
            ROTATION,
            SCALE
        }

        public bool HasChanged()
        {
            if (Changed)
            {
                Changed = false;
                return true;
            }
            return false;
        }

        public TransformTool()
        {
        }

        public void UndoOperation()
        {
            object op = Undo.Pop();

            if (op is Bone)
            {
                _b = (Bone)op;
                op = Undo.Pop();
            }

            if (op is Vector3)
                _b.pos = (Vector3)op;

            if (op is Quaternion)
                _b.rot = (Quaternion)op;

            if (op is Sca)
                _b.sca = ((Sca)op).scale;

            _b.vbnParent.update();
            Changed = true;
        }

        bool keydown = false;
        public void Render(Camera Camera, Ray Ray)
        {

            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.Z) && OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.ControlLeft))
            {
                if (keydown == false)
                    UndoOperation();
                keydown = true;
            }
            else
                keydown = false;

            if (b == null) return;
            if (state == 0 && hit && OpenTK.Input.Mouse.GetState().IsButtonDown(OpenTK.Input.MouseButton.Left))
            {
                state = 1;
                if (Type == ToolTypes.POSITION)
                    Undo.Push(b.pos);
                if (Type == ToolTypes.ROTATION)
                    Undo.Push(new Quaternion(b.rot.Xyz, b.rot.W));
                if (Type == ToolTypes.SCALE)
                    Undo.Push(new Sca(b.pos));
            }

            if (state == 1 && !OpenTK.Input.Mouse.GetState().IsButtonDown(OpenTK.Input.MouseButton.Left))
            {
                Changed = true;
                state = 0;
            }

            switch (Type)
            {
                case ToolTypes.ROTATION:
                    RenderRotationTool(Camera, Ray);
                    break;
                case ToolTypes.POSITION:
                    RenderPositionTool(Camera, Ray);
                    break;
                case ToolTypes.SCALE:
                    RenderScaleTool(Camera, Ray);
                    break;
            }

            PrevPoint = new Vector2(Ray.mouse_x, Ray.mouse_y);

        }
        
        public void RenderScaleTool(Camera Camera, Ray Ray)
        {
            Matrix4 mat = b.transform.ClearScale(); // Matrix4.CreateTranslation(b.transform.ExtractTranslation());
            Vector3 center = Vector3.TransformPosition(Vector3.Zero, mat);

            if (state == 0)
            {
                _hiX = false;
                _hiY = false;
                _hiZ = false;

                Vector3 close;
                if (Ray.CheckSphereHit(center, 0.5f, out close))
                {
                    _hiX = true;
                    _hiY = true;
                    _hiZ = true;
                }
                else
                if (Ray.CheckSphereHit(Vector3.TransformPosition(new Vector3(2, 0, 0), mat), 0.5f, out close))
                    _hiX = true;
                else
                if (Ray.CheckSphereHit(Vector3.TransformPosition(new Vector3(0, 2, 0), mat), 0.5f, out close))
                    _hiY = true;
                else
                if (Ray.CheckSphereHit(Vector3.TransformPosition(new Vector3(0, 0, 2), mat), 0.5f, out close))
                    _hiZ = true;

                hit = true;
                if (!_hiX && !_hiZ && !_hiY)
                    hit = false;
            }
            if (state == 1)
            {
                float sx = (Ray.mouse_x - PrevPoint.X) / 100;
                float sy = (Ray.mouse_y - PrevPoint.Y) / 100;
                float s = sx + sy;
                b.sca = b.sca + new Vector3(_hiX ? s : 0, _hiY ? s : 0, _hiZ ? s : 0);
                b.vbnParent.update();
            }

            GL.PushMatrix();
            GL.MultMatrix(ref mat);
            GL.LineWidth(2f);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(_hiX ? Color.Yellow : Color.Red);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(2, 0, 0);
            GL.Color3(_hiY ? Color.Yellow : Color.Green);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, 2, 0);
            GL.Color3(_hiZ ? Color.Yellow : Color.Blue);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, 0, 2);
            GL.End();


            GL.Color3(_hiX ? Color.Yellow : Color.Red);
            RenderTools.DrawCube(new Vector3(2, 0, 0), 0.25f);
            GL.Color3(_hiY ? Color.Yellow : Color.Green);
            RenderTools.DrawCube(new Vector3(0, 2, 0), 0.25f);
            GL.Color3(_hiZ ? Color.Yellow : Color.Blue);
            RenderTools.DrawCube(new Vector3(0, 0, 2), 0.25f);
            GL.PopMatrix();
        }

        public void RenderPositionTool(Camera Camera, Ray Ray)
        {
            Matrix4 mat = b.transform.ClearScale();// Matrix4.CreateTranslation(b.transform.ExtractTranslation());
            Vector3 center = Vector3.TransformPosition(Vector3.Zero, mat);

            if (state == 0)
            {
                _hiX = false;
                _hiY = false;
                _hiZ = false;
                
                Vector3 close;
                if (Ray.CheckSphereHit(center, 0.5f, out close))
                {
                    _hiX = true;
                    _hiY = true;
                    _hiZ = true;
                }
                else
                if (Ray.CheckSphereHit(Vector3.TransformPosition(new Vector3(2, 0, 0), mat), 0.5f, out close))
                    _hiX = true; else
                if (Ray.CheckSphereHit(Vector3.TransformPosition(new Vector3(0, 2, 0), mat), 0.5f, out close))
                    _hiY = true; else
                if (Ray.CheckSphereHit(Vector3.TransformPosition(new Vector3(0, 0, 2), mat), 0.5f, out close))
                    _hiZ = true;

                hit = true;
                if (!_hiX && !_hiZ && !_hiY)
                    hit = false;
            }
            if (state == 1)
            {
                float sx = (Ray.mouse_x - PrevPoint.X) / 100;
                float sy = (Ray.mouse_y - PrevPoint.Y) / 100;
                float s = sx + sy;
                b.pos = b.pos + new Vector3(_hiX ? s : 0, _hiY ? s : 0, _hiZ ? s * -1 : 0);
                b.vbnParent.update();
            }

            GL.PushMatrix();
            GL.MultMatrix(ref mat);
            GL.LineWidth(2f);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(_hiX ? Color.Yellow : Color.Red);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(2, 0, 0);
            GL.Vertex3(2, 0, 0);
            GL.Vertex3(1.5f, 0, 0.25f);
            GL.Vertex3(2, 0, 0);
            GL.Vertex3(1.5f, 0, -0.25f);
            GL.Color3(_hiY ? Color.Yellow : Color.Green);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, 2, 0);
            GL.Vertex3(0, 2, 0);
            GL.Vertex3(0.25f, 1.5f, 0);
            GL.Vertex3(0, 2, 0);
            GL.Vertex3(-0.25f, 1.5f, 0);
            GL.Color3(_hiZ ? Color.Yellow : Color.Blue);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, 0, 2);
            GL.Vertex3(0, 0, 2);
            GL.Vertex3(0.25f, 0, 1.5f);
            GL.Vertex3(0, 0, 2);
            GL.Vertex3(-0.25f, 0, 1.5f);
            GL.End();
            GL.PopMatrix();
        }

        private void RenderRotationTool(Camera Camera, Ray Ray)
        {
            Matrix4 mat = b.transform.ClearScale();
            Vector3 center = Vector3.TransformPosition(Vector3.Zero, mat);
            Matrix4 invTrasnform = b.transform.ClearScale().Inverted();
            Vector3 point;
            if (state == 0)
            {
                hit = Ray.LineSphereIntersect(center, Size, out point);
                if (hit)
                {
                    Vector3 angle = Angles(Vector3.TransformPosition(point, invTrasnform)) * new Vector3(180f / (float)Math.PI);
                    angle.X = Math.Abs(angle.X);
                    angle.Y = Math.Abs(angle.Y);
                    angle.Z = Math.Abs(angle.Z);

                    _hiX = false;
                    _hiY = false;
                    _hiZ = false;
                    float _axisSnapRange = 14f;
                    if (Math.Abs(angle.Y - 90.0f) <= _axisSnapRange)
                        _hiX = true;
                    else if (angle.X >= (180.0f - _axisSnapRange) || angle.X <= _axisSnapRange)
                        _hiY = true;
                    else if (angle.Y >= (180.0f - _axisSnapRange) || angle.Y <= _axisSnapRange)
                        _hiZ = true;
                }
                if (!_hiX && !_hiZ && !_hiY)
                    hit = false;

            }

            if (state == 1)
            {
                float sx = (Ray.mouse_x - PrevPoint.X) / 100;
                float sy = (Ray.mouse_y - PrevPoint.Y) / 100;
                float s = sx + sy;
                if (_hiX)
                    b.rot = b.rot * Quaternion.FromAxisAngle(Vector3.UnitX, s);
                if (_hiY)
                    b.rot = b.rot * Quaternion.FromAxisAngle(Vector3.UnitY, s);
                if (_hiZ)
                    b.rot = b.rot * Quaternion.FromAxisAngle(Vector3.UnitZ, s);
                b.vbnParent.update();
            }

            GL.PushMatrix();
            GL.MultMatrix(ref mat);

            GL.Color4(0.25f, 0.25f, 0.25f, 0.2f);
            RenderTools.drawSphere(Vector3.Zero, Size, 25);

            GL.Color3(_hiZ ? Color.Yellow : Color.Green);
            GL.LineWidth(3);
            RenderTools.drawCircleOutline(Vector3.Zero, Size, 25);

            GL.Rotate(90.0f, 0.0f, 1.0f, 0.0f);

            GL.Color3(_hiX ? Color.Yellow : Color.Red);
            RenderTools.drawCircleOutline(Vector3.Zero, Size, 25);

            GL.Rotate(90.0f, 1.0f, 0.0f, 0.0f);

            GL.Color3(_hiY ? Color.Yellow : Color.Blue);
            RenderTools.drawCircleOutline(Vector3.Zero, Size, 25);

            GL.PopMatrix();
        }


        public float CamDistance(Vector3 va, Vector3 vb)
        {
            return (float)Math.Sqrt(Vector3.Dot(vb - va, vb - va));
        }

        public float TrueDistance(Vector3 va, Vector3 vb)
        {
            return Vector3.Dot(vb, va);
        }


        public Vector3 Angles(Vector3 i)
        {
            Vector3 ni = new Vector3();
            ni.X = (float)Math.Atan2(i.Y, -i.Z);
            ni.Y = (float)Math.Atan2(-i.Z, i.X);
            ni.Z = (float)Math.Atan2(i.Y, i.X);
            return ni;
        }
    }
}
