﻿using System;
using System.IO;
using System.Collections.Generic;
using OpenTK;
using System.Windows.Forms;

namespace Smash_Forge
{
    public class ANIM
    {
        private class AnimHeader
        {
            public string animVersion;
            public string mayaVersion;
            public float startTime;
            public float endTime;
            public float startUnitless;
            public float endUnitless;
            public string timeUnit;
            public string linearUnit;
            public string angularUnit;

            public AnimHeader()
            {
                animVersion = "1.1";
                mayaVersion = "2015";
                startTime = 0;
                endTime = 0;
                startUnitless = 0;
                endUnitless = 0;
                timeUnit = "ntscf";
                linearUnit = "cm";
                angularUnit = "deg";
            }
        }

        private class AnimKey{
            public float input, output;
            public string intan, outtan;
            public float t1 = 0, w1 = 1;
        }

        private class AnimData{
            public string type, input, output, preInfinity, postInfinity;
            public bool weighted = false;
            public List<AnimKey> keys = new List<AnimKey>();

            public float getValue(int frame){
                AnimKey f1 = null, f2 = null;
                for (int i = 0; i < keys.Count-1; i++) {
                    if ((keys [i].input-1 <= frame && keys [i + 1].input-1 >= frame)) {
                        f1 = keys [i];
                        f2 = keys [i + 1];
                        break;
                    }
                }
                if (f1 == null) {
                    if (keys.Count <= 1) {
                        return keys [0].output;
                    } else {
                        f1 = keys [keys.Count - 2];
                        f2 = keys [keys.Count - 1];
                    }
                }

                return CHR0.interHermite (frame+1, f1.input, f2.input, weighted ? f1.t1 : 0, weighted ? f2.t1 : 0, f1.output, f2.output);
            }
        }

        private class AnimBone{
            public string name;
            public List<AnimData> atts = new List<AnimData>();
        }

        public static Animation read(string filename, VBN vbn)
        {
            Animation a = new Animation(filename);
            Animation.KeyNode current = null;
            Animation.KeyFrame att = new Animation.KeyFrame();

            StreamReader reader = File.OpenText(filename);
            string line;

            AnimHeader header = new AnimHeader();
            bool inHeader = true;
            bool inKeys = false;
            string type = "";

            while ((line = reader.ReadLine()) != null)
            {
                string[] args = line.Replace(";", "").TrimStart().Split(' ');

                if (inHeader)
                {
                    if (args[0].Equals("anim"))
                        inHeader = false;
                    else if (args[0].Equals("animVersion"))
                        header.animVersion = args[1];
                    else if (args[0].Equals("mayaVersion"))
                        header.mayaVersion = args[1];
                    else if (args[0].Equals("startTime"))
                        header.startTime = float.Parse(args[1]);
                    else if (args[0].Equals("endTime"))
                        header.endTime = float.Parse(args[1]);
                    else if (args[0].Equals("startUnitless"))
                        header.startUnitless = float.Parse(args[1]);
                    else if (args[0].Equals("endUnitless"))
                        header.endUnitless = float.Parse(args[1]);
                    else if (args[0].Equals("timeUnit"))
                        header.timeUnit = args[1];
                    else if (args[0].Equals("linearUnit"))
                        header.linearUnit = args[1];
                    else if (args[0].Equals("angularUnit"))
                        header.angularUnit = args[1];
                }
                if (!inHeader)
                {
                    if (inKeys)
                    {
                        if(args[0].Equals("}")){
                            inKeys = false;
                            continue;
                        }
                        Animation.KeyFrame k = new Animation.KeyFrame ();
                        //att.keys.Add (k);
                        if (type.Contains("translate"))
                        {
                            if (type.Contains("X")) current.XPOS.Keys.Add(k);
                            if (type.Contains("Y")) current.YPOS.Keys.Add(k);
                            if (type.Contains("Z")) current.ZPOS.Keys.Add(k);
                        }
                        if (type.Contains("rotate"))
                        {
                            if (type.Contains("X")) current.XROT.Keys.Add(k);
                            if (type.Contains("Y")) current.YROT.Keys.Add(k);
                            if (type.Contains("Z")) current.ZROT.Keys.Add(k);
                        }
                        if (type.Contains("scale"))
                        {
                            if (type.Contains("X")) current.XSCA.Keys.Add(k);
                            if (type.Contains("Y")) current.YSCA.Keys.Add(k);
                            if (type.Contains("Z")) current.ZSCA.Keys.Add(k);
                        }
                        k.Frame = float.Parse (args [0])-1;
                        k.Value = float.Parse (args [1]);
                        if (type.Contains("rotate"))
                        {
                            k.Value *= (float)(Math.PI / 180f);
                        }
                            //k.intan = (args [2]);
                            //k.outtan = (args [3]);
                        if (args.Length > 7 && att.Weighted)
                        {
                            k.In = float.Parse(args[7]) * (float)(Math.PI / 180f);
                            k.Out = float.Parse(args[8]) * (float)(Math.PI / 180f);
                        }
                    }

                    if (args [0].Equals ("anim")) {
                        inKeys = false;
                        if (args.Length == 5) {
                            //TODO: finish this type
                            // can be name of attribute
                        }
                        if (args.Length == 7) {
                            // see of the bone of this attribute exists
                            current = null;
                            foreach (Animation.KeyNode b in a.Bones)
                                if (b.Text.Equals (args [3])) {
                                    current = b;
                                    break;
                                }
                            if (current == null) {
                                current = new Animation.KeyNode (args[3]);
                                current.RotType = Animation.RotationType.EULER;
                                a.Bones.Add (current);
                            }
                            current.Text = args [3];

                            att = new Animation.KeyFrame();
                            att.InterType = Animation.InterpolationType.HERMITE;
                            type = args [2];
                            //current.Nodes.Add (att);

                            // row child attribute aren't needed here
                        }
                    }

                    /*if (args [0].Equals ("input"))
                        att.input = args [1];
                    if (args [0].Equals ("output"))
                        att.output = args [1];
                    if (args [0].Equals ("preInfinity"))
                        att.preInfinity = args [1];
                    if (args [0].Equals ("postInfinity"))
                        att.postInfinity = args [1];*/
                    if (args[0].Equals("weighted"))
                        att.Weighted = args[1].Equals("1");


                    // begining keys section
                    if (args [0].Contains ("keys")) {
                        inKeys = true;
                    }
                }
            }

            int startTime = (int)Math.Ceiling(header.startTime);
            int endTime = (int)Math.Ceiling(header.endTime);
            a.FrameCount = (endTime + 1) - startTime;

            reader.Close();
            return a;
        }

