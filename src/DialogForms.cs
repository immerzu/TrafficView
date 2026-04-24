using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed class LanguageSelectionForm : Form
    {
        private readonly ComboBox languageComboBox;
        private readonly Button saveButton;
        private bool allowClose;

        public LanguageSelectionForm(string selectedLanguageCode)
        {
            this.Text = UiLanguage.Get("StartupLanguage.Title", "Programmsprache");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = SystemFonts.MessageBoxFont;
            this.TopMost = true;
            int compactSpacing = Math.Max(8, this.Font.Height / 2);
            int sectionSpacing = Math.Max(12, this.Font.Height - 1);
            int contentTop = Math.Max(14, this.Font.Height);
            int horizontalPadding = Math.Max(16, this.Font.Height + 4);
            int bottomPadding = Math.Max(18, compactSpacing + 10);
            int infoMinimumHeight = Math.Max(48, (this.Font.Height * 2) + (compactSpacing * 2));
            int languageLabelTextHorizontalPadding = Math.Max(8, compactSpacing + 2);
            int languageLabelTextVerticalPadding = Math.Max(4, (compactSpacing / 2) + 1);
            int languageLabelMinimumHeight = Math.Max(24, this.Font.Height + languageLabelTextVerticalPadding + 4);
            int languageLabelMinimumWidth = Math.Max(120, (this.Font.Height * 6) + languageLabelTextHorizontalPadding + 18);
            int comboBoxMinimumHeight = Math.Max(30, this.languageComboBox != null ? this.languageComboBox.PreferredHeight + 6 : this.Font.Height + 14);
            int saveButtonHorizontalPadding = Math.Max(10, compactSpacing + 4);
            int saveButtonVerticalPadding = Math.Max(4, (compactSpacing / 2) + 1);
            int saveButtonTextHorizontalPadding = Math.Max(24, (saveButtonHorizontalPadding * 2) + 4);
            int saveButtonTextVerticalPadding = Math.Max(12, (saveButtonVerticalPadding * 2) + 4);
            int saveButtonMinimumWidth = Math.Max(112, (this.Font.Height * 5) + saveButtonTextHorizontalPadding);
            int saveButtonMinimumHeight = Math.Max(40, this.Font.Height + saveButtonTextVerticalPadding);
            int baseContentWidth = Math.Max(Math.Max(388, (this.Font.Height * 18) + 64), languageLabelMinimumWidth + 200);
            int dialogMinimumWidth = Math.Max(420, (horizontalPadding * 2) + baseContentWidth);
            int baseContentHeight =
                contentTop +
                infoMinimumHeight +
                sectionSpacing +
                languageLabelMinimumHeight +
                compactSpacing +
                comboBoxMinimumHeight +
                sectionSpacing +
                saveButtonMinimumHeight +
                bottomPadding;
            int dialogMinimumHeight = Math.Max(224, baseContentHeight);
            this.ClientSize = new Size(dialogMinimumWidth, dialogMinimumHeight);
            int contentLeft = horizontalPadding;
            int contentRight = this.ClientSize.Width - horizontalPadding;
            int contentWidth = contentRight - contentLeft;

            Label infoLabel = new Label();
            infoLabel.AutoSize = false;
            infoLabel.Text = UiLanguage.Get(
                "StartupLanguage.Info",
                "Bitte wähle zuerst die Programmsprache aus. Nach dem Speichern wird das Fenster geschlossen und das Programm startet weiter.");
            Size infoTextSize = TextRenderer.MeasureText(
                infoLabel.Text,
                infoLabel.Font,
                new Size(contentWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int infoHeight = Math.Max(infoMinimumHeight, infoTextSize.Height);
            infoLabel.SetBounds(contentLeft, contentTop, contentWidth, infoHeight);

            Label languageLabel = new Label();
            languageLabel.AutoSize = false;
            languageLabel.Text = UiLanguage.Get("StartupLanguage.Label", "Sprache");
            Size languageLabelTextSize = TextRenderer.MeasureText(
                languageLabel.Text,
                languageLabel.Font,
                new Size(contentWidth, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int languageLabelTop = infoLabel.Bottom + sectionSpacing;
            int languageLabelHeight = Math.Max(languageLabelMinimumHeight, languageLabelTextSize.Height + languageLabelTextVerticalPadding);
            int languageLabelWidth = Math.Max(languageLabelMinimumWidth, languageLabelTextSize.Width + languageLabelTextHorizontalPadding);
            languageLabel.SetBounds(contentLeft, languageLabelTop, languageLabelWidth, languageLabelHeight);

            this.languageComboBox = new ComboBox();
            this.languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            int comboBoxTop = languageLabel.Bottom + compactSpacing;
            int comboBoxHeight = Math.Max(comboBoxMinimumHeight, this.languageComboBox.PreferredHeight + 6);
            this.languageComboBox.SetBounds(contentLeft, comboBoxTop, contentWidth, comboBoxHeight);

            LanguageOption[] languages = UiLanguage.GetSupportedLanguages();
            int selectedIndex = 0;
            for (int i = 0; i < languages.Length; i++)
            {
                this.languageComboBox.Items.Add(languages[i]);
                if (string.Equals(languages[i].Code, selectedLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (this.languageComboBox.Items.Count > 0)
            {
                this.languageComboBox.SelectedIndex = selectedIndex;
            }

            this.saveButton = new Button();
            this.saveButton.AutoSize = true;
            this.saveButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.saveButton.Text = UiLanguage.Get("StartupLanguage.Save", "Speichern");
            this.saveButton.Padding = new Padding(
                saveButtonHorizontalPadding,
                saveButtonVerticalPadding,
                saveButtonHorizontalPadding,
                saveButtonVerticalPadding);
            this.saveButton.MinimumSize = new Size(saveButtonMinimumWidth, saveButtonMinimumHeight);
            this.saveButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            Size saveButtonSize = this.saveButton.GetPreferredSize(Size.Empty);
            int saveButtonWidth = Math.Max(this.saveButton.MinimumSize.Width, saveButtonSize.Width);
            int saveButtonHeight = Math.Max(this.saveButton.MinimumSize.Height, saveButtonSize.Height);
            int buttonTop = this.languageComboBox.Bottom + sectionSpacing;
            this.ClientSize = new Size(
                this.ClientSize.Width,
                Math.Max(this.ClientSize.Height, buttonTop + saveButtonHeight + bottomPadding));
            this.saveButton.SetBounds(
                contentRight - saveButtonWidth,
                buttonTop,
                saveButtonWidth,
                saveButtonHeight);
            int requiredClientWidthFromControls = Math.Max(
                Math.Max(
                    languageLabel.Right + horizontalPadding,
                    this.languageComboBox.Right + horizontalPadding),
                this.saveButton.Right + horizontalPadding);
            if (requiredClientWidthFromControls > this.ClientSize.Width)
            {
                this.ClientSize = new Size(requiredClientWidthFromControls, this.ClientSize.Height);
                contentRight = this.ClientSize.Width - horizontalPadding;
                contentWidth = contentRight - contentLeft;
                infoLabel.SetBounds(contentLeft, infoLabel.Top, contentWidth, infoLabel.Height);
                this.languageComboBox.SetBounds(contentLeft, this.languageComboBox.Top, contentWidth, this.languageComboBox.Height);
                this.saveButton.SetBounds(
                    contentRight - saveButtonWidth,
                    this.saveButton.Top,
                    saveButtonWidth,
                    saveButtonHeight);
            }
            int requiredClientHeightFromVisibleControls = Math.Max(
                Math.Max(
                    infoLabel.Bottom,
                    languageLabel.Bottom),
                Math.Max(
                    this.languageComboBox.Bottom,
                    this.saveButton.Bottom)) + bottomPadding;
            if (requiredClientHeightFromVisibleControls != this.ClientSize.Height)
            {
                this.ClientSize = new Size(this.ClientSize.Width, requiredClientHeightFromVisibleControls);
            }
            this.saveButton.Click += this.SaveButton_Click;

            this.AcceptButton = this.saveButton;
            this.Controls.Add(infoLabel);
            this.Controls.Add(languageLabel);
            this.Controls.Add(this.languageComboBox);
            this.Controls.Add(this.saveButton);
        }

        public string SelectedLanguageCode
        {
            get
            {
                LanguageOption option = this.languageComboBox.SelectedItem as LanguageOption;
                return option != null ? option.Code : "de";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!this.allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            this.allowClose = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    internal sealed class TransparencyForm : Form
    {
        private readonly TrackBar transparencyTrackBar;
        private readonly Label valueLabel;
        private readonly Action<int> previewTransparency;

        public TransparencyForm(int transparencyPercent, Action<int> previewTransparency)
        {
            this.previewTransparency = previewTransparency;
            this.Text = UiLanguage.Get("Transparency.Title", "Transparenzeinstellung");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = SystemFonts.MessageBoxFont;
            this.TopMost = true;
            int compactSpacing = Math.Max(4, this.Font.Height / 3);
            int sectionSpacing = Math.Max(8, compactSpacing * 2);
            int horizontalPadding = Math.Max(16, this.Font.Height + 4);
            int buttonSpacing = Math.Max(8, compactSpacing * 2);
            int baseContentWidth = Math.Max(240, this.Font.Height * 15);
            int dialogMinimumWidth = Math.Max(320, (horizontalPadding * 2) + baseContentWidth);
            int buttonWidthReserve = Math.Max(28, compactSpacing * 7);
            int buttonHeightReserve = Math.Max(12, compactSpacing * 3);
            int buttonHorizontalPadding = Math.Max(10, compactSpacing + 4);
            int buttonVerticalPadding = Math.Max(4, (compactSpacing / 2) + 1);
            int buttonTextHorizontalPadding = Math.Max(buttonWidthReserve, (buttonHorizontalPadding * 2) + 4);
            int buttonTextVerticalPadding = Math.Max(buttonHeightReserve, (buttonVerticalPadding * 2) + 4);
            int valueLabelWidthReserve = Math.Max(12, compactSpacing * 3);
            int valueLabelHeightReserve = Math.Max(8, compactSpacing * 2);
            int trackBarHeightReserve = Math.Max(10, compactSpacing * 3);
            int minimumButtonWidth = Math.Max(78, (this.Font.Height * 4) + buttonTextHorizontalPadding);
            int minimumButtonHeight = Math.Max(28, this.Font.Height + buttonTextVerticalPadding);
            int valueLabelMinimumWidth = Math.Max(90, (this.Font.Height * 5) + valueLabelWidthReserve);
            int valueLabelMinimumHeight = Math.Max(24, this.Font.Height + valueLabelHeightReserve);
            int trackBarMinimumHeight = Math.Max(40, this.Font.Height + trackBarHeightReserve);
            int valueLabelTextHorizontalPadding = valueLabelWidthReserve;
            int infoTextVerticalPadding = Math.Max(4, compactSpacing + 1);
            int valueLabelTextVerticalPadding = Math.Max(4, compactSpacing + 1);
            int contentTop = Math.Max(sectionSpacing, this.Font.Height);
            int infoMinimumHeight = Math.Max(this.Font.Height + compactSpacing + infoTextVerticalPadding, sectionSpacing + compactSpacing + 2);
            int contentSectionSpacing = Math.Max(compactSpacing, this.Font.Height / 3);
            int buttonAreaTopSpacing = Math.Max(sectionSpacing, contentSectionSpacing + compactSpacing);
            int bottomPadding = Math.Max(12, buttonAreaTopSpacing);
            int baseContentHeight =
                contentTop +
                infoMinimumHeight +
                contentSectionSpacing +
                valueLabelMinimumHeight +
                contentSectionSpacing +
                trackBarMinimumHeight +
                buttonAreaTopSpacing +
                minimumButtonHeight +
                bottomPadding;
            int dialogMinimumHeight = Math.Max(176, baseContentHeight);
            this.ClientSize = new Size(dialogMinimumWidth, dialogMinimumHeight);
            int contentLeft = horizontalPadding;
            int contentRight = this.ClientSize.Width - horizontalPadding;
            int contentWidth = contentRight - contentLeft;

            Label infoLabel = new Label();
            infoLabel.AutoSize = false;
            infoLabel.Text = UiLanguage.Get(
                "Transparency.Info",
                "Transparenz");
            Size infoTextSize = TextRenderer.MeasureText(
                infoLabel.Text,
                infoLabel.Font,
                new Size(contentWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int infoHeight = Math.Max(infoMinimumHeight, infoTextSize.Height);
            infoLabel.SetBounds(contentLeft, contentTop, contentWidth, infoHeight);

            this.valueLabel = new Label();
            this.valueLabel.AutoSize = false;
            this.valueLabel.TextAlign = ContentAlignment.MiddleRight;
            Size valueLabelTextSize = TextRenderer.MeasureText(
                "100 %",
                this.valueLabel.Font ?? this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int valueLabelHeight = Math.Max(valueLabelMinimumHeight, valueLabelTextSize.Height + valueLabelTextVerticalPadding);
            int valueLabelWidth = Math.Max(valueLabelMinimumWidth, valueLabelTextSize.Width + valueLabelTextHorizontalPadding);
            int valueLabelTop = infoLabel.Bottom + contentSectionSpacing;
            this.valueLabel.SetBounds(contentRight - valueLabelWidth, valueLabelTop, valueLabelWidth, valueLabelHeight);

            this.transparencyTrackBar = new TrackBar();
            this.transparencyTrackBar.Minimum = 0;
            this.transparencyTrackBar.Maximum = 100;
            this.transparencyTrackBar.TickFrequency = 10;
            this.transparencyTrackBar.SmallChange = 1;
            this.transparencyTrackBar.LargeChange = 10;
            this.transparencyTrackBar.Value = Math.Max(0, Math.Min(100, transparencyPercent));
            int trackBarHeight = Math.Max(trackBarMinimumHeight, this.transparencyTrackBar.PreferredSize.Height);
            int trackBarTop = this.valueLabel.Bottom + contentSectionSpacing;
            this.transparencyTrackBar.SetBounds(contentLeft, trackBarTop, contentWidth, trackBarHeight);
            this.transparencyTrackBar.ValueChanged += this.TransparencyTrackBar_ValueChanged;

            Button saveButton = new Button();
            saveButton.Text = UiLanguage.Get("Transparency.Save", "Speichern");
            saveButton.DialogResult = DialogResult.OK;
            saveButton.Padding = new Padding(buttonHorizontalPadding, buttonVerticalPadding, buttonHorizontalPadding, buttonVerticalPadding);
            saveButton.MinimumSize = new Size(minimumButtonWidth, minimumButtonHeight);
            Size saveButtonTextSize = TextRenderer.MeasureText(
                saveButton.Text,
                saveButton.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int saveButtonWidth = Math.Max(saveButton.MinimumSize.Width, saveButtonTextSize.Width + buttonTextHorizontalPadding);
            saveButton.Click += delegate
            {
                if (this.previewTransparency != null)
                {
                    this.previewTransparency(this.transparencyTrackBar.Value);
                }
            };

            Button cancelButton = new Button();
            cancelButton.Text = UiLanguage.Get("Transparency.Close", "Schließen");
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Padding = new Padding(buttonHorizontalPadding, buttonVerticalPadding, buttonHorizontalPadding, buttonVerticalPadding);
            cancelButton.MinimumSize = new Size(minimumButtonWidth, minimumButtonHeight);
            Size cancelButtonTextSize = TextRenderer.MeasureText(
                cancelButton.Text,
                cancelButton.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int cancelButtonWidth = Math.Max(cancelButton.MinimumSize.Width, cancelButtonTextSize.Width + buttonTextHorizontalPadding);
            Size saveButtonPreferredSize = saveButton.GetPreferredSize(Size.Empty);
            Size cancelButtonPreferredSize = cancelButton.GetPreferredSize(Size.Empty);
            int buttonHeight = Math.Max(
                Math.Max(saveButton.MinimumSize.Height, cancelButton.MinimumSize.Height),
                Math.Max(saveButtonPreferredSize.Height, cancelButtonPreferredSize.Height));
            int minimumClientWidth = Math.Max(
                dialogMinimumWidth,
                contentLeft + saveButtonWidth + buttonSpacing + cancelButtonWidth + horizontalPadding);
            int minimumClientHeight = Math.Max(
                dialogMinimumHeight,
                this.transparencyTrackBar.Bottom + buttonAreaTopSpacing + buttonHeight + bottomPadding);
            this.ClientSize = new Size(minimumClientWidth, minimumClientHeight);
            contentRight = this.ClientSize.Width - horizontalPadding;
            contentWidth = contentRight - contentLeft;
            infoLabel.SetBounds(contentLeft, infoLabel.Top, contentWidth, infoLabel.Height);
            this.valueLabel.SetBounds(contentRight - valueLabelWidth, this.valueLabel.Top, valueLabelWidth, valueLabelHeight);
            this.transparencyTrackBar.SetBounds(contentLeft, this.transparencyTrackBar.Top, contentWidth, this.transparencyTrackBar.Height);
            int buttonTop = this.transparencyTrackBar.Bottom + buttonAreaTopSpacing;
            int cancelButtonLeft = contentRight - cancelButtonWidth;
            int saveButtonLeft = cancelButtonLeft - buttonSpacing - saveButtonWidth;
            saveButton.SetBounds(saveButtonLeft, buttonTop, saveButtonWidth, buttonHeight);
            cancelButton.SetBounds(cancelButtonLeft, buttonTop, cancelButtonWidth, buttonHeight);
            int requiredClientWidthFromControls = Math.Max(
                Math.Max(
                    Math.Max(
                        this.valueLabel.Right + horizontalPadding,
                        this.transparencyTrackBar.Right + horizontalPadding),
                    saveButton.Right + horizontalPadding),
                cancelButton.Right + horizontalPadding);
            if (requiredClientWidthFromControls > this.ClientSize.Width)
            {
                this.ClientSize = new Size(requiredClientWidthFromControls, this.ClientSize.Height);
                contentRight = this.ClientSize.Width - horizontalPadding;
                contentWidth = contentRight - contentLeft;
                infoLabel.SetBounds(contentLeft, infoLabel.Top, contentWidth, infoLabel.Height);
                this.valueLabel.SetBounds(contentRight - valueLabelWidth, this.valueLabel.Top, valueLabelWidth, valueLabelHeight);
                this.transparencyTrackBar.SetBounds(contentLeft, this.transparencyTrackBar.Top, contentWidth, this.transparencyTrackBar.Height);
                cancelButtonLeft = contentRight - cancelButtonWidth;
                saveButtonLeft = cancelButtonLeft - buttonSpacing - saveButtonWidth;
                saveButton.SetBounds(saveButtonLeft, buttonTop, saveButtonWidth, buttonHeight);
                cancelButton.SetBounds(cancelButtonLeft, buttonTop, cancelButtonWidth, buttonHeight);
            }
            int requiredClientHeightFromVisibleControls = Math.Max(
                Math.Max(
                    infoLabel.Bottom,
                    this.valueLabel.Bottom),
                Math.Max(
                    this.transparencyTrackBar.Bottom,
                    cancelButton.Bottom)) + bottomPadding;
            if (requiredClientHeightFromVisibleControls != this.ClientSize.Height)
            {
                this.ClientSize = new Size(this.ClientSize.Width, requiredClientHeightFromVisibleControls);
            }

            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;
            this.Controls.Add(infoLabel);
            this.Controls.Add(this.valueLabel);
            this.Controls.Add(this.transparencyTrackBar);
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);
            this.UpdateValueLabel();
            if (this.previewTransparency != null)
            {
                this.previewTransparency(this.transparencyTrackBar.Value);
            }
        }

        public int SelectedTransparencyPercent
        {
            get
            {
                return this.transparencyTrackBar.Value;
            }
        }

        private void TransparencyTrackBar_ValueChanged(object sender, EventArgs e)
        {
            this.UpdateValueLabel();
            if (this.previewTransparency != null)
            {
                this.previewTransparency(this.transparencyTrackBar.Value);
            }
        }

        private void UpdateValueLabel()
        {
            this.valueLabel.Text = UiLanguage.Format(
                "Transparency.ValueFormat",
                "{0} %",
                this.transparencyTrackBar.Value);
        }
    }
}
