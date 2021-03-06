﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SmashForge
{
    public partial class PolygonSelector : Form
    {
        public PolygonSelector()
        {
            InitializeComponent();
        }

        public List<Nud.Polygon> selected = new List<Nud.Polygon>();
        public bool finished = false;
        List<ModelContainer> ModelContainers;

        private void PolygonSelector_Load(object sender, EventArgs e)
        {
            int modelCount = 0;
            foreach (ModelContainer mc in ModelContainers)
            {
                if (mc.NUD != null)
                {
                    TreeNode model = new TreeNode($"Model {modelCount}") { Tag = mc.NUD };
                    foreach (Nud.Mesh m in mc.NUD.Nodes)
                    {
                        TreeNode mesh = new TreeNode(m.Text) { Tag = m };
                        foreach (Nud.Polygon p in m.Nodes)
                        {
                            TreeNode poly = new TreeNode(p.Text) { Tag = p };
                            mesh.Nodes.Add(poly);
                        }
                        model.Nodes.Add(mesh);
                    }
                    treeView1.Nodes.Add(model);
                }
            }
        }

        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            bool isChecked = (e.Node).Checked;
            if (e.Node.Tag is Nud)
            {
                foreach (TreeNode mesh in e.Node.Nodes)
                {
                    mesh.Checked = isChecked;
                    foreach (TreeNode poly in mesh.Nodes)
                        poly.Checked = isChecked;
                }
            }
            if (e.Node.Tag is Nud.Mesh)
                foreach (TreeNode poly in e.Node.Nodes)
                    poly.Checked = isChecked;
        }

        private void click_ok(object sender, EventArgs e)
        {
            foreach (TreeNode model in treeView1.Nodes)
                foreach (TreeNode mesh in model.Nodes)
                    foreach (TreeNode poly in mesh.Nodes)
                        if (poly.Checked)
                            selected.Add((Nud.Polygon)poly.Tag);
            finished = true;
            Close();
        }

        private void click_cancel(object sender, EventArgs e)
        {
            finished = true;
            Close();
        }

        public static List<Nud.Polygon> Popup(List<ModelContainer> ModelContainer)
        {
            PolygonSelector selector = new PolygonSelector() { ModelContainers = ModelContainer};
            selector.ShowDialog();
            return selector.selected;
        }
    }
}