        public static void CreateANIM(string fname, Animation a, VBN vbn)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@fname))
            {
                AnimHeader header = new AnimHeader();
                file.WriteLine("animVersion " + header.animVersion + ";");
                file.WriteLine("mayaVersion " + header.mayaVersion + ";");
                file.WriteLine("timeUnit " + header.timeUnit + ";");
                file.WriteLine("linearUnit " + header.linearUnit + ";");
                file.WriteLine("angularUnit " + header.angularUnit + ";");
                file.WriteLine("startTime " + 1 + ";");
                file.WriteLine("endTime " + a.FrameCount + ";");

                a.SetFrame(a.FrameCount - 1); //from last frame
                for (int li = 0; li < a.FrameCount; ++li) //go through each frame with nextFrame
                    a.NextFrame(vbn);
                a.NextFrame(vbn);  //go on first frame

                int i = 0;

                // writing node attributes
                foreach (Bone b in vbn.getBoneTreeOrder())
                {
                    i = vbn.boneIndex(b.Text);

                    if (a.HasBone(b.Text))
                    {
                        // write the bone attributes
                        // count the attributes
                        Animation.KeyNode n = a.GetBone(b.Text);
                        int ac = 0;
                        
                        if (n.XPOS.HasAnimation())
                        {
                            file.WriteLine("anim translate.translateX translateX " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.XPOS, n, a.Size(), "translateX");
                            file.WriteLine("}");
                        }
                        if (n.YPOS.HasAnimation())
                        {
                            file.WriteLine("anim translate.translateY translateY " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.YPOS, n, a.Size(), "translateY");
                            file.WriteLine("}");
                        }
                        if (n.ZPOS.HasAnimation())
                        {
                            file.WriteLine("anim translate.translateZ translateZ " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.ZPOS, n, a.Size(), "translateZ");
                            file.WriteLine("}");
                        }
                        if (n.XROT.HasAnimation())
                        {
                            file.WriteLine("anim rotate.rotateX rotateX " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.XROT, n, a.Size(), "rotateX");
                            file.WriteLine("}");
                        }
                        if (n.YROT.HasAnimation())
                        {
                            file.WriteLine("anim rotate.rotateY rotateY " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.YROT, n, a.Size(), "rotateY");
                            file.WriteLine("}");
                        }
                        if (n.ZROT.HasAnimation())
                        {
                            file.WriteLine("anim rotate.rotateZ rotateZ " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.ZROT, n, a.Size(), "rotateZ");
                            file.WriteLine("}");
                        }
                        if (n.XSCA.HasAnimation())
                        {
                            file.WriteLine("anim scale.scaleX scaleX " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.XSCA, n, a.Size(), "scaleX");
                            file.WriteLine("}");
                        }
                        if (n.YSCA.HasAnimation())
                        {
                            file.WriteLine("anim scale.scaleY scaleY " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.YSCA, n, a.Size(), "scaleY");
                            file.WriteLine("}");
                        }
                        if (n.ZSCA.HasAnimation())
                        {
                            file.WriteLine("anim scale.scaleZ scaleZ " + b.Text + " 0 0 " + (ac++) + ";");
                            writeKey(file, n.ZSCA, n, a.Size(), "scaleZ");
                            file.WriteLine("}");
                        }

                        if(ac == 0)
                            file.WriteLine("anim " + b.Text + " 0 0 0;");
                    }
                    else
                    {
                        file.WriteLine("anim " + b.Text + " 0 0 0;");
                    }
                }
            }
        }

        private static void writeKey(StreamWriter file, Animation.KeyGroup keys, Animation.KeyNode rt, int size, string type)
        {
            file.WriteLine("animData {\n input time;\n output linear;\n weighted 1;\n preInfinity constant;\n postInfinity constant;\n keys {");

            if (((Animation.KeyFrame)keys.Keys[0]).InterType == Animation.InterpolationType.CONSTANT)
                size = 1;
            
            foreach (Animation.KeyFrame key in keys.Keys)
            {
                float v = 0;

                float scale = 1;
                switch (type)
                {
                    case "translateX":
                        v = key.Value;
                        break;
                    case "translateY":
                        v = key.Value;
                        break;
                    case "translateZ":
                        v = key.Value;
                        break;
                    case "rotateX":
                        if (rt.RotType == Animation.RotationType.EULER)
                            v = key.Value * (float)(180f / Math.PI);
                        if (rt.RotType == Animation.RotationType.QUATERNION)
                        {
                            Quaternion q = new Quaternion(rt.XROT.GetValue(key.Frame), rt.YROT.GetValue(key.Frame), rt.ZROT.GetValue(key.Frame), rt.WROT.GetValue(key.Frame));
                            v = quattoeul(q).X * (float)(180f / Math.PI);
                        }
                        scale = (float)(180f / Math.PI);
                            break;
                    case "rotateY":
                        if (rt.RotType == Animation.RotationType.EULER)
                            v = key.Value * (float)(180f / Math.PI);
                        if (rt.RotType == Animation.RotationType.QUATERNION)
                        {
                            Quaternion q = new Quaternion(rt.XROT.GetValue(key.Frame), rt.YROT.GetValue(key.Frame), rt.ZROT.GetValue(key.Frame), rt.WROT.GetValue(key.Frame));
                            v = quattoeul(q).Y * (float)(180f / Math.PI);
                        }
                        scale = (float)(180f / Math.PI);
                        break;
                    case "rotateZ":
                        if (rt.RotType == Animation.RotationType.EULER)
                            v = key.Value * (float)(180f / Math.PI);
                        if (rt.RotType == Animation.RotationType.QUATERNION)
                        {
                            Quaternion q = new Quaternion(rt.XROT.GetValue(key.Frame), rt.YROT.GetValue(key.Frame), rt.ZROT.GetValue(key.Frame), rt.WROT.GetValue(key.Frame));
                            v = quattoeul(q).Z * (float)(180f / Math.PI);
                        }
                        scale = (float)(180f / Math.PI);
                        break;
                    case "scaleX":
                        v = key.Value;
                        break;
                    case "scaleY":
                        v = key.Value;
                        break;
                    case "scaleZ":
                        v = key.Value;
                        break;
                }

                file.WriteLine(" " + (key.Frame + 1) + " {0:N6} fixed fixed 1 1 0 " + key.In*scale + " 1 " + (key.Out != -1 ? key.Out : key.In)*scale + " 1;", v);
            }

            file.WriteLine(" }");
        }


        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static Vector3 quattoeul(Quaternion q){

            Matrix4 mat = Matrix4.CreateFromQuaternion(q);
            float x, y, z;
            y = (float)Math.Asin(Clamp(mat.M13, -1, 1));

            if (Math.Abs(mat.M13) < 0.99999)
            {
                x = (float)Math.Atan2(-mat.M23, mat.M33);
                z = (float)Math.Atan2(-mat.M12, mat.M11);
            }
            else
            {
                x = (float)Math.Atan2(mat.M32, mat.M22);
                z = 0;
            }
            return new Vector3(x, y, z) * -1;
        }
    }
}

