
namespace CameraObjectDetection
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            

            btnStartCamera = new Button();
            btnStopCamera = new Button();
            btnFullScreen = new Button();
            cameraView = new LibVLCSharp.WinForms.VideoView();
            pictureBoxDetection = new PictureBox();

            headerPanel = new Panel();
            lblBrand = new Label();
            lblSubtitle = new Label();
            lblLiveStatus = new Label();

            lblCameraTitle = new Label();
            lblDetectionTitle = new Label();
            lblCameraHint = new Label();
            lblDetectionHint = new Label();

            lblPersonCount = new Label();
            lblPersonCount.AutoSize = false;
            lblPersonCount.BackColor = Color.FromArgb(28, 43, 67);
            lblPersonCount.ForeColor = Color.FromArgb(74, 222, 128);
            lblPersonCount.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblPersonCount.Location = new Point(1100, 115);
            lblPersonCount.Size = new Size(260, 38);
            lblPersonCount.Text = "HUMANS DETECTED: 0";
            lblPersonCount.TextAlign = ContentAlignment.MiddleCenter;
            lblPersonCount.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            footerPanel = new Panel();
            lblFooter = new Label();

            ((System.ComponentModel.ISupportInitialize)cameraView).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxDetection).BeginInit();

            headerPanel.SuspendLayout();
            footerPanel.SuspendLayout();
            SuspendLayout();

            // Main Form
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(13, 20, 34);
            ClientSize = new Size(1400, 790);
            MinimumSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "VisionGuard | Human Detection";
            Font = new Font("Segoe UI", 10F);
            ForeColor = Color.White;

            // Header
            headerPanel.BackColor = Color.FromArgb(20, 31, 51);
            headerPanel.Location = new Point(0, 0);
            headerPanel.Size = new Size(1400, 100);
            headerPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            lblBrand.AutoSize = true;
            lblBrand.Font = new Font("Segoe UI", 23F, FontStyle.Bold);
            lblBrand.ForeColor = Color.White;
            lblBrand.Location = new Point(28, 12);
            lblBrand.Text = "VISIONGUARD";

            lblSubtitle.AutoSize = true;
            lblSubtitle.Font = new Font("Segoe UI", 10F);
            lblSubtitle.ForeColor = Color.FromArgb(160, 178, 204);
            lblSubtitle.Location = new Point(32, 58);
            lblSubtitle.Text = "AI-POWERED HUMAN MONITORING";

            lblLiveStatus.AutoSize = false;
            lblLiveStatus.BackColor = Color.FromArgb(35, 53, 73);
            lblLiveStatus.ForeColor = Color.FromArgb(255, 194, 85);
            lblLiveStatus.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblLiveStatus.Location = new Point(1120, 32);
            lblLiveStatus.Size = new Size(235, 36);
            lblLiveStatus.Text = "●  CAMERA DISCONNECTED";
            lblLiveStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblLiveStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            headerPanel.Controls.Add(lblBrand);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(lblLiveStatus);

            // Camera section title
            lblCameraTitle.AutoSize = true;
            lblCameraTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblCameraTitle.ForeColor = Color.White;
            lblCameraTitle.Location = new Point(30, 120);
            lblCameraTitle.Text = "01  |  LIVE CAMERA";

            lblCameraHint.AutoSize = true;
            lblCameraHint.Font = new Font("Segoe UI", 9F);
            lblCameraHint.ForeColor = Color.FromArgb(145, 163, 190);
            lblCameraHint.Location = new Point(31, 148);
            lblCameraHint.Text = "Original camera stream";

            // Detection section title
            lblDetectionTitle.AutoSize = true;
            lblDetectionTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblDetectionTitle.ForeColor = Color.White;
            lblDetectionTitle.Location = new Point(710, 120);
            lblDetectionTitle.Text = "02  |  HUMAN DETECTION";

            lblDetectionHint.AutoSize = true;
            lblDetectionHint.Font = new Font("Segoe UI", 9F);
            lblDetectionHint.ForeColor = Color.FromArgb(145, 163, 190);
            lblDetectionHint.Location = new Point(711, 148);
            lblDetectionHint.Text = "AI inference with person-only bounding boxes";

            // Original VLC camera view
            cameraView.BackColor = Color.Black;
            cameraView.Location = new Point(30, 180);
            cameraView.Name = "cameraView";
            cameraView.Size = new Size(650, 440);
            cameraView.MediaPlayer = null;
            cameraView.TabIndex = 2;
            cameraView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;

            // Detection output
            pictureBoxDetection.BackColor = Color.Black;
            pictureBoxDetection.Location = new Point(710, 180);
            pictureBoxDetection.Name = "pictureBoxDetection";
            pictureBoxDetection.Size = new Size(650, 440);
            pictureBoxDetection.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxDetection.TabIndex = 3;
            pictureBoxDetection.TabStop = false;
            pictureBoxDetection.Anchor =
                AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;

            // Connect button - keep original control name for existing event hookup
            btnStartCamera.BackColor = Color.FromArgb(37, 99, 235);
            btnStartCamera.FlatStyle = FlatStyle.Flat;
            btnStartCamera.FlatAppearance.BorderSize = 0;
            btnStartCamera.FlatAppearance.MouseOverBackColor = Color.FromArgb(59, 130, 246);
            btnStartCamera.ForeColor = Color.White;
            btnStartCamera.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnStartCamera.Location = new Point(480, 665);
            btnStartCamera.Name = "btnStartCamera";
            btnStartCamera.Size = new Size(190, 48);
            btnStartCamera.TabIndex = 0;
            btnStartCamera.Text = "▶  Connect";
            btnStartCamera.UseVisualStyleBackColor = false;
            btnStartCamera.Cursor = Cursors.Hand;
            btnStartCamera.FlatAppearance.BorderColor = Color.FromArgb(37, 99, 235);

            // Stop button - keep original control name for existing event hookup
            btnStopCamera.BackColor = Color.FromArgb(153, 48, 60);
            btnStopCamera.FlatStyle = FlatStyle.Flat;
            btnStopCamera.FlatAppearance.BorderSize = 0;
            btnStopCamera.FlatAppearance.MouseOverBackColor = Color.FromArgb(190, 55, 68);
            btnStopCamera.ForeColor = Color.White;
            btnStopCamera.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnStopCamera.Location = new Point(700, 665);
            btnStopCamera.Name = "btnStopCamera";
            btnStopCamera.Size = new Size(190, 48);
            btnStopCamera.TabIndex = 1;
            btnStopCamera.Text = "■  Stop";
            btnStopCamera.UseVisualStyleBackColor = false;
            btnStopCamera.Cursor = Cursors.Hand;
            btnStopCamera.FlatAppearance.BorderColor = Color.FromArgb(153, 48, 60);


            btnFullScreen.BackColor = Color.FromArgb(37, 99, 235);
            btnFullScreen.FlatStyle = FlatStyle.Flat;
            btnFullScreen.FlatAppearance.BorderSize = 0;
            btnFullScreen.ForeColor = Color.White;
            btnFullScreen.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnFullScreen.Location = new Point(920, 665);
            btnFullScreen.Name = "btnFullScreen";
            btnFullScreen.Size = new Size(190, 48);
            btnFullScreen.Text = "⛶ Full Screen";
            btnFullScreen.UseVisualStyleBackColor = false;
            btnFullScreen.Cursor = Cursors.Hand;


            // Footer
            footerPanel.BackColor = Color.FromArgb(20, 31, 51);
            footerPanel.Location = new Point(0, 745);
            footerPanel.Size = new Size(1400, 45);
            footerPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            lblFooter.AutoSize = true;
            lblFooter.Font = new Font("Segoe UI", 9F);
            lblFooter.ForeColor = Color.FromArgb(145, 163, 190);
            lblFooter.Location = new Point(28, 12);
            lblFooter.Text = "VISIONGUARD  •  HUMAN DETECTION SYSTEM";

            footerPanel.Controls.Add(lblFooter);

            // Add controls
            Controls.Add(headerPanel);
            Controls.Add(lblCameraTitle);
            Controls.Add(lblCameraHint);
            Controls.Add(lblDetectionTitle);
            Controls.Add(lblDetectionHint);
            Controls.Add(cameraView);
            Controls.Add(pictureBoxDetection);
            Controls.Add(btnStartCamera);
            Controls.Add(btnStopCamera);
            Controls.Add(btnFullScreen);
            Controls.Add(footerPanel);
            Controls.Add(lblPersonCount);

            ((System.ComponentModel.ISupportInitialize)cameraView).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxDetection).EndInit();

            headerPanel.ResumeLayout(false);
            headerPanel.PerformLayout();
            footerPanel.ResumeLayout(false);
            footerPanel.PerformLayout();

            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnStartCamera;
        private Button btnStopCamera;
        private Button btnFullScreen;
        private LibVLCSharp.WinForms.VideoView cameraView;
        private PictureBox pictureBoxDetection;

        private Panel headerPanel;
        private Label lblBrand;
        private Label lblSubtitle;
        private Label lblLiveStatus;

        private Label lblCameraTitle;
        private Label lblDetectionTitle;
        private Label lblCameraHint;
        private Label lblDetectionHint;

        private Panel footerPanel;
        private Label lblPersonCount;
        private Label lblFooter;
    }
}
