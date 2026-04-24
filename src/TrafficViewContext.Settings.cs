using System;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficViewContext
    {
        private void TransparencyItem_Click(object sender, EventArgs e)
        {
            using (TransparencyForm transparencyForm = new TransparencyForm(
                this.settings.TransparencyPercent,
                this.ApplyTransparencySetting))
            {
                bool pausePopupTopMost = this.popupForm.Visible;
                if (pausePopupTopMost)
                {
                    this.popupForm.SuspendTopMostEnforcement();
                }

                try
                {
                    transparencyForm.ShowDialog();
                }
                finally
                {
                    if (pausePopupTopMost)
                    {
                        this.popupForm.ResumeTopMostEnforcement(false);
                    }
                }

                if (this.popupForm.Visible)
                {
                    this.popupForm.BringToFrontOnly();
                }
            }
        }

        private void ApplyTransparencySetting(int transparencyPercent)
        {
            this.settings = this.settings.WithTransparencyPercent(transparencyPercent);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void PopupScaleMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || item.Tag == null)
            {
                return;
            }

            int popupScalePercent;
            if (!int.TryParse(item.Tag.ToString(), out popupScalePercent))
            {
                return;
            }

            this.ApplyPopupScalePercent(popupScalePercent);
        }

        private void DisplayModeMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is PopupDisplayMode))
            {
                return;
            }

            PopupDisplayMode popupDisplayMode = (PopupDisplayMode)item.Tag;
            if (this.settings.PopupDisplayMode == popupDisplayMode)
            {
                return;
            }

            this.settings = this.settings.WithPopupDisplayMode(popupDisplayMode);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void SectionModeMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is PopupSectionMode))
            {
                return;
            }

            PopupSectionMode popupSectionMode = (PopupSectionMode)item.Tag;
            if (this.GetCurrentPopupSectionModeSetting() == popupSectionMode)
            {
                return;
            }

            if (this.settings.TaskbarIntegrationEnabled)
            {
                this.popupForm.ClearTaskbarDefaultSectionModeOverride();
                this.ApplyTaskbarPopupSectionMode(popupSectionMode);
            }
            else
            {
                this.settings = this.settings.WithPopupSectionMode(popupSectionMode);
                this.settings.Save();
                this.popupForm.ApplySettings(this.settings);
                this.UpdateMenuState();
            }

            if (this.popupForm.Visible)
            {
                if (!this.settings.TaskbarIntegrationEnabled)
                {
                    this.settings = this.settings.WithPopupLocation(this.popupForm.Location);
                    this.settings.Save();
                }
                this.popupForm.BringToFrontOnly();
            }

            this.UpdateMenuState();
        }

        private void RotatingGlossItem_Click(object sender, EventArgs e)
        {
            bool nextValue = !this.settings.RotatingMeterGlossEnabled;
            this.settings = this.settings.WithRotatingMeterGlossEnabled(nextValue);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void ActivityBorderGlowItem_Click(object sender, EventArgs e)
        {
            bool nextValue = !this.settings.ActivityBorderGlowEnabled;
            this.settings = this.settings.WithActivityBorderGlowEnabled(nextValue);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void TaskbarIntegrationMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is bool))
            {
                return;
            }

            bool enableTaskbarIntegration = (bool)item.Tag;
            this.SetTaskbarIntegrationEnabled(enableTaskbarIntegration);
        }

        private void PopupForm_TaskbarIntegrationNoSpaceAcknowledged(object sender, EventArgs e)
        {
            this.SetTaskbarIntegrationEnabled(false, true);
        }

        private void PopupForm_TaskbarSectionModeChangeRequested(object sender, TaskbarSectionModeChangeRequestedEventArgs e)
        {
            if (e == null ||
                this.settings == null ||
                !this.settings.TaskbarIntegrationEnabled)
            {
                return;
            }

            this.ApplyTaskbarPopupSectionMode(e.PopupSectionMode);
        }

        private void ApplyTaskbarPopupSectionMode(PopupSectionMode popupSectionMode)
        {
            if (this.settings == null)
            {
                return;
            }

            if (this.settings.TaskbarPopupSectionMode == popupSectionMode)
            {
                return;
            }

            this.settings = this.settings.WithTaskbarPopupSectionMode(popupSectionMode);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void SetTaskbarIntegrationEnabled(bool enableTaskbarIntegration)
        {
            this.SetTaskbarIntegrationEnabled(enableTaskbarIntegration, true);
        }

        private void SetTaskbarIntegrationEnabled(bool enableTaskbarIntegration, bool restorePopupAfterChange)
        {
            if (this.settings.TaskbarIntegrationEnabled == enableTaskbarIntegration)
            {
                return;
            }

            if (enableTaskbarIntegration)
            {
                this.popupForm.ClearTaskbarIntegrationPreferredLocation();
            }

            this.settings = this.settings.WithTaskbarIntegrationEnabled(enableTaskbarIntegration);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);

            if (restorePopupAfterChange &&
                (this.popupForm.Visible || this.popupForm.HasDeferredVisibilityRequest))
            {
                this.ShowPopupAtPreferredOrSavedLocation(null, false);
            }

            this.UpdateMenuState();
        }

        private void ApplyPopupScalePercent(int popupScalePercent)
        {
            if (this.settings.PopupScalePercent == popupScalePercent)
            {
                return;
            }

            this.settings = this.settings.WithPopupScalePercent(popupScalePercent);
            this.popupForm.ApplySettings(this.settings);

            if (this.popupForm.Visible)
            {
                if (!this.settings.TaskbarIntegrationEnabled)
                {
                    this.settings = this.settings.WithPopupLocation(this.popupForm.Location);
                }
                this.popupForm.BringToFrontOnly();
            }

            this.settings.Save();
            this.UpdateMenuState();
        }

        private void LanguageMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            string languageCode = item != null ? item.Tag as string : null;
            if (string.IsNullOrEmpty(languageCode))
            {
                return;
            }

            this.settings = this.settings
                .WithLanguageCode(languageCode)
                .WithInitialLanguagePromptHandled(true);
            this.settings.Save();
            UiLanguage.SetLanguage(this.settings.LanguageCode);
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private string GetPopupDisplayModeDisplayName(PopupDisplayMode popupDisplayMode)
        {
            switch (popupDisplayMode)
            {
                case PopupDisplayMode.MiniGraph:
                    return UiLanguage.Get("Menu.DisplayModeMiniGraph", "MiniGraph");
                case PopupDisplayMode.MiniSoft:
                    return UiLanguage.Get("Menu.DisplayModeMiniSoft", "MiniSoft");
                case PopupDisplayMode.Simple:
                    return UiLanguage.Get("Menu.DisplayModeSimple", "Simple");
                case PopupDisplayMode.SimpleBlue:
                    return UiLanguage.Get("Menu.DisplayModeSimpleBlue", "Simple blue");
                default:
                    return UiLanguage.Get("Menu.DisplayModeStandard", "Standard");
            }
        }

        private string GetPopupSectionModeDisplayName(PopupSectionMode popupSectionMode)
        {
            switch (popupSectionMode)
            {
                case PopupSectionMode.LeftOnly:
                    return UiLanguage.Get("Menu.SectionModeLeftOnly", "Nur links");
                case PopupSectionMode.RightOnly:
                    return UiLanguage.Get("Menu.SectionModeRightOnly", "Nur rechts");
                default:
                    return UiLanguage.Get("Menu.SectionModeBoth", "Beide Teile");
            }
        }

        private PopupSectionMode GetCurrentPopupSectionModeSetting()
        {
            if (this.settings == null)
            {
                return PopupSectionMode.Both;
            }

            return this.settings.TaskbarIntegrationEnabled
                ? this.settings.TaskbarPopupSectionMode
                : this.settings.PopupSectionMode;
        }
    }
}
