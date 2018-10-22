﻿using OpenTK;
using SFGraphics.Utils;
using System;
using System.Drawing;
using System.Windows.Forms;


namespace Smash_Forge.GUI.Menus
{
    public partial class ColorEditor : Form
    {
        float hue = 360.0f;
        float saturation = 1.0f;
        float value = 1.0f;
        float R = 1.0f;
        float G = 1.0f;
        float B = 1.0f;
        float colorTemp = 6500.0f;

        const float maxRgb = 2;
        const float maxHue = 360;
        const float maxValue = 2;
        const float maxSat = 1;
        const float maxTemp = 10000;
        string numFormat = "0.000";

        Vector3 color;

        public ColorEditor(Vector3 color)
        {
            InitializeComponent();

            R = color.X;
            G = color.Y;
            B = color.Z;
            ColorUtils.RgbToHsv(R, G, B, out hue, out saturation, out value);
            modeComboBox.SelectedIndex = 0;
            colorXTB.Text = R.ToString(numFormat);
            colorYTB.Text = G.ToString(numFormat);
            colorZTB.Text = G.ToString(numFormat);
            this.color = color;
        }

        public Vector3 GetColor()
        {
            color.X = R;
            color.Y = G;
            color.Z = B;
            return color;
        }

        private void colorXTB_TextChanged(object sender, EventArgs e)
        {
            float newValue = GuiTools.TryParseTBFloat(colorXTB);
            switch (modeComboBox.SelectedItem.ToString())
            {
                default:
                    break;
                case "RGB":
                    R = newValue;
                    UpdateValuesFromRgb();
                    break;
                case "HSV":
                    hue = newValue;
                    UpdateValuesFromHsv();
                    break;
            }
        }

        private void colorYTB_TextChanged(object sender, EventArgs e)
        {
            float newValue = GuiTools.TryParseTBFloat(colorYTB);
            switch (modeComboBox.SelectedItem.ToString())
            {
                default:
                    break;
                case "RGB":
                    G = newValue;
                    UpdateValuesFromRgb();
                    break;
                case "HSV":
                    saturation = newValue;
                    UpdateValuesFromHsv();
                    break;
            }
        }

        private void colorZTB_TextChanged(object sender, EventArgs e)
        {
            float newValue = GuiTools.TryParseTBFloat(colorZTB);
            switch (modeComboBox.SelectedItem.ToString())
            {
                default:
                    break;
                case "RGB":
                    B = newValue;
                    UpdateValuesFromRgb();
                    break;
                case "HSV":
                    value = newValue;
                    UpdateValuesFromHsv();
                    break;
            }
        }


        private void useColorTempCB_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void colorTempTrackBar_Scroll(object sender, EventArgs e)
        {

        }

        private void colorWTB_TextChanged(object sender, EventArgs e) 
        {
            R = GuiTools.TryParseTBFloat(colorWTB);
            UpdateValuesFromRgb();
        }

        private void redTrackBar_Scroll(object sender, EventArgs e)
        {
            colorWTB.Text = GuiTools.GetTrackBarValue(colorTrackBarW, 0, maxRgb).ToString(numFormat);
        }

        private void colorTrackBar_Scroll(object sender, EventArgs e)
        {

        }

        private void UpdateValuesFromRgb()
        {
            ColorUtils.RgbToHsv(R, G, B, out hue, out saturation, out value);
            UpdateColorTrackBars();
            UpdateButtonColor();
        }

        private void UpdateValuesFromHsv()
        {
            ColorUtils.HsvToRgb(hue, saturation, value, out R, out G, out B);
            UpdateColorTrackBars();
            UpdateButtonColor();
        }

        private void UpdateValuesFromTemp()
        {
            ColorUtils.GetRgb(colorTemp, out R, out G, out B);
            UpdateValuesFromRgb();
            UpdateColorTrackBars();
            UpdateButtonColor();
        }

        private void UpdateButtonColor()
        {
            colorButton.BackColor = ColorUtils.GetColor(R, G, B);
        }

