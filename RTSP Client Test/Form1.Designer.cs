namespace RTSP_Client_Test
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
			this.URLTextBox = new System.Windows.Forms.TextBox();
			this.PlayButton = new System.Windows.Forms.Button();
			this.RTSPMessagesTextBox = new System.Windows.Forms.TextBox();
			this.panel1 = new System.Windows.Forms.Panel();
			this.label2 = new System.Windows.Forms.Label();
			this.panel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// URLTextBox
			// 
			this.URLTextBox.Location = new System.Drawing.Point(94, 12);
			this.URLTextBox.Name = "URLTextBox";
			this.URLTextBox.Size = new System.Drawing.Size(169, 20);
			this.URLTextBox.TabIndex = 0;
			this.URLTextBox.Text = "rtsp://10.0.0.97/h264";
			// 
			// PlayButton
			// 
			this.PlayButton.Location = new System.Drawing.Point(312, 10);
			this.PlayButton.Name = "PlayButton";
			this.PlayButton.Size = new System.Drawing.Size(75, 23);
			this.PlayButton.TabIndex = 1;
			this.PlayButton.Text = "Play";
			this.PlayButton.UseVisualStyleBackColor = true;
			this.PlayButton.Click += new System.EventHandler(this.PlayButton_Click);
			// 
			// RTSPMessagesTextBox
			// 
			this.RTSPMessagesTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.RTSPMessagesTextBox.Location = new System.Drawing.Point(0, 44);
			this.RTSPMessagesTextBox.Multiline = true;
			this.RTSPMessagesTextBox.Name = "RTSPMessagesTextBox";
			this.RTSPMessagesTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.RTSPMessagesTextBox.Size = new System.Drawing.Size(761, 314);
			this.RTSPMessagesTextBox.TabIndex = 10;
			// 
			// panel1
			// 
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.URLTextBox);
			this.panel1.Controls.Add(this.PlayButton);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
			this.panel1.Location = new System.Drawing.Point(0, 0);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(761, 44);
			this.panel1.TabIndex = 11;
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(472, 12);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(35, 13);
			this.label2.TabIndex = 2;
			this.label2.Text = "label1";
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(761, 358);
			this.Controls.Add(this.RTSPMessagesTextBox);
			this.Controls.Add(this.panel1);
			this.Name = "Form1";
			this.Text = "Form1";
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox URLTextBox;
		private System.Windows.Forms.Button PlayButton;
		private System.Windows.Forms.TextBox RTSPMessagesTextBox;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Label label2;
	}
}

