﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Security.Cryptography;
using SALT.Moveset.AnimCMD;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using Gif.Components;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;
using Smash_Forge.Rendering.Lights;
using OpenTK.Input;
using Smash_Forge.Rendering;
using Smash_Forge.Params;
using SFGraphics.GLObjects.Textures;

namespace Smash_Forge
{
    public partial class ModelViewport : EditorBase
    {
        // setup
        bool readyToRender = false;

        // View controls
        public Camera camera = new Camera();
        public GUI.Menus.CameraSettings cameraPosForm = null;

        // Rendering Stuff
        int colorHdrFbo;
        int colorHdrTex0;
        int colorHdrTex1;
        int hdrDepthRbo;

        // The texture that will be blurred for bloom.
        Framebuffer imageBrightHdrFbo;

        // Used for screen renders and color picking.
        Framebuffer offscreenRenderFbo;

        // The viewport dimensions should be used for FBOs visible on screen.
        // Larger dimensions can be used for higher quality outputs for FBOs.
        int fboRenderWidth;
        int fboRenderHeight;

        // Functions of Viewer
        public enum Mode
        {
            Normal = 0,
            Photoshoot,
            Selection
        }
        public Mode currentMode = Mode.Normal;

        FrameTimer frameTime = new FrameTimer();

        VertexTool vertexTool = new VertexTool();
        TransformTool transformTool = new TransformTool();

        //Animation
        private Animation currentAnimation;
        public Animation CurrentAnimation {
            get
            {
                return currentAnimation;
            }
            set
            {
                //Moveset
                //If moveset is loaded then initialize with null script so handleACMD loads script for frame speed modifiers and FAF (if parameters are imported)
                if (MovesetManager != null && acmdScript == null)
                    acmdScript = new ForgeACMDScript(null);

                if(value != null)
                {
                    string TargetAnimString = value.Text;
                    if (!string.IsNullOrEmpty(TargetAnimString))
                    {
                        if (acmdScript != null)
                        {
                            //Remove manual crc flag
                            //acmdEditor.manualCrc = false;
                            HandleACMD(TargetAnimString);
                            if (acmdScript != null)
                                acmdScript.processToFrame(0);

                        }
                    }
                }
                ResetModels();
                currentMaterialAnimation = null;
                currentAnimation = value;
                totalFrame.Value = value.FrameCount;
                animationTrackBar.TickFrequency = 1;
                currentFrame.Value = 1;
                currentFrame.Value = 0;
            }
        }

        private MTA currentMaterialAnimation;
        public MTA CurrentMaterialAnimation
        {
            get
            {
                return currentMaterialAnimation;
            }
            set
            {
                ResetModels();
                currentAnimation = null;
                currentMaterialAnimation = value;
                totalFrame.Value = value.numFrames;
                animationTrackBar.TickFrequency = 1;
                animationTrackBar.SetRange(0, (int)value.numFrames);
                currentFrame.Value = 1;
                currentFrame.Value = 0;
            }
        }

        // ACMD
        public int scriptId = -1;
        public Dictionary<string, int> paramMoveNameIdMapping;
        public CharacterParamManager paramManager;
        public PARAMEditor paramManagerHelper;

        private MovesetManager movesetManager;
        public MovesetManager MovesetManager
        {
            get
            {
                return movesetManager;
            }
            set
            {
                movesetManager = value;
                if(acmdEditor != null)
                    acmdEditor.updateCrcList();
            }
        }

        public ForgeACMDScript acmdScript = null;

        public ACMDPreviewEditor acmdEditor;
        public HitboxList hitboxList;
        public HurtboxList hurtboxList;
        public VariableList variableViewer;

        // Used in ModelContainer for direct UV time animation.
        public static Stopwatch directUVTimeStopWatch = new Stopwatch();

        //LVD
        private LVD lvd;
        public LVD LVD
        {
            get
            {
                return lvd;
            }
            set
            {
                lvd = value;
                lvd.MeshList = meshList;
                lvdEditor.LVD = lvd;
                lvdList.TargetLVD = lvd;
                lvdList.fillList();
            }
        }

        LVDList lvdList = new LVDList();
        LVDEditor lvdEditor = new LVDEditor();

        //Path
        public PathBin pathBin;
        
        // Selection Functions
        public float sx1, sy1;
        
        //Animation Functions
        public int animationSpeed = 60;
        public float frame = 0;
        public bool isPlaying;

        // Contents
        public MeshList meshList = new MeshList();
        public AnimListPanel animListPanel = new AnimListPanel();
        public TreeNodeCollection draw;

        // Photoshoot
        public bool freezeCamera = false;
        public int shootX = 0;
        public int shootY = 0;
        public int shootWidth = 50;
        public int shootHeight = 50;

        public ModelViewport()
        {
            InitializeComponent();
            camera = new Camera();
            FilePath = "";
            Text = "Model Viewport";

            SetupMeshList();
            SetupAnimListPanel();
            SetupLvdEditors();
            SetupVertexTool();
            SetupAcmdEditor();
            SetupHitBoxList();
            SetupHurtBoxList();
            SetupVariableViewer();

            LVD = new LVD();

            ViewComboBox.SelectedIndex = 0;

            draw = meshList.filesTreeView.Nodes;
        }

        private void SetupVariableViewer()
        {
            variableViewer = new VariableList();
            variableViewer.Dock = DockStyle.Right;
        }

        private void SetupHurtBoxList()
        {
            hurtboxList = new HurtboxList();
            hurtboxList.Dock = DockStyle.Right;
        }

        private void SetupHitBoxList()
        {
            hitboxList = new HitboxList();
            hitboxList.Dock = DockStyle.Right;
            AddControl(hitboxList);
        }

        private void SetupAcmdEditor()
        {
            acmdEditor = new ACMDPreviewEditor();
            acmdEditor.Owner = this;
            acmdEditor.Dock = DockStyle.Right;
            acmdEditor.updateCrcList();
            AddControl(acmdEditor);
        }

        private void SetupVertexTool()
        {
            vertexTool.Dock = DockStyle.Left;
            vertexTool.MaximumSize = new Size(300, 2000);
            AddControl(vertexTool);
            vertexTool.vp = this;
        }

        private void SetupLvdEditors()
        {
            lvdList.Dock = DockStyle.Left;
            lvdList.MaximumSize = new Size(300, 2000);
            AddControl(lvdList);
            lvdList.lvdEditor = lvdEditor;

            lvdEditor.Dock = DockStyle.Right;
            lvdEditor.MaximumSize = new Size(300, 2000);
            AddControl(lvdEditor);
        }

        private void SetupAnimListPanel()
        {
            animListPanel.Dock = DockStyle.Left;
            animListPanel.MaximumSize = new Size(300, 2000);
            animListPanel.Size = new Size(300, 2000);
            AddControl(animListPanel);
        }

        private void SetupMeshList()
        {
            meshList.Dock = DockStyle.Right;
            meshList.MaximumSize = new Size(300, 2000);
            meshList.Size = new Size(300, 2000);
            AddControl(meshList);
        }

