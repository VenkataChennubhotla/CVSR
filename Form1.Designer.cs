namespace GetRestApiToken
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
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.cb_Configs = new System.Windows.Forms.ComboBox();
            this.tb_username = new System.Windows.Forms.TextBox();
            this.tb_password = new System.Windows.Forms.TextBox();
            this.tb_restUrl = new System.Windows.Forms.TextBox();
            this.tb_accessToken = new System.Windows.Forms.TextBox();
            this.tb_restToken = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.btn_login = new System.Windows.Forms.Button();
            this.tb_OnCallurl = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.copyRestToken = new System.Windows.Forms.Button();
            this.copyToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tb_language = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.lb_sessionIdVal = new System.Windows.Forms.Label();
            this.lb_sessionId = new System.Windows.Forms.Label();
            this.lb_issuerVal = new System.Windows.Forms.Label();
            this.lb_issuer = new System.Windows.Forms.Label();
            this.lb_expireDate = new System.Windows.Forms.Label();
            this.lb_expiresAt = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 42);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "OnCall Urls";
            // 
            // cb_Configs
            // 
            this.cb_Configs.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cb_Configs.FormattingEnabled = true;
            this.cb_Configs.Location = new System.Drawing.Point(82, 12);
            this.cb_Configs.Name = "cb_Configs";
            this.cb_Configs.Size = new System.Drawing.Size(458, 21);
            this.cb_Configs.TabIndex = 5;
            this.cb_Configs.SelectedIndexChanged += new System.EventHandler(this.cb_Configs_SelectedIndexChanged);
            // 
            // tb_username
            // 
            this.tb_username.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tb_username.Location = new System.Drawing.Point(70, 19);
            this.tb_username.Name = "tb_username";
            this.tb_username.Size = new System.Drawing.Size(117, 20);
            this.tb_username.TabIndex = 0;
            this.tb_username.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tb_username_KeyPress);
            // 
            // tb_password
            // 
            this.tb_password.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tb_password.Location = new System.Drawing.Point(70, 49);
            this.tb_password.Name = "tb_password";
            this.tb_password.Size = new System.Drawing.Size(117, 20);
            this.tb_password.TabIndex = 1;
            this.tb_password.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tb_password_KeyPress);
            // 
            // tb_restUrl
            // 
            this.tb_restUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tb_restUrl.Location = new System.Drawing.Point(88, 66);
            this.tb_restUrl.Name = "tb_restUrl";
            this.tb_restUrl.Size = new System.Drawing.Size(452, 20);
            this.tb_restUrl.TabIndex = 7;
            // 
            // tb_accessToken
            // 
            this.tb_accessToken.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.tb_accessToken.Location = new System.Drawing.Point(12, 248);
            this.tb_accessToken.Name = "tb_accessToken";
            this.tb_accessToken.ReadOnly = true;
            this.tb_accessToken.Size = new System.Drawing.Size(528, 20);
            this.tb_accessToken.TabIndex = 8;
            // 
            // tb_restToken
            // 
            this.tb_restToken.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.tb_restToken.Location = new System.Drawing.Point(12, 291);
            this.tb_restToken.Name = "tb_restToken";
            this.tb_restToken.ReadOnly = true;
            this.tb_restToken.Size = new System.Drawing.Size(476, 20);
            this.tb_restToken.TabIndex = 9;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 69);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(73, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "RestApi Login";
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(22, 26);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(42, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "ClientId";
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(26, 52);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "Secret";
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(13, 232);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(64, 13);
            this.label5.TabIndex = 12;
            this.label5.Text = "OIDCToken";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(13, 275);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(80, 13);
            this.label6.TabIndex = 13;
            this.label6.Text = "RestAPI Token";
            // 
            // btn_login
            // 
            this.btn_login.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.btn_login.Location = new System.Drawing.Point(193, 39);
            this.btn_login.Name = "btn_login";
            this.btn_login.Size = new System.Drawing.Size(75, 39);
            this.btn_login.TabIndex = 3;
            this.btn_login.Text = "Login";
            this.btn_login.UseVisualStyleBackColor = true;
            this.btn_login.Click += new System.EventHandler(this.btn_login_clicked);
            // 
            // tb_OnCallurl
            // 
            this.tb_OnCallurl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tb_OnCallurl.Location = new System.Drawing.Point(74, 39);
            this.tb_OnCallurl.Name = "tb_OnCallurl";
            this.tb_OnCallurl.Size = new System.Drawing.Size(466, 20);
            this.tb_OnCallurl.TabIndex = 6;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(7, 15);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(69, 13);
            this.label7.TabIndex = 16;
            this.label7.Text = "Configuration";
            // 
            // copyRestToken
            // 
            this.copyRestToken.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.copyRestToken.Location = new System.Drawing.Point(494, 289);
            this.copyRestToken.Name = "copyRestToken";
            this.copyRestToken.Size = new System.Drawing.Size(46, 22);
            this.copyRestToken.TabIndex = 17;
            this.copyRestToken.TabStop = false;
            this.copyRestToken.Text = "Copy";
            this.copyRestToken.UseVisualStyleBackColor = true;
            this.copyRestToken.Click += new System.EventHandler(this.copyRestToken_Click);
            // 
            // tb_language
            // 
            this.tb_language.Location = new System.Drawing.Point(70, 76);
            this.tb_language.Name = "tb_language";
            this.tb_language.Size = new System.Drawing.Size(117, 20);
            this.tb_language.TabIndex = 21;
            this.tb_language.Text = "en-us";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(9, 79);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(55, 13);
            this.label8.TabIndex = 20;
            this.label8.Text = "Language";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.tb_password);
            this.groupBox1.Controls.Add(this.tb_language);
            this.groupBox1.Controls.Add(this.tb_username);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.btn_login);
            this.groupBox1.Location = new System.Drawing.Point(16, 100);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(279, 121);
            this.groupBox1.TabIndex = 22;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Login Information";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lb_sessionIdVal);
            this.groupBox2.Controls.Add(this.lb_sessionId);
            this.groupBox2.Controls.Add(this.lb_issuerVal);
            this.groupBox2.Controls.Add(this.lb_issuer);
            this.groupBox2.Controls.Add(this.lb_expireDate);
            this.groupBox2.Controls.Add(this.lb_expiresAt);
            this.groupBox2.Location = new System.Drawing.Point(301, 100);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(239, 121);
            this.groupBox2.TabIndex = 23;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Token Information";
            // 
            // lb_sessionIdVal
            // 
            this.lb_sessionIdVal.AutoSize = true;
            this.lb_sessionIdVal.Location = new System.Drawing.Point(14, 96);
            this.lb_sessionIdVal.Name = "lb_sessionIdVal";
            this.lb_sessionIdVal.Size = new System.Drawing.Size(0, 13);
            this.lb_sessionIdVal.TabIndex = 5;
            // 
            // lb_sessionId
            // 
            this.lb_sessionId.AutoSize = true;
            this.lb_sessionId.Location = new System.Drawing.Point(6, 83);
            this.lb_sessionId.Name = "lb_sessionId";
            this.lb_sessionId.Size = new System.Drawing.Size(56, 13);
            this.lb_sessionId.TabIndex = 4;
            this.lb_sessionId.Text = "SessionId:";
            // 
            // lb_issuerVal
            // 
            this.lb_issuerVal.AutoSize = true;
            this.lb_issuerVal.Location = new System.Drawing.Point(14, 65);
            this.lb_issuerVal.Name = "lb_issuerVal";
            this.lb_issuerVal.Size = new System.Drawing.Size(0, 13);
            this.lb_issuerVal.TabIndex = 3;
            // 
            // lb_issuer
            // 
            this.lb_issuer.AutoSize = true;
            this.lb_issuer.Location = new System.Drawing.Point(6, 52);
            this.lb_issuer.Name = "lb_issuer";
            this.lb_issuer.Size = new System.Drawing.Size(38, 13);
            this.lb_issuer.TabIndex = 2;
            this.lb_issuer.Text = "Issuer:";
            // 
            // lb_expireDate
            // 
            this.lb_expireDate.AutoSize = true;
            this.lb_expireDate.Location = new System.Drawing.Point(14, 32);
            this.lb_expireDate.Name = "lb_expireDate";
            this.lb_expireDate.Size = new System.Drawing.Size(0, 13);
            this.lb_expireDate.TabIndex = 1;
            // 
            // lb_expiresAt
            // 
            this.lb_expiresAt.AutoSize = true;
            this.lb_expiresAt.Location = new System.Drawing.Point(6, 19);
            this.lb_expiresAt.Name = "lb_expiresAt";
            this.lb_expiresAt.Size = new System.Drawing.Size(61, 13);
            this.lb_expiresAt.TabIndex = 0;
            this.lb_expiresAt.Text = "Expires On:";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(552, 334);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.copyRestToken);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.tb_OnCallurl);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.tb_restToken);
            this.Controls.Add(this.tb_accessToken);
            this.Controls.Add(this.tb_restUrl);
            this.Controls.Add(this.cb_Configs);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(550, 300);
            this.Name = "Form1";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "RestAPI Login";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cb_Configs;
        private System.Windows.Forms.TextBox tb_username;
        private System.Windows.Forms.TextBox tb_password;
        private System.Windows.Forms.TextBox tb_restUrl;
        private System.Windows.Forms.TextBox tb_accessToken;
        private System.Windows.Forms.TextBox tb_restToken;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button btn_login;
        private System.Windows.Forms.TextBox tb_OnCallurl;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button copyRestToken;
        private System.Windows.Forms.ToolTip copyToolTip;
        private System.Windows.Forms.TextBox tb_language;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label lb_expireDate;
        private System.Windows.Forms.Label lb_expiresAt;
        private System.Windows.Forms.Label lb_issuer;
        private System.Windows.Forms.Label lb_issuerVal;
        private System.Windows.Forms.Label lb_sessionId;
        private System.Windows.Forms.Label lb_sessionIdVal;
    }
}

