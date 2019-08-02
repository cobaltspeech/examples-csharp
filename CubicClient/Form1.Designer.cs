namespace CubicClient
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textBoxURL = new System.Windows.Forms.TextBox();
            this.checkBoxSecureConnection = new System.Windows.Forms.CheckBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.textBoxResults = new System.Windows.Forms.TextBox();
            this.labelVersion = new System.Windows.Forms.Label();
            this.labelLegal = new System.Windows.Forms.Label();
            this.labelServerURL = new System.Windows.Forms.Label();
            this.buttonTranscribe = new System.Windows.Forms.Button();
            this.buttonConnect = new System.Windows.Forms.Button();
            this.textBoxFile = new System.Windows.Forms.TextBox();
            this.buttonChangeFile = new System.Windows.Forms.Button();
            this.modelPicker = new System.Windows.Forms.ComboBox();
            this.labelModels = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // textBoxURL
            // 
            this.textBoxURL.Location = new System.Drawing.Point(93, 12);
            this.textBoxURL.Name = "textBoxURL";
            this.textBoxURL.Size = new System.Drawing.Size(305, 20);
            this.textBoxURL.TabIndex = 4;
            // 
            // checkBoxSecureConnection
            // 
            this.checkBoxSecureConnection.AutoSize = true;
            this.checkBoxSecureConnection.Location = new System.Drawing.Point(404, 14);
            this.checkBoxSecureConnection.Name = "checkBoxSecureConnection";
            this.checkBoxSecureConnection.Size = new System.Drawing.Size(117, 17);
            this.checkBoxSecureConnection.TabIndex = 5;
            this.checkBoxSecureConnection.Text = "Secure Connection";
            this.checkBoxSecureConnection.UseVisualStyleBackColor = true;
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // textBoxResults
            // 
            this.textBoxResults.Location = new System.Drawing.Point(12, 90);
            this.textBoxResults.Multiline = true;
            this.textBoxResults.Name = "textBoxResults";
            this.textBoxResults.Size = new System.Drawing.Size(509, 177);
            this.textBoxResults.TabIndex = 6;
            // 
            // labelVersion
            // 
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(9, 270);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(48, 13);
            this.labelVersion.TabIndex = 7;
            this.labelVersion.Text = "Version: ";
            // 
            // labelLegal
            // 
            this.labelLegal.AutoSize = true;
            this.labelLegal.Location = new System.Drawing.Point(443, 270);
            this.labelLegal.Name = "labelLegal";
            this.labelLegal.Size = new System.Drawing.Size(78, 13);
            this.labelLegal.TabIndex = 8;
            this.labelLegal.Text = "Copyright 2019";
            // 
            // labelServerURL
            // 
            this.labelServerURL.AutoSize = true;
            this.labelServerURL.Location = new System.Drawing.Point(21, 15);
            this.labelServerURL.Name = "labelServerURL";
            this.labelServerURL.Size = new System.Drawing.Size(66, 13);
            this.labelServerURL.TabIndex = 9;
            this.labelServerURL.Text = "Server URL:";
            this.labelServerURL.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // buttonTranscribe
            // 
            this.buttonTranscribe.Location = new System.Drawing.Point(446, 61);
            this.buttonTranscribe.Name = "buttonTranscribe";
            this.buttonTranscribe.Size = new System.Drawing.Size(75, 23);
            this.buttonTranscribe.TabIndex = 10;
            this.buttonTranscribe.Text = "Transcribe";
            this.buttonTranscribe.UseVisualStyleBackColor = true;
            this.buttonTranscribe.Click += new System.EventHandler(this.ButtonTranscribe_Click);
            // 
            // buttonConnect
            // 
            this.buttonConnect.Location = new System.Drawing.Point(446, 34);
            this.buttonConnect.Name = "buttonConnect";
            this.buttonConnect.Size = new System.Drawing.Size(75, 23);
            this.buttonConnect.TabIndex = 11;
            this.buttonConnect.Text = "Connect";
            this.buttonConnect.UseVisualStyleBackColor = true;
            this.buttonConnect.Click += new System.EventHandler(this.ButtonConnect_Click);
            // 
            // textBoxFile
            // 
            this.textBoxFile.Enabled = false;
            this.textBoxFile.Location = new System.Drawing.Point(93, 63);
            this.textBoxFile.Name = "textBoxFile";
            this.textBoxFile.Size = new System.Drawing.Size(347, 20);
            this.textBoxFile.TabIndex = 13;
            // 
            // buttonChangeFile
            // 
            this.buttonChangeFile.Location = new System.Drawing.Point(12, 61);
            this.buttonChangeFile.Name = "buttonChangeFile";
            this.buttonChangeFile.Size = new System.Drawing.Size(75, 23);
            this.buttonChangeFile.TabIndex = 14;
            this.buttonChangeFile.Text = "Change File";
            this.buttonChangeFile.UseVisualStyleBackColor = true;
            this.buttonChangeFile.Click += new System.EventHandler(this.ButtonChangeFile_Click);
            // 
            // comboBox1
            // 
            this.modelPicker.FormattingEnabled = true;
            this.modelPicker.Location = new System.Drawing.Point(93, 36);
            this.modelPicker.Name = "comboBoxModelPicker";
            this.modelPicker.Size = new System.Drawing.Size(347, 21);
            this.modelPicker.TabIndex = 15;
            // 
            // labelModels
            // 
            this.labelModels.AutoSize = true;
            this.labelModels.Location = new System.Drawing.Point(43, 39);
            this.labelModels.Name = "labelModels";
            this.labelModels.Size = new System.Drawing.Size(44, 13);
            this.labelModels.TabIndex = 16;
            this.labelModels.Text = "Models:";
            this.labelModels.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(533, 292);
            this.Controls.Add(this.labelModels);
            this.Controls.Add(this.modelPicker);
            this.Controls.Add(this.buttonChangeFile);
            this.Controls.Add(this.textBoxFile);
            this.Controls.Add(this.buttonConnect);
            this.Controls.Add(this.buttonTranscribe);
            this.Controls.Add(this.labelServerURL);
            this.Controls.Add(this.labelLegal);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.textBoxResults);
            this.Controls.Add(this.checkBoxSecureConnection);
            this.Controls.Add(this.textBoxURL);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox textBoxURL;
        private System.Windows.Forms.CheckBox checkBoxSecureConnection;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.TextBox textBoxResults;
        private System.Windows.Forms.Label labelVersion;
        private System.Windows.Forms.Label labelLegal;
        private System.Windows.Forms.Label labelServerURL;
        private System.Windows.Forms.Button buttonTranscribe;
        private System.Windows.Forms.Button buttonConnect;
        private System.Windows.Forms.TextBox textBoxFile;
        private System.Windows.Forms.Button buttonChangeFile;
        private System.Windows.Forms.ComboBox modelPicker;
        private System.Windows.Forms.Label labelModels;
    }
}