        private void UpdateColorTrackBars()
        {
            switch (modeComboBox.SelectedItem.ToString())
            {
                default:
                    break;
                case "RGB":
                    GuiTools.UpdateTrackBarFromValue(R, colorTrackBarX, 0, maxRgb);
                    GuiTools.UpdateTrackBarFromValue(G, colorTrackBarY, 0, maxRgb);
                    GuiTools.UpdateTrackBarFromValue(B, colorTrackBarZ, 0, maxRgb);
                    break;
                case "HSV":
                    GuiTools.UpdateTrackBarFromValue(hue, colorTrackBarX, 0, maxHue);
                    GuiTools.UpdateTrackBarFromValue(saturation, colorTrackBarY, 0, maxSat);
                    GuiTools.UpdateTrackBarFromValue(value, colorTrackBarZ, 0, maxValue);
                    break;
            }         
        }

        private void colorTrackBarX_Scroll(object sender, EventArgs e)
        {
            switch (modeComboBox.SelectedItem.ToString())
            {
                default:
                    break;
                case "RGB":
                    colorXTB.Text = GuiTools.GetTrackBarValue(colorTrackBarX, 0, maxRgb).ToString(numFormat);
                    break;
                case "HSV":
                    colorXTB.Text = GuiTools.GetTrackBarValue(colorTrackBarX, 0, maxHue).ToString(numFormat); 
                    break;
            }
        }

        private void colorTrackBarY_Scroll(object sender, EventArgs e)
        {
            switch (modeComboBox.SelectedItem.ToString())
            {
                default:
                    break;
                case "RGB":
                    colorYTB.Text = GuiTools.GetTrackBarValue(colorTrackBarY, 0, maxRgb).ToString(numFormat);
                    break;
                case "HSV":
                    colorYTB.Text = GuiTools.GetTrackBarValue(colorTrackBarY, 0, maxSat).ToString(numFormat);
                    break;
            }
        }

        private void colorTrackBarZ_Scroll(object sender, EventArgs e)
        {
            switch (modeComboBox.SelectedItem.ToString())
            {
                default:
                    break;
                case "RGB":
                    colorZTB.Text = GuiTools.GetTrackBarValue(colorTrackBarZ, 0, maxRgb).ToString(numFormat);
                    break;
                case "HSV":
                    colorZTB.Text = GuiTools.GetTrackBarValue(colorTrackBarZ, 0, maxValue).ToString(numFormat);
                    break;
            }
        }

        private void editModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (modeComboBox.SelectedItem.ToString())
            {
                default:
                    break;
                case "RGB":
                    colorXTB.Text = R.ToString(numFormat);
                    colorYTB.Text = G.ToString(numFormat);
                    colorZTB.Text = B.ToString(numFormat);
                    colorLabelX.Text = "Red";
                    colorLabelY.Text = "Green";
                    colorLabelZ.Text = "Blue";
                    colorLabelX.Visible = true;
                    colorLabelY.Visible = true;
                    colorLabelZ.Visible = true;
                    colorLabelW.Visible = false;
                    colorTrackBarX.Visible = true;
                    colorTrackBarY.Visible = true;
                    colorTrackBarZ.Visible = true;
                    colorTrackBarW.Visible = false;
                    colorXTB.Visible = true;
                    colorYTB.Visible = true;
                    colorZTB.Visible = true;
                    colorWTB.Visible = false;
                    break;
                case "HSV":
                    colorXTB.Text = hue.ToString(numFormat);
                    colorYTB.Text = saturation.ToString(numFormat);
                    colorZTB.Text = value.ToString(numFormat);
                    colorLabelX.Text = "Hue";
                    colorLabelY.Text = "Saturation";
                    colorLabelZ.Text = "Value";
                    colorLabelX.Visible = true;
                    colorLabelY.Visible = true;
                    colorLabelZ.Visible = true;
                    colorLabelW.Visible = false;
                    colorTrackBarX.Visible = true;
                    colorTrackBarY.Visible = true;
                    colorTrackBarZ.Visible = true;
                    colorTrackBarW.Visible = false;
                    colorXTB.Visible = true;
                    colorYTB.Visible = true;
                    colorZTB.Visible = true;
                    colorWTB.Visible = false;
                    break;
                case "Temperature (K)":
                    colorLabelX.Text = "Temp";
                    colorLabelX.Visible = true;
                    colorLabelY.Visible = false;
                    colorLabelZ.Visible = false;
                    colorLabelW.Visible = false;
                    colorTrackBarX.Visible = true;
                    colorTrackBarY.Visible = false;
                    colorTrackBarZ.Visible = false;
                    colorTrackBarW.Visible = false;
                    colorXTB.Visible = true;
                    colorYTB.Visible = false;
                    colorZTB.Visible = false;
                    colorWTB.Visible = false;
                    break;
            }
        }
    }
}
