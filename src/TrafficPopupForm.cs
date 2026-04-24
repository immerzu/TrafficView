using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm : Form
    {
        public TrafficPopupForm(MonitorSettings initialSettings)
        {
            this.settings = initialSettings.Clone();
            this.currentDpi = DpiHelper.GetDesktopDpi();
            this.recentDownloadSamples = new Queue<double>();
            this.recentUploadSamples = new Queue<double>();
            this.trafficHistory = new Queue<TrafficHistorySample>();

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = this.ShouldUseGlobalTopMost();
            this.Text = "TrafficView";
            this.BackColor = BackgroundBlue;
            this.AutoScaleMode = AutoScaleMode.None;
            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            this.downloadCaptionLabel = CreateCaptionLabel(
                "DL",
                this.GetDownloadCaptionBaseColor());
            this.Controls.Add(this.downloadCaptionLabel);
            this.downloadCaptionLabel.Visible = false;

            this.downloadValueLabel = CreateValueLabel(
                this.GetDownloadValueBaseColor());
            this.Controls.Add(this.downloadValueLabel);
            this.downloadValueLabel.Visible = false;

            this.uploadCaptionLabel = CreateCaptionLabel(
                "UL",
                this.GetUploadCaptionBaseColor());
            this.Controls.Add(this.uploadCaptionLabel);
            this.uploadCaptionLabel.Visible = false;

            this.uploadValueLabel = CreateValueLabel(
                this.GetUploadValueBaseColor());
            this.Controls.Add(this.uploadValueLabel);
            this.uploadValueLabel.Visible = false;

            this.SuppressOverlayContextMenus();
            this.WireSurface(this);

            this.refreshTimer = new Timer();
            this.refreshTimer.Interval = 1000;
            this.refreshTimer.Tick += this.RefreshTimer_Tick;
            this.refreshTimer.Start();

            this.animationTimer = new Timer();
            this.animationTimer.Interval = 140;
            this.animationTimer.Tick += this.AnimationTimer_Tick;
            this.animationTimer.Enabled = false;

            this.topMostGuardTimer = new Timer();
            this.topMostGuardTimer.Interval = KeepTopMostRefreshIntervalMs;
            this.topMostGuardTimer.Tick += this.TopMostGuardTimer_Tick;
            this.topMostGuardTimer.Enabled = this.Visible &&
                !this.IsTopMostEnforcementPaused &&
                this.ShouldUseGlobalTopMost();

            this.taskbarMonitorTimer = new Timer();
            this.taskbarMonitorTimer.Interval = TaskbarMonitorIntervalMs;
            this.taskbarMonitorTimer.Tick += this.TaskbarMonitorTimer_Tick;
            this.taskbarMonitorTimer.Enabled = false;

            this.taskbarRefreshDebounceTimer = new Timer();
            this.taskbarRefreshDebounceTimer.Interval = TaskbarRefreshDebounceMs;
            this.taskbarRefreshDebounceTimer.Tick += this.TaskbarRefreshDebounceTimer_Tick;
            this.taskbarRefreshDebounceTimer.Enabled = false;

            this.manualDragMoveTimer = new Timer();
            this.manualDragMoveTimer.Interval = ManualDragMoveIntervalMs;
            this.manualDragMoveTimer.Tick += this.ManualDragMoveTimer_Tick;
            this.manualDragMoveTimer.Enabled = false;

            this.ApplyDpiLayout(this.currentDpi);
            this.ApplySettings(this.settings);
        }

    }

}
