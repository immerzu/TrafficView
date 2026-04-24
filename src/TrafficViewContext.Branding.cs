using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficViewContext
    {
        private static Image LoadCompanyLogoImage()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CompanyLogoFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "company-logo-load-failed",
                    string.Format("Firmenlogo konnte nicht aus '{0}' geladen werden.", path),
                    ex);
                return null;
            }
        }

        private static string GetMenuVersionNumber()
        {
            Version version = typeof(Program).Assembly.GetName().Version;
            if (version == null)
            {
                return "?";
            }

            return string.Format(
                "{0}.{1}.{2:00}",
                version.Major,
                version.Minor,
                version.Build);
        }

        private string GetMenuVersionText()
        {
            return UiLanguage.Format(
                "Menu.VersionFormat",
                "Version {0}",
                this.menuVersionNumber);
        }

        private ToolStripControlHost CreateCompanyLogoHost(Image logoImage, string versionText)
        {
            if (logoImage == null)
            {
                return null;
            }

            Image trimmedLogoImage = TrimMenuLogoImage(logoImage);
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Size = new Size(286, 72);
            panel.Margin = Padding.Empty;
            panel.Padding = Padding.Empty;
            panel.BackColor = SystemColors.Menu;
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel logoPanel = new Panel();
            logoPanel.Dock = DockStyle.Fill;
            logoPanel.Margin = Padding.Empty;
            logoPanel.Padding = Padding.Empty;
            logoPanel.BackColor = SystemColors.Menu;
            logoPanel.Cursor = Cursors.Hand;
            logoPanel.Click += this.CompanyLogo_Click;

            int logoWidth = Math.Min(82, Math.Max(1, (int)Math.Round(72D * trimmedLogoImage.Width / trimmedLogoImage.Height)));
            PictureBox pictureBox = new PictureBox();
            pictureBox.Location = new Point(0, 0);
            pictureBox.Size = new Size(logoWidth, 72);
            pictureBox.Margin = Padding.Empty;
            pictureBox.Padding = Padding.Empty;
            pictureBox.TabStop = false;
            pictureBox.Image = trimmedLogoImage;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.BackColor = SystemColors.Menu;
            pictureBox.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            pictureBox.Cursor = Cursors.Hand;
            pictureBox.Click += this.CompanyLogo_Click;
            logoPanel.Controls.Add(pictureBox);

            Label versionLabel = new Label();
            versionLabel.Dock = DockStyle.Fill;
            versionLabel.Margin = Padding.Empty;
            versionLabel.Padding = new Padding(0, 0, 8, 0);
            versionLabel.AutoSize = false;
            versionLabel.Text = string.IsNullOrWhiteSpace(versionText) ? "?" : versionText;
            versionLabel.TextAlign = ContentAlignment.MiddleCenter;
            versionLabel.Font = new Font(
                this.sharedMenu.Font.FontFamily,
                this.sharedMenu.Font.Size + 0.5F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            versionLabel.BackColor = SystemColors.Menu;
            this.menuVersionLabel = versionLabel;

            panel.Controls.Add(logoPanel, 0, 0);
            panel.Controls.Add(versionLabel, 1, 0);

            ToolStripControlHost host = new ToolStripControlHost(panel);
            host.AutoSize = false;
            host.Size = panel.Size;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            return host;
        }

        private void CompanyLogo_Click(object sender, EventArgs e)
        {
            if (this.sharedMenu != null && this.sharedMenu.Visible)
            {
                this.sharedMenu.Close(ToolStripDropDownCloseReason.ItemClicked);
            }

            this.ShowCompanyLogoWindow();
        }

        private void ShowCompanyLogoWindow()
        {
            if (this.companyLogoImage == null)
            {
                return;
            }

            bool pausePopupTopMost = this.popupForm.Visible;
            if (pausePopupTopMost)
            {
                this.popupForm.SuspendTopMostEnforcement();
            }

            try
            {
                Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
                int clientWidth = Math.Min(this.companyLogoImage.Width, Math.Max(320, workingArea.Width - 80));
                int clientHeight = Math.Min(this.companyLogoImage.Height, Math.Max(240, workingArea.Height - 80));

                using (Form logoForm = new Form())
                using (Panel containerPanel = new Panel())
                using (PictureBox pictureBox = new PictureBox())
                {
                    logoForm.Text = "LOLO-SOFT";
                    logoForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    logoForm.StartPosition = FormStartPosition.CenterScreen;
                    logoForm.ShowInTaskbar = false;
                    logoForm.MaximizeBox = false;
                    logoForm.MinimizeBox = false;
                    logoForm.AutoScaleMode = AutoScaleMode.Dpi;
                    logoForm.Font = this.sharedMenu.Font;
                    logoForm.ClientSize = new Size(clientWidth, clientHeight);
                    logoForm.TopMost = true;

                    containerPanel.Dock = DockStyle.Fill;
                    containerPanel.AutoScroll = true;
                    containerPanel.BackColor = Color.White;
                    containerPanel.Padding = Padding.Empty;

                    pictureBox.Location = new Point(0, 0);
                    pictureBox.Size = this.companyLogoImage.Size;
                    pictureBox.Margin = Padding.Empty;
                    pictureBox.Padding = Padding.Empty;
                    pictureBox.Image = this.companyLogoImage;
                    pictureBox.SizeMode = PictureBoxSizeMode.Normal;
                    pictureBox.BackColor = Color.White;

                    containerPanel.Controls.Add(pictureBox);
                    logoForm.Controls.Add(containerPanel);

                    if (this.popupForm.Visible)
                    {
                        logoForm.ShowDialog(this.popupForm);
                    }
                    else
                    {
                        logoForm.ShowDialog();
                    }
                }
            }
            finally
            {
                if (pausePopupTopMost)
                {
                    this.popupForm.ResumeTopMostEnforcement(false);
                }
            }
        }

        private static Image TrimMenuLogoImage(Image logoImage)
        {
            Bitmap sourceBitmap = logoImage as Bitmap;
            if (sourceBitmap == null)
            {
                return logoImage;
            }

            int minX = sourceBitmap.Width;
            int minY = sourceBitmap.Height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < sourceBitmap.Height; y++)
            {
                for (int x = 0; x < sourceBitmap.Width; x++)
                {
                    Color pixel = sourceBitmap.GetPixel(x, y);
                    if (pixel.A <= 8)
                    {
                        continue;
                    }

                    if (pixel.R >= 245 && pixel.G >= 245 && pixel.B >= 245)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return logoImage;
            }

            Rectangle cropBounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            return sourceBitmap.Clone(cropBounds, sourceBitmap.PixelFormat);
        }
    }
}