        private void SetupBuffersAndTextures()
        {
            // Use the viewport dimensions by default.
            fboRenderWidth = glViewport.Width;
            fboRenderHeight = glViewport.Height;

            // Render bright and normal images to separate textures.
            FramebufferTools.CreateHdrFboTwoTextures(out colorHdrFbo, out hdrDepthRbo, out colorHdrTex0, out colorHdrTex1, fboRenderWidth, fboRenderHeight);

            // Smaller FBO/texture for the brighter, blurred portions.
            int brightTexWidth = (int)(glViewport.Width * Runtime.bloomTexScale);
            int brightTexHeight = (int)(glViewport.Height * Runtime.bloomTexScale);
            imageBrightHdrFbo = new Framebuffer(FramebufferTarget.Framebuffer, brightTexWidth, brightTexHeight, PixelInternalFormat.Rgba16f);

            // Screen Rendering
            offscreenRenderFbo = new Framebuffer(FramebufferTarget.Framebuffer, fboRenderWidth, fboRenderHeight);

            // Bind the default framebuffer again.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public ModelViewport(string filename) : this()
        {

        }

        ~ModelViewport()
        {
        }

        public Camera GetCamera()
        {
            return camera;
        }

        public override void Save()
        {
            if (FilePath.Equals(""))
            {
                SaveAs();
                return;
            }
            switch (Path.GetExtension(FilePath).ToLower())
            {
                case ".lvd":
                    lvd.Save(FilePath);
                    break;
                case ".mtable":
                    movesetManager.Save(FilePath);
                    break;
            }
            Edited = false;
        }

        public override void SaveAs()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Smash 4 Level Data (.lvd)|*.lvd|" +
                             "ACMD|*.mtable|" +
                             "All Files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    if (sfd.FileName.EndsWith(".lvd") && lvd != null)
                    {
                        FilePath = sfd.FileName;
                        Save();
                    }
                    if (sfd.FileName.EndsWith(".mtable") && movesetManager != null)
                    {
                        FilePath = sfd.FileName;
                        Save();
                    }
                }
            }
        }

        public void AddControl(Form frm)
        {
            frm.TopLevel = false;
            frm.FormBorderStyle = FormBorderStyle.None;
            frm.Visible = false;
            Controls.Add(frm);
        }

        private void ModelViewport_Load(object sender, EventArgs e)
        {
            var timer = new Timer();
            timer.Interval = 1000 / 120;
            timer.Tick += new EventHandler(Application_Idle);
            timer.Start();

            InitializeLights();
        }

        private static void InitializeLights()
        {
            // TODO: Initialize Lights
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (this.IsDisposed)
                return;

            if (readyToRender)
            {
                if (isPlaying)
                {
                    if (animationTrackBar.Value == totalFrame.Value)
                        animationTrackBar.Value = 0;
                    else
                        animationTrackBar.Value++;
                }
                glViewport.Invalidate();
            }
        }

        public Vector2 GetMouseOnViewport()
        {
            float mouse_x = glViewport.PointToClient(Cursor.Position).X;
            float mouse_y = glViewport.PointToClient(Cursor.Position).Y;

            float mx = (2.0f * mouse_x) / glViewport.Width - 1.0f;
            float my = 1.0f - (2.0f * mouse_y) / glViewport.Height;
            return new Vector2(mx, my);
        }

        private void MouseClickItemSelect(System.Windows.Forms.MouseEventArgs e)
        {
            if (!readyToRender || glViewport == null)
                return;

            //Mesh Selection Test
            if (e.Button == MouseButtons.Left)
            {
                Ray ray = new Ray(camera, glViewport);

                transformTool.b = null;
                foreach (TreeNode node in draw)
                {
                    if (!(node is ModelContainer))
                        continue;
                    ModelContainer modelContainer = (ModelContainer)node;

                    if (modeBone.Checked)
                    {
                        // Bounding spheres work well because bones aren't close together.
                        SortedList<double, Bone> selected = modelContainer.GetBoneSelection(ray);
                        if (selected.Count > 0)
                            transformTool.b = selected.Values.ElementAt(0);
                        //break;
                    }

                    if (modeMesh.Checked)
                    {
                        // Use a color ID render pass for more precision.
                        //SelectMeshAtMousePosition();
                    }

                    if (modePolygon.Checked)
                    {
                        // Use a color ID render pass for more precision.
                        SelectPolygonAtMousePosition();
                    }
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Display the MeshList context menus in the viewport.
                // This is faster than right clicking in the treeview.
                if (meshList.filesTreeView.SelectedNode is NUD.Polygon)
                    meshList.PolyContextMenu.Show(glViewport, e.X, e.Y);
                else if (meshList.filesTreeView.SelectedNode is NUD.Mesh)
                    meshList.MeshContextMenu.Show(glViewport, e.X, e.Y);
            }
        }

        private void SelectPolygonAtMousePosition()
        {
            RenderNudColorIdPassToFbo(offscreenRenderFbo.Id);

            // Get the color at the mouse's position.
            Color selectedColor = ColorPickPixelAtMousePosition();
            meshList.filesTreeView.SelectedNode = GetSelectedPolygonFromColor(selectedColor);
        }

        private void SelectMeshAtMousePosition()
        {
            RenderNudColorIdPassToFbo(offscreenRenderFbo.Id);

            // Get the color at the mouse's position.
            Color selectedColor = ColorPickPixelAtMousePosition();
            meshList.filesTreeView.SelectedNode = GetSelectedMeshFromColor(selectedColor);
        }

        private void RenderNudColorIdPassToFbo(int fbo)
        {
            // Render the ID map to the offscreen FBO.
            glViewport.MakeCurrent();
            GL.Viewport(0, 0, fboRenderWidth, fboRenderHeight);
            Runtime.drawNudColorIdPass = true;
            Render(null, null, fbo);
            Runtime.drawNudColorIdPass = false;
        }

        private NUD.Polygon GetSelectedPolygonFromColor(Color pixelColor)
        {
            // Determine what polgyon is selected.
            foreach (TreeNode node in draw)
            {
                if (!(node is ModelContainer))
                    continue;
                ModelContainer con = (ModelContainer)node;

                foreach (NUD.Mesh mesh in con.NUD.Nodes)
                {
                    foreach (NUD.Polygon p in mesh.Nodes)
                    {
                        // The color is the polygon index (not the render order).
                        // Convert to Vector3 to ignore the alpha.
                        Vector3 polyColor = ColorTools.Vector4FromColor(Color.FromArgb(p.DisplayId)).Xyz;
                        Vector3 pickedColor = ColorTools.Vector4FromColor(pixelColor).Xyz;

                        if (polyColor == pickedColor)
                            return p;
                    }
                }         
            }

            return null;
        }

        private NUD.Mesh GetSelectedMeshFromColor(Color pixelColor)
        {
            // Determine what mesh is selected.
            foreach (TreeNode node in draw)
            {
                if (!(node is ModelContainer))
                    continue;
                ModelContainer con = (ModelContainer)node;

                foreach (NUD.Mesh mesh in con.NUD.Nodes)
                {
                    // The color is the mesh index.
                    // Convert to Vector3 to ignore the alpha.
                    Vector3 meshColor = ColorTools.Vector4FromColor(Color.FromArgb(mesh.DisplayId)).Xyz;
                    Vector3 pickedColor = ColorTools.Vector4FromColor(pixelColor).Xyz;

                    if (meshColor == pickedColor)
                        return mesh;
                }
            }

            return null;
        }

        private Color ColorPickPixelAtMousePosition()
        {
            // Colorpick a single pixel from the offscreen FBO at the mouse's location.
            System.Drawing.Point mousePosition = glViewport.PointToClient(Cursor.Position);
            return offscreenRenderFbo.SamplePixelColor(mousePosition.X, glViewport.Height - mousePosition.Y);
        }

        private Vector3 getScreenPoint(Vector3 pos)
        {
            Vector4 n = Vector4.Transform(new Vector4(pos, 1), camera.MvpMatrix);
            n.X /= n.W;
            n.Y /= n.W;
            n.Z /= n.W;
            return n.Xyz;
        }

        private void glViewport_Resize(object sender, EventArgs e)
        {
            if (readyToRender && currentMode != Mode.Selection && glViewport.Height != 0 && glViewport.Width != 0)
            {
                GL.LoadIdentity();
                GL.Viewport(0, 0, fboRenderWidth, fboRenderHeight);

                camera.renderWidth = glViewport.Width;
                camera.renderHeight = glViewport.Height;
                fboRenderWidth = glViewport.Width;
                fboRenderHeight = glViewport.Height;
                camera.Update();

                ResizeTexturesAndBuffers();
            }
        }

        private void ResizeTexturesAndBuffers()
        {
            // FBOs manage their own resizing.
            ResizeHdrFboRboTwoColorAttachments();

            imageBrightHdrFbo.Width = (int)(fboRenderWidth * Runtime.bloomTexScale);
            imageBrightHdrFbo.Height = (int)(fboRenderHeight * Runtime.bloomTexScale);

            offscreenRenderFbo.Width = fboRenderWidth;
            offscreenRenderFbo.Height = fboRenderHeight;
        }

        private void ResizeHdrFboRboTwoColorAttachments()
        {
            // Resize the textures and buffers everytime the dimensions change.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, colorHdrFbo);

            int textureWidth = fboRenderWidth;
            int textureHeight = fboRenderHeight;

            // First color attachment.
            GL.BindTexture(TextureTarget.Texture2D, colorHdrTex0);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, textureWidth, textureHeight, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

            // Second color attachment.
            GL.BindTexture(TextureTarget.Texture2D, colorHdrTex1);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, textureWidth, textureHeight, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

            // Render buffer for the depth attachment.
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, hdrDepthRbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent, textureWidth, textureHeight);
        }

        #region Animation Events

        private void animationTrackBar_ValueChanged(object sender, EventArgs e)
        {
            if (animationTrackBar.Value > totalFrame.Value)
                animationTrackBar.Value = 0;
            if (animationTrackBar.Value < 0)
                animationTrackBar.Value = (int)totalFrame.Value;
            currentFrame.Value = animationTrackBar.Value;


            int frameNum = animationTrackBar.Value;

            if (currentMaterialAnimation != null)
            {
                foreach (TreeNode node in meshList.filesTreeView.Nodes)
                {
                    if (!(node is ModelContainer)) continue;
                    ModelContainer m = (ModelContainer)node;
                    m.NUD.ApplyMta(currentMaterialAnimation, frameNum);
                }
            }
            
            if (currentAnimation == null) return;

            // Process script first in case we have to speed up the animation
            if (acmdScript != null)
                acmdScript.processToFrame(frameNum);

            float animFrameNum = frameNum;
            if (acmdScript != null && Runtime.useFrameDuration)
                animFrameNum = acmdScript.animationFrame;// - 1;
            
            foreach (TreeNode node in meshList.filesTreeView.Nodes)
            {
                if (!(node is ModelContainer)) continue;
                ModelContainer m = (ModelContainer)node;
                currentAnimation.SetFrame(animFrameNum);
                if (m.VBN != null)
                    currentAnimation.NextFrame(m.VBN);

                // Deliberately do not ever use ACMD/animFrame to modify these other types of model
                if (m.DatMelee != null)
                {
                    currentAnimation.SetFrame(frameNum);
                    currentAnimation.NextFrame(m.DatMelee.bones);
                }
                if (m.BCH != null)
                {
                    foreach (BCH_Model mod in m.BCH.Models.Nodes)
                    {
                        if (mod.skeleton != null)
                        {
                            currentAnimation.SetFrame(animFrameNum);
                            currentAnimation.NextFrame(mod.skeleton);
                        }
                    }
                }
            }

            //Frame = (int)animFrameNum;
        }

        public void ResetModels()
        {
            foreach (TreeNode node in meshList.filesTreeView.Nodes)
            {
                if (!(node is ModelContainer)) continue;
                ModelContainer m = (ModelContainer)node;
                m.NUD.ClearMta();
                if (m.VBN != null)
                    m.VBN.reset();

                // Deliberately do not ever use ACMD/animFrame to modify these other types of model
                if (m.DatMelee != null)
                {
                    m.DatMelee.bones.reset();
                }
                if (m.BCH != null)
                {
                    foreach (BCH_Model mod in m.BCH.Models.Nodes)
                    {
                        if (mod.skeleton != null)
                        {
                            mod.skeleton.reset();
                        }
                    }
                }
            }
        }

        private void currentFrame_ValueChanged(object sender, EventArgs e)
        {
            if (currentFrame.Value > totalFrame.Value)
                currentFrame.Value = totalFrame.Value;
            animationTrackBar.Value = (int)currentFrame.Value;
        }

        private void playButton_Click(object sender, EventArgs e)
        {
            isPlaying = !isPlaying;
            playButton.Text = isPlaying ? "Pause" : "Play";

            if (isPlaying)
                directUVTimeStopWatch.Start();
            else
                directUVTimeStopWatch.Stop();
        }

        #endregion

        private void ResetCamera_Click(object sender, EventArgs e)
        {
            // Frame the selected NUD or mesh based on the bounding spheres. Frame the NUD if nothing is selected. 
            FrameSelectionAndSort();
        }

        public void FrameSelectionAndSort()
        {
            if (meshList.filesTreeView.SelectedNode is NUD.Mesh)
            {
                FrameSelectedMesh();
            }
            else if (meshList.filesTreeView.SelectedNode is NUD)
            {
                FrameSelectedNud();
            }
            else if (meshList.filesTreeView.SelectedNode is NUD.Polygon)
            {
                FrameSelectedPolygon();
            }
            else if (meshList.filesTreeView.SelectedNode is ModelContainer)
            {
                FrameSelectedModelContainer();
            }
            else
            {
                FrameAllModelContainers();
            }

            // Depth sorting. 
            foreach (TreeNode node in meshList.filesTreeView.Nodes)
            {
                if (node is ModelContainer)
                {
                    ModelContainer modelContainer = (ModelContainer)node;
                    modelContainer.DepthSortModels(camera.Position);
                }
            }
        }

        private void FrameSelectedModelContainer()
        {
            ModelContainer modelContainer = (ModelContainer)meshList.filesTreeView.SelectedNode;
            float[] boundingBox = new float[] { 0, 0, 0, 0 };

            // Use the main bounding box for the NUD.
            if (modelContainer.NUD.boundingBox[3] > boundingBox[3])
            {
                boundingBox[0] = modelContainer.NUD.boundingBox[0];
                boundingBox[1] = modelContainer.NUD.boundingBox[1];
                boundingBox[2] = modelContainer.NUD.boundingBox[2];
                boundingBox[3] = modelContainer.NUD.boundingBox[3];
            }

            // It's possible that only the individual meshes have bounding boxes.
            foreach (NUD.Mesh mesh in modelContainer.NUD.Nodes)
            {
                if (mesh.boundingBox[3] > boundingBox[3])
                {
                    boundingBox[0] = mesh.boundingBox[0];
                    boundingBox[1] = mesh.boundingBox[1];
                    boundingBox[2] = mesh.boundingBox[2];
                    boundingBox[3] = mesh.boundingBox[3];
                }
            }

            camera.FrameBoundingSphere(new Vector3(boundingBox[0], boundingBox[1], boundingBox[2]), boundingBox[3]);
            camera.Update();
        }

        private void FrameSelectedMesh()
        {
            NUD.Mesh mesh = (NUD.Mesh)meshList.filesTreeView.SelectedNode;
            float[] boundingBox = mesh.boundingBox;
            camera.FrameBoundingSphere(new Vector3(boundingBox[0], boundingBox[1], boundingBox[2]), boundingBox[3]);
            camera.Update();
        }

        private void FrameSelectedNud()
        {
            NUD nud = (NUD)meshList.filesTreeView.SelectedNode;
            float[] boundingBox = nud.boundingBox;
            camera.FrameBoundingSphere(new Vector3(boundingBox[0], boundingBox[1], boundingBox[2]), boundingBox[3]);
            camera.Update();
        }

        private void FrameSelectedPolygon()
        {
            NUD.Mesh mesh = (NUD.Mesh)meshList.filesTreeView.SelectedNode.Parent;
            float[] boundingBox = mesh.boundingBox;
            camera.FrameBoundingSphere(new Vector3(boundingBox[0], boundingBox[1], boundingBox[2]), boundingBox[3]);
            camera.Update();
        }

        private void FrameAllModelContainers(float maxBoundingRadius = 400)
        {
            // Find the max NUD bounding box for all models. 
            float[] boundingBox = new float[] { 0, 0, 0, 0 };
            foreach (TreeNode node in meshList.filesTreeView.Nodes)
            {
                if (node is ModelContainer)
                {
                    ModelContainer modelContainer = (ModelContainer)node;

                    // Use the main bounding box for the NUD.
                    if ((modelContainer.NUD.boundingBox[3] > boundingBox[3]) && (modelContainer.NUD.boundingBox[3] < maxBoundingRadius))
                    {
                        boundingBox[0] = modelContainer.NUD.boundingBox[0];
                        boundingBox[1] = modelContainer.NUD.boundingBox[1];
                        boundingBox[2] = modelContainer.NUD.boundingBox[2];
                        boundingBox[3] = modelContainer.NUD.boundingBox[3];
                    }

                    // It's possible that only the individual meshes have bounding boxes.
                    foreach (NUD.Mesh mesh in modelContainer.NUD.Nodes)
                    {
                        if (mesh.boundingBox[3] > boundingBox[3] && mesh.boundingBox[3] < maxBoundingRadius)
                        {
                            boundingBox[0] = mesh.boundingBox[0];
                            boundingBox[1] = mesh.boundingBox[1];
                            boundingBox[2] = mesh.boundingBox[2];
                            boundingBox[3] = mesh.boundingBox[3];
                        }
                    }
                }

            }

            camera.FrameBoundingSphere(new Vector3(boundingBox[0], boundingBox[1], boundingBox[2]), boundingBox[3]);
            camera.Update();
        }

        #region Moveset

        public void HandleACMD(string animname)
        {
            //if (ACMDEditor.manualCrc)
            //    return;

            var crc = Crc32.Compute(animname.Replace(".omo", "").ToLower());

            scriptId = -1;

            if (MovesetManager == null)
            {
                this.acmdScript = null;
                return;
            }

            // Try and set up the editor
            try
            {
                if (acmdEditor.crc != crc)
                    acmdEditor.SetAnimation(crc);
            }
            catch { }

            //Putting scriptId here to get intangibility of the animation, previous method only did it for animations that had game scripts
            if (MovesetManager.ScriptsHashList.Contains(crc))
                scriptId = MovesetManager.ScriptsHashList.IndexOf(crc);

            // Game script specific processing stuff below here
            if (!MovesetManager.Game.Scripts.ContainsKey(crc))
            {
                //Some characters don't have AttackS[3-4]S and use attacks[3-4] crc32 hash on scripts making forge unable to access the script, thus not visualizing these hitboxes
                //If the character doesn't have angled ftilt/fsmash
                if (animname == "AttackS4S.omo" || animname == "AttackS3S.omo")
                {
                    HandleACMD(animname.Replace("S.omo", ".omo"));
                    return;
                }
                //Ryu ftilts
                else if (animname == "AttackS3Ss.omo")
                {
                    HandleACMD(animname.Replace("Ss.omo", "s.omo"));
                    return;
                }
                else if (animname == "AttackS3Sw.omo")
                {
                    HandleACMD(animname.Replace("Sw.omo", "w.omo"));
                    return;
                }
                //Rapid Jab Finisher
                else if (animname == "AttackEnd.omo")
                {
                    HandleACMD("Attack100End.omo");
                    return;
                }
                else if (animname.Contains("ZeldaPhantomMainPhantom"))
                {
                    HandleACMD(animname.Replace("ZeldaPhantomMainPhantom", ""));
                    return;
                }
                else if (animname == "SpecialHi1.omo")
                {
                    HandleACMD("SpecialHi.omo");
                    return;
                }
                else if (animname == "SpecialAirHi1.omo")
                {
                    HandleACMD("SpecialAirHi.omo");
                    return;
                }
                else
                {
                    this.acmdScript = null;
                    hitboxList.refresh();
                    variableViewer.refresh();
                    return;
                }
            }

            ACMDScript acmdScript = (ACMDScript)MovesetManager.Game.Scripts[crc];
            // Only update the script if it changed
            if (acmdScript != null)
            {
                // If script wasn't set, or it was set and it changed, load the new script
                if (this.acmdScript == null || (this.acmdScript != null && this.acmdScript.script != acmdScript))
                {
                    this.acmdScript = new ForgeACMDScript(acmdScript);
                }
            }
            else
                this.acmdScript = null;
        }

        #endregion


        private void CameraSettings_Click(object sender, EventArgs e)
        {
            if(cameraPosForm == null)
                cameraPosForm = new GUI.Menus.CameraSettings(camera);
            cameraPosForm.ShowDialog();
        }

        private void glViewport_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (currentMode != Mode.Selection && !freezeCamera)
                camera.Update();
        }

        #region Controls

        public void HideAll()
        {
            lvdEditor.Visible = false;
            lvdList.Visible = false;
            meshList.Visible = false;
            animListPanel.Visible = false;
            acmdEditor.Visible = false;
            vertexTool.Visible = false;
            totalFrame.Enabled = false;
        }

        private void ViewComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            HideAll();
            // Use a string so the order of the items can be changed later. 
            switch (ViewComboBox.SelectedItem.ToString())
            {
                case "Model Viewer":
                    meshList.Visible = true;
                    animListPanel.Visible = true;
                    break;
                case "Model Editor":
                    meshList.Visible = true;
                    vertexTool.Visible = true;
                    break;
                case "Animation Editor":
                    animListPanel.Visible = true;
                    totalFrame.Enabled = true;
                    break;
                case "LVD Editor":
                    lvdEditor.Visible = true;
                    lvdList.Visible = true;
                    break;
                case "ACMD Editor":
                    animListPanel.Visible = true;
                    acmdEditor.Visible = true;
                    break;
                case "Clean":
                    lvdEditor.Visible = false;
                    lvdList.Visible = false;
                    meshList.Visible = false;
                    animListPanel.Visible = false;
                    acmdEditor.Visible = false;
                    vertexTool.Visible = false;
                    totalFrame.Enabled = false;
                    break;
            }
        }

        private void BatchRenderModels()
        {
            // Ignore warnings.
            Runtime.checkNudTexIdOnOpen = false;

            // Get the source model folder and then the output folder. 
            using (var folderSelect = new FolderSelectDialog())
            {
                folderSelect.Title = "Models Directory";
                if (folderSelect.ShowDialog() == DialogResult.OK)
                {
                    string[] files = Directory.GetFiles(folderSelect.SelectedPath, "*model.nud", SearchOption.AllDirectories);

                    using (var outputFolderSelect = new FolderSelectDialog())
                    {
                        outputFolderSelect.Title = "Output Renders Directory";
                        if (outputFolderSelect.ShowDialog() == DialogResult.OK)
                        {
                            for (int i = 0; i < files.Length; i++)
                            {
                                try
                                {
                                    MainForm.Instance.OpenNud(files[i], "", this);
                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine(e.Message);
                                    Debug.WriteLine(e.StackTrace);
                                }
                                BatchRenderViewportToFile(files[i], folderSelect.SelectedPath, outputFolderSelect.SelectedPath);

                                // Cleanup the models and nodes but keep the same viewport.
                                ClearModelContainers();
                            }
                        }
                    }
                }
            }

            Runtime.checkNudTexIdOnOpen = true;
        }

        private void BatchRenderStages()
        {
            // Get the source model folder and then the output folder. 
            using (var sourceFolderSelect = new FolderSelectDialog())
            {
                sourceFolderSelect.Title = "Stages Directory";
                if (sourceFolderSelect.ShowDialog() == DialogResult.OK)
                {
                    using (var outputFolderSelect = new FolderSelectDialog())
                    {
                        outputFolderSelect.Title = "Output Renders Directory";
                        if (outputFolderSelect.ShowDialog() == DialogResult.OK)
                        {
                            foreach (string stageFolder in Directory.GetDirectories(sourceFolderSelect.SelectedPath))
                            {
                                MainForm.Instance.OpenStageFolder(stageFolder, this);
                                BatchRenderViewportToFile(stageFolder, sourceFolderSelect.SelectedPath, outputFolderSelect.SelectedPath);
                                MainForm.Instance.ClearWorkSpace(false);
                                ClearModelContainers();
                            }
                        }
                    }
                }
            }
        }

        private void BatchRenderViewportToFile(string nudFileName, string sourcePath, string outputPath)
        {
            SetupNextRender();
            string renderName = ConvertDirSeparatorsToUnderscore(nudFileName, sourcePath);
            // Manually dispose the bitmap to avoid memory leaks. 
            Bitmap screenCapture = FramebufferTools.ReadFrameBufferPixels(0, FramebufferTarget.Framebuffer, fboRenderWidth, fboRenderHeight, true);
            screenCapture.Save(outputPath + "\\" + renderName + ".png");
            screenCapture.Dispose();
        }

        private void SetupNextRender()
        {
            // Setup before rendering the model. Use a large max radius to show skybox models.
            FrameAllModelContainers();
            Render(null, null);
            glViewport.SwapBuffers();
        }

        public static string ConvertDirSeparatorsToUnderscore(string fullPath, string sourceDirPath)
        {
            // Save the render using the folder structure as the name.
            string renderName = fullPath.Replace(sourceDirPath, "");
            renderName = renderName.Substring(1);
            renderName = renderName.Replace("\\", "_");
            renderName = renderName.Replace("//", "_");
            renderName = renderName.Replace(".nud", "");
            return renderName;
        }

        private void GIFButton_Click(object sender, EventArgs e)
        {
            if (currentAnimation == null)
                return;

            List<Bitmap> images = new List<Bitmap>();
            float ScaleFactor = 1f;
            isPlaying = false;
            playButton.Text = "Play";

            GIFSettings settings = new GIFSettings((int)totalFrame.Value, ScaleFactor, images.Count > 0);
            settings.ShowDialog();

            if (settings.ClearFrames)
                images.Clear();

            if (!settings.OK)
                return;

            ScaleFactor = settings.ScaleFactor;

            int cFrame = (int)currentFrame.Value; //Get current frame so at the end of capturing all frames of the animation it goes back to this frame
                                                    //Disable controls
            this.Enabled = false;

            for (int i = settings.StartFrame; i <= settings.EndFrame + 1; i++)
            {
                currentFrame.Value = i;
                currentFrame.Refresh(); //Refresh the frame counter control
                Render(null, null);

                if (i != settings.StartFrame) //On i=StartFrame it captures the frame the user had before setting frame to it so ignore that one, the +1 on the for makes it so the last frame is captured
                {
                    Bitmap cs = FramebufferTools.ReadFrameBufferPixels(0, FramebufferTarget.Framebuffer, fboRenderWidth, fboRenderWidth);
                    images.Add(new Bitmap(cs, new Size((int)(cs.Width / ScaleFactor), (int)(cs.Height / settings.ScaleFactor)))); //Resize images
                    cs.Dispose();
                }
            }


            if (images.Count > 0 && !settings.StoreFrames)
            {
                SaveFileDialog sf = new SaveFileDialog();

                sf.FileName = "Render.gif";
                sf.Filter = "GIF file (*.gif)|*.gif";

                if (sf.ShowDialog() == DialogResult.OK)
                {
                    GIFProgress g = new GIFProgress(images, sf.FileName, animationSpeed, settings.Repeat, settings.Quality);
                    g.Show();
                }

                images = new List<Bitmap>();

            }
            //Enable controls
            this.Enabled = true;

            currentFrame.Value = cFrame;
        }

        private void ModelViewport_FormClosed(object sender, FormClosedEventArgs e)
        {
            ClearModelContainers();
            Texture.ClearTexturesFlaggedForDeletion(); // Resources already freed.
        }

        public void ClearModelContainers()
        {
            foreach (TreeNode node in meshList.filesTreeView.Nodes)
            {
                if (node is ModelContainer)
                {
                    Runtime.TextureContainers.Remove(((ModelContainer)node).NUT);
                    ((ModelContainer)node).NUT.Destroy();
                    ((ModelContainer)node).NUD.Destroy();
                }
            }

            draw.Clear();
            GC.Collect();
        }

        private void beginButton_Click(object sender, EventArgs e)
        {
            currentFrame.Value = 0;
        }

        private void endButton_Click(object sender, EventArgs e)
        {
            currentFrame.Value = totalFrame.Value;
        }

        private void nextButton_Click(object sender, EventArgs e)
        {
            if (currentFrame.Value != totalFrame.Value)
                currentFrame.Value++;
        }

        private void prevButton_Click(object sender, EventArgs e)
        {
            if(currentFrame.Value != 0)
                currentFrame.Value--;
        }

        private void viewStripButtonsBone(object sender, EventArgs e)
        {
            stripPos.Checked = false;
            stripRot.Checked = false;
            stripSca.Checked = false;
            ((ToolStripButton)sender).Checked = true;
            if (stripPos.Checked)
                transformTool.Type = TransformTool.ToolTypes.POSITION;
            if (stripRot.Checked)
                transformTool.Type = TransformTool.ToolTypes.ROTATION;
            if (stripSca.Checked)
                transformTool.Type = TransformTool.ToolTypes.SCALE;
        }

        #endregion

        private void ModelViewport_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach(TreeNode n in meshList.filesTreeView.Nodes)
            {
                if(n is ModelContainer)
                {
                    ((ModelContainer)n).NUD.Dispose();
                    ((ModelContainer)n).NUT.Destroy();
                }
            }
        }

        private void toolStripSaveRenderAlphaButton_Click(object sender, EventArgs e)
        {
            SaveScreenRender(true);
        }

        private void toolStripRenderNoAlphaButton_Click(object sender, EventArgs e)
        {
            SaveScreenRender(false);
        }
        
        private void totalFrame_ValueChanged(object sender, EventArgs e)
        {
            if (currentAnimation == null) return;
            if(totalFrame.Value < 1)
            {
                totalFrame.Value = 1;
            }else
            {
                if(currentAnimation.Tag is Animation)
                    ((Animation)currentAnimation.Tag).FrameCount = (int)totalFrame.Value;
                currentAnimation.FrameCount = (int)totalFrame.Value;
                animationTrackBar.Value = 0;
                animationTrackBar.SetRange(0, currentAnimation.FrameCount);
            }
        }

        private void checkSelect()
        {
            if (currentMode == Mode.Selection)
            {
                Vector2 m = GetMouseOnViewport();
                if (!m.Equals(new Vector2(sx1, sy1)))
                {
                    // select group of vertices
                    float minx = Math.Min(sx1, m.X);
                    float miny = Math.Min(sy1, m.Y);
                    float width = Math.Abs(sx1 - m.X);
                    float height = Math.Abs(sy1 - m.Y);

                    foreach (TreeNode node in draw)
                    {
                        if (!(node is ModelContainer)) continue;
                        ModelContainer con = (ModelContainer)node;
                        foreach (NUD.Mesh mesh in con.NUD.Nodes)
                        {
                            foreach (NUD.Polygon poly in mesh.Nodes)
                            {
                                //if (!poly.IsSelected && !mesh.IsSelected) continue;
                                int i = 0;
                                foreach (NUD.Vertex v in poly.vertices)
                                {
                                    if (!OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.ControlLeft))
                                        poly.selectedVerts[i] = 0;
                                    Vector3 n = getScreenPoint(v.pos);
                                    if (n.X >= minx && n.Y >= miny && n.X <= minx + width && n.Y <= miny + height)
                                        poly.selectedVerts[i] = 1;
                                    i++;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // single vertex
                    // Selects the closest vertex
                    Ray r = RenderTools.CreateRay(camera.MvpMatrix, GetMouseOnViewport());
                    Vector3 close = Vector3.Zero;
                    foreach (TreeNode node in draw)
                    {
                        if (!(node is ModelContainer)) continue;
                        ModelContainer con = (ModelContainer)node;
                        NUD.Polygon Close = null;
                        int index = 0;
                        double mindis = 999;
                        foreach (NUD.Mesh mesh in con.NUD.Nodes)
                        {
                            foreach (NUD.Polygon poly in mesh.Nodes)
                            {
                                //if (!poly.IsSelected && !mesh.IsSelected) continue;
                                int i = 0;
                                foreach (NUD.Vertex v in poly.vertices)
                                {
                                    //if (!poly.IsSelected) continue;
                                    if (!OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.ControlLeft))
                                        poly.selectedVerts[i] = 0;

                                    if (r.TrySphereHit(v.pos, 0.2f, out close))
                                    {
                                        double dis = r.Distance(close);
                                        if (dis < mindis)
                                        {
                                            mindis = dis;
                                            Close = poly;
                                            index = i;
                                        }
                                    }
                                    i++;
                                }
                            }
                        }
                        if (Close != null)
                        {
                            Close.selectedVerts[index] = 1;
                        }
                    }
                }

                vertexTool.vertexListBox.BeginUpdate();
                vertexTool.vertexListBox.Items.Clear();
                foreach (TreeNode node in draw)
                {
                    if (!(node is ModelContainer)) continue;
                    ModelContainer con = (ModelContainer)node;
                    foreach (NUD.Mesh mesh in con.NUD.Nodes)
                    {
                        foreach (NUD.Polygon poly in mesh.Nodes)
                        {
                            int i = 0;
                            foreach (NUD.Vertex v in poly.vertices)
                            {
                                if (poly.selectedVerts[i++] == 1)
                                {
                                    vertexTool.vertexListBox.Items.Add(v);
                                }
                            }
                        }
                    }
                }
                vertexTool.vertexListBox.EndUpdate();
                currentMode = Mode.Normal;
            }
        }

        private void glViewport_Click(object sender, EventArgs e)
        {
            
        }

        private void glViewport_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //checkSelect();
        }

        private void weightToolButton_Click(object sender, EventArgs e)
        {
            //vertexTool.Show();
        }

        private void Render(object sender, PaintEventArgs e, int defaultFbo = 0)
        {
            // Don't render if the context and resources aren't setup properly.
            // Watching textures suddenly appear looks weird.
            if (!readyToRender || Runtime.glTexturesNeedRefreshing)
                return;

            SetupViewport();

            // Bind the default framebuffer in case it was set elsewhere.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, defaultFbo);

            // Push all attributes so we don't have to clean up later
            GL.PushAttrib(AttribMask.AllAttribBits);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            // Return early to avoid rendering other stuff. 
            if (meshList.filesTreeView.SelectedNode != null)
            {
                if (meshList.filesTreeView.SelectedNode is BCH_Texture)
                {
                    DrawBchTex();
                    return;
                }
                if (meshList.filesTreeView.SelectedNode is NutTexture)
                {
                    DrawNutTexAndUvs();
                    return;
                }
            }

            if (Runtime.usePostProcessing)
            {
                // Render models and background into an HDR buffer. 
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, colorHdrFbo);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            }

            // The screen quad shader draws its own background gradient.
            // This prevents the second color attachment from having a white background.
            if (Runtime.renderBackGround && !Runtime.usePostProcessing)
                DrawViewportBackground();

            // What even is this...
            if (glViewport.ClientRectangle.Contains(glViewport.PointToClient(Cursor.Position))
             && glViewport.Focused
             && (currentMode == Mode.Normal || (currentMode == Mode.Photoshoot && !freezeCamera))
             && !transformTool.hit)
            {
                camera.Update();
            }

            if (cameraPosForm != null)
                cameraPosForm.ApplyCameraAnimation(camera, animationTrackBar.Value);

            if (Runtime.renderFloor)
                RenderTools.DrawFloor(camera.MvpMatrix);

            // Depth testing isn't set by materials.
            SetDepthTesting();

            if (Runtime.drawNudColorIdPass)
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            DrawModels();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, defaultFbo);

            if (Runtime.usePostProcessing)
            {
                // Draw the texture to the screen into a smaller FBO.
                imageBrightHdrFbo.Bind();
                GL.Viewport(0, 0, imageBrightHdrFbo.Width, imageBrightHdrFbo.Height);
                RenderTools.DrawTexturedQuad(colorHdrTex1, imageBrightHdrFbo.Width, imageBrightHdrFbo.Height);

                // Setup the normal viewport dimensions again.
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, defaultFbo);
                GL.Viewport(0, 0, fboRenderWidth, fboRenderHeight);

                RenderTools.DrawScreenQuadPostProcessing(colorHdrTex0, imageBrightHdrFbo.ColorAttachment0Tex);
            }

            FixedFunctionRendering();

            GL.PopAttrib();
            glViewport.SwapBuffers();
        }

        private static void SetDepthTesting()
        {
            // Allow disabling depth testing for experimental "flat" rendering. 
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            if (!Runtime.useDepthTest)
                GL.Disable(EnableCap.DepthTest);
        }

        private void FixedFunctionRendering()
        {
            RenderTools.Setup3DFixedFunctionRendering(camera.MvpMatrix);

            // Bounding boxes should not render on top.
            if (Runtime.drawAreaLightBoundingBoxes)
                DrawAreaLightBoundingBoxes();

            DrawOverlays();
        }

        private static void DrawViewportBackground()
        {
            Vector3 topColor = ColorTools.Vector4FromColor(Runtime.backgroundGradientTop).Xyz;
            Vector3 bottomColor = ColorTools.Vector4FromColor(Runtime.backgroundGradientBottom).Xyz;

            // Only use the top color for solid color rendering.
            if (Runtime.backgroundStyle == Runtime.BackgroundStyle.Solid)
                RenderTools.DrawQuadGradient(topColor, topColor);
            else
                RenderTools.DrawQuadGradient(topColor, bottomColor);
        }

        private void SetupViewport()
        {
            glViewport.MakeCurrent();
            GL.LoadIdentity();
            GL.Viewport(0, 0, fboRenderWidth, fboRenderHeight);
        }

        private void DrawModels()
        {
            if (Runtime.renderModel || Runtime.renderModelWireframe)
                foreach (TreeNode m in draw)
                    if (m is ModelContainer)
                        ((ModelContainer)m).Render(camera, 0, Matrix4.Zero, camera.MvpMatrix, new Vector2(glViewport.Width, glViewport.Height));

            if (ViewComboBox.SelectedIndex == 1)
                foreach (TreeNode m in draw)
                    if (m is ModelContainer)
                        ((ModelContainer)m).RenderPoints(camera);
        }

        private void DrawOverlays()
        {
            // Clearing the depth buffer allows stuff to render on top of the models.
            GL.Clear(ClearBufferMask.DepthBufferBit);

            if (Runtime.renderLVD)
                lvd.Render();

            if (Runtime.renderBones)
                foreach (ModelContainer m in draw)
                    m.RenderBones();

            // ACMD
            if (paramManager != null && Runtime.renderHurtboxes && draw.Count > 0 && (draw[0] is ModelContainer))
            {
                // Doesn't do anything. ParamManager is always null.
                paramManager.RenderHurtboxes(frame, scriptId, acmdScript, ((ModelContainer)draw[0]).GetVBN());
            }

            if (acmdScript != null && draw.Count > 0 && (draw[0] is ModelContainer))
                acmdScript.Render(((ModelContainer)draw[0]).GetVBN());

            if (ViewComboBox.SelectedIndex == 2)
            {
                DrawBoneTransformTool();
            }

            if (ViewComboBox.SelectedIndex == 1)
            {
                MouseSelectionStuff();
            }

            if (currentMode == Mode.Photoshoot)
            {
                freezeCamera = false;
                if (Keyboard.GetState().IsKeyDown(Key.W) && Mouse.GetState().IsButtonDown(MouseButton.Left))
                {
                    shootX = glViewport.PointToClient(Cursor.Position).X;
                    shootY = glViewport.PointToClient(Cursor.Position).Y;
                    freezeCamera = true;
                }
                RenderTools.DrawPhotoshoot(glViewport, shootX, shootY, shootWidth, shootHeight);
            }
        }

        private void MouseSelectionStuff()
        {
            try
            {
                if (currentMode == Mode.Normal && OpenTK.Input.Mouse.GetState().IsButtonDown(OpenTK.Input.MouseButton.Right))
                {
                    currentMode = Mode.Selection;
                    Vector2 m = GetMouseOnViewport();
                    sx1 = m.X;
                    sy1 = m.Y;
                }
            }
            catch
            {

            }
            if (currentMode == Mode.Selection)
            {
                if (!OpenTK.Input.Mouse.GetState().IsButtonDown(OpenTK.Input.MouseButton.Right))
                {
                    checkSelect();
                    currentMode = Mode.Normal;
                }

                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                GL.LoadIdentity();

                Vector2 m = GetMouseOnViewport();
                GL.Color3(Color.Black);
                GL.LineWidth(2f);
                GL.Begin(PrimitiveType.LineLoop);
                GL.Vertex2(sx1, sy1);
                GL.Vertex2(m.X, sy1);
                GL.Vertex2(m.X, m.Y);
                GL.Vertex2(sx1, m.Y);
                GL.End();

                GL.Color3(Color.White);
                GL.LineWidth(1f);
                GL.Begin(PrimitiveType.LineLoop);
                GL.Vertex2(sx1, sy1);
                GL.Vertex2(m.X, sy1);
                GL.Vertex2(m.X, m.Y);
                GL.Vertex2(sx1, m.Y);
                GL.End();
                GL.PopMatrix();
            }
        }

        private void DrawBoneTransformTool()
        {
            if (modeBone.Checked)
            {
                transformTool.Render(camera, new Ray(camera, glViewport));
                if (transformTool.state == 1)
                    currentMode = Mode.Selection;
                else
                    currentMode = Mode.Normal;
            }

            if (transformTool.HasChanged())
            {
                if (currentAnimation != null && transformTool.b != null)
                {
                    // get the node group for the current bone in animation
                    Animation.KeyNode ThisNode = null;
                    foreach (Animation.KeyNode node in currentAnimation.Bones)
                    {
                        if (node.Text.Equals(transformTool.b.Text))
                        {
                            // found
                            ThisNode = node;
                            break;
                        }
                    }
                    if (ThisNode == null)
                    {
                        ThisNode = new Animation.KeyNode(transformTool.b.Text);
                        currentAnimation.Bones.Add(ThisNode);
                    }

                    // update or add the key frame
                    ThisNode.SetKeyFromBone((float)currentFrame.Value, transformTool.b);
                }
            }
        }

        private void glViewport_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (e.KeyChar == 'f')
                FrameSelectionAndSort();

            if (e.KeyChar == 'i')
            {
                ShaderTools.SetupShaders();
                ShaderTools.SaveErrorLogs();
            }
        }

        private void ModelViewport_KeyDown(object sender, KeyEventArgs e)
        {
            // Super secret commands. I'm probably going to be the only one that uses them anyway...
            if (Keyboard.GetState().IsKeyDown(Key.C) && Keyboard.GetState().IsKeyDown(Key.H) && Keyboard.GetState().IsKeyDown(Key.M))
                BatchRenderModels();

            if (Keyboard.GetState().IsKeyDown(Key.X) && Keyboard.GetState().IsKeyDown(Key.M) && Keyboard.GetState().IsKeyDown(Key.L))
                MaterialXmlBatchExport.ExportAllMaterialsFromFolder();

            if (Keyboard.GetState().IsKeyDown(Key.S) && Keyboard.GetState().IsKeyDown(Key.T) && Keyboard.GetState().IsKeyDown(Key.M))
                BatchRenderStages();

            if (Keyboard.GetState().IsKeyDown(Key.L) && Keyboard.GetState().IsKeyDown(Key.S) && Keyboard.GetState().IsKeyDown(Key.T))
                ParamTools.BatchExportParamValuesAsCsv("light_set");

            if (Keyboard.GetState().IsKeyDown(Key.R) && Keyboard.GetState().IsKeyDown(Key.N) && Keyboard.GetState().IsKeyDown(Key.D))
                ParamTools.BatchExportParamValuesAsCsv("stprm");
        }

        private void RenderStageModels(string stageFolder, string outputPath, string sourcePath)
        {
            string renderPath = stageFolder + "//render";
            if (Directory.Exists(renderPath))
            {
                if (File.Exists(renderPath + "//light_set_param.bin"))
                {
                    try
                    {
                        Runtime.lightSetParam = new LightSetParam(renderPath + "//light_set_param.bin");
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                    }

                }
            }

            string modelPath = stageFolder + "//model//";
            if (Directory.Exists(modelPath))
            {
                // We can assume one NUD per folder. 
                string[] nudFileNames = Directory.GetFiles(modelPath, "*.nud", SearchOption.AllDirectories);
                foreach (string nudFile in nudFileNames)
                {
                    BatchRenderViewportToFile(nudFile, sourcePath, outputPath);
                }
            }
        }

        public void SaveScreenRender(bool saveAlpha = false)
        {
            // Set these dimensions back again before normal rendering so the viewport doesn't look glitchy.
            int oldWidth = glViewport.Width;
            int oldHeight = glViewport.Height;

            // The scissor test is causing issues with viewport resizing. Just disable it for now.
            glViewport.MakeCurrent();
            GL.Disable(EnableCap.ScissorTest);

            // Render screenshots in a higher quality.
            fboRenderWidth = oldWidth * 2;
            fboRenderHeight = oldHeight * 2;

            // Make sure the framebuffers and viewport match the new drawing size.
            ResizeTexturesAndBuffers();
            GL.Viewport(0, 0, fboRenderWidth, fboRenderHeight);

            // Render the viewport.       
            offscreenRenderFbo.Bind();
            Render(null, null, offscreenRenderFbo.Id);

            // Save the render as a PNG.
            Bitmap screenCapture = offscreenRenderFbo.ReadImagePixels(saveAlpha);
            string outputPath = CalculateUniqueName();
            screenCapture.Save(outputPath);

            // Cleanup
            screenCapture.Dispose(); // Manually dispose the bitmap to avoid memory leaks. 
            fboRenderWidth = oldWidth;
            fboRenderHeight = oldHeight;
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private static string CalculateUniqueName()
        {
            // Keep incrementing the number until unique.
            int i = 0;
            string outputPath = MainForm.executableDir + "\\render_" + i + ".png";
            while (File.Exists(outputPath))
            {
                outputPath = MainForm.executableDir + "\\render_" + i + ".png";
                i++;
            }

            return outputPath;
        }

        private void DrawNutTexAndUvs()
        {
            GL.PopAttrib();
            NutTexture tex = ((NutTexture)meshList.filesTreeView.SelectedNode);
            RenderTools.DrawTexturedQuad(((NUT)tex.Parent).glTexByHashId[tex.HASHID].Id, tex.Width, tex.Height);

            if (Runtime.drawUv)
                DrawUvsForSelectedTexture(tex);

            glViewport.SwapBuffers();
        }

        private void DrawBchTex()
        {
            GL.PopAttrib();
            BCH_Texture tex = ((BCH_Texture)meshList.filesTreeView.SelectedNode);
            RenderTools.DrawTexturedQuad(tex.display, tex.Width, tex.Height);
            glViewport.SwapBuffers();
        }

        private void DrawAreaLightBoundingBoxes()
        {
            foreach (AreaLight light in LightTools.areaLights)
            {
                Color color = Color.White;

                RenderTools.DrawRectangularPrism(new Vector3(light.positionX, light.positionY, light.positionZ),
                    light.scaleX, light.scaleY, light.scaleZ, true);
            }
        }

        private void glViewport_Paint(object sender, PaintEventArgs e)
        {
            Render(sender, e);

            // Make sure unused resources get cleaned up.
            Texture.DeleteUnusedTextures();

            // Deleting the context will require all the textures to be reloaded.
            if (Runtime.glTexturesNeedRefreshing)
            {
                RefreshGlTextures();
                Runtime.glTexturesNeedRefreshing = false;
            }
        }

        private void glViewport_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            MouseClickItemSelect(e);
        }

        private void modePolygon_Click(object sender, EventArgs e)
        {
            // These should act like radio buttons.
            modeBone.Checked = false;
            modePolygon.Checked = true;
            modeMesh.Checked = false;
        }

        private void modeMesh_Click(object sender, EventArgs e)
        {
            // These should act like radio buttons.
            modeBone.Checked = false;
            modePolygon.Checked = false;
            modeMesh.Checked = true;
        }

        private void modeBone_Click(object sender, EventArgs e)
        {
            // These should act like radio buttons.
            modeBone.Checked = true;
            modePolygon.Checked = false;
            modeMesh.Checked = false;
        }

        private void glViewport_Load(object sender, EventArgs e)
        {
            glViewport.MakeCurrent();
            if (OpenTK.Graphics.GraphicsContext.CurrentContext != null)
            {
                RenderTools.SetupOpenTkRendering();
                SetupBuffersAndTextures();
                RefreshGlTextures();
                readyToRender = true;
            }
        }

        private void RefreshGlTextures()
        {
            // Regenerate all the texture objects.
            foreach (TreeNode node in meshList.filesTreeView.Nodes)
            {
                if (!(node is ModelContainer))
                    continue;

                ModelContainer m = (ModelContainer)node;

                if (m.NUT != null)
                    m.NUT.RefreshGlTexturesByHashId();
            }
        }

        private void DrawUvsForSelectedTexture(NutTexture tex)
        {
            foreach (TreeNode node in meshList.filesTreeView.Nodes)
            {
                if (!(node is ModelContainer))
                    continue;

                ModelContainer m = (ModelContainer)node;

                int textureHash = 0;
                int.TryParse(tex.Text, NumberStyles.HexNumber, null, out textureHash);
                RenderTools.DrawUv(camera, m.NUD, textureHash, 4, Color.Red, 1, Color.White);
            }
        }
    }
}
