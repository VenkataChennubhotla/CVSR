using IdentityModel.Client;
using System.IdentityModel.Tokens.Jwt;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GetRestApiToken
{
    public partial class Form1 : Form
    {
        private HttpClient _client;

        private const string _fallbackRestUrl = "http://localhost:8080/api/StartSession";
        private const string _fallbackOnCallUrl = "http://OncallWebsite.com/OnCall/";

        public Form1()
        {
            _client = new HttpClient();
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.copyToolTip.SetToolTip(this.copyRestToken, "Copy to clipboard");

            if (!File.Exists(@"C:\ProgramData\Intergraph\Cad\cad-global.json.config"))
            {
                cb_Configs.Enabled = false;
                cb_Configs.Text = "cad-global.json.config Not Found";
                tb_restUrl.Text = _fallbackRestUrl;
                tb_OnCallurl.Text = _fallbackOnCallUrl;
            }
            else
            {
                (var configs, int index) = GetDefaultConfigValue();
                cb_Configs.Items.AddRange(configs.ToArray());
                cb_Configs.SelectedIndex = index;
            }
        }

        private (List<ConfigValues>, int) GetDefaultConfigValue()
        {
            var configText = File.ReadAllText(@"C:\ProgramData\Intergraph\Cad\cad-global.json.config");
            
            var configData = (JObject)JsonConvert.DeserializeObject<JObject>(configText).First.First;
            var defaultConfig = configData.Children<JProperty>().FirstOrDefault(d => d.Name.StartsWith("default")).Value.Value<string>();
            List<ConfigValues> values = new List<ConfigValues>();
            int defaultIndex = 0;

            foreach (var child in configData.Properties())
            {
                if (child.Name.Substring(0,7) == "default")
                {
                    continue;
                }

                var name = child.Name;
                var url = child.First["ONCALL_BASE_URL"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                //strip oncall/ it will get added later
                if (url.EndsWith("oncall/"))
                {
                    url = url.Replace("oncall/", "");
                }
                if (url.EndsWith("/oncall"))
                {
                    url = url.Replace("/oncall", "/");
                }

                //strip the trailing /
                if (url.EndsWith("/"))
                {
                    url = url.TrimEnd('/');
                }

                //if we are using CIAB we need to change to the oidc port
                url = url.Replace("44319", "44318");

                var temp = new ConfigValues(name, url);
                values.Add(temp);

                if (name == defaultConfig)
                {
                    defaultIndex = values.Count - 1;
                }
            }

            return (values,defaultIndex);
        }


        private void cb_Configs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (((ComboBox)sender).Items.Count <= 0)
            {
                return;
            }
            var data = ((ComboBox)sender).SelectedItem as ConfigValues;
            tb_OnCallurl.Text = data.OnCallUrl.ToString();
            tb_restUrl.Text = data.RestApiUrl.ToString();
        }

        private async void btn_login_clicked(object sender, EventArgs e)
        {
            tb_restToken.Text = string.Empty;
            tb_accessToken.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(tb_username.Text))
            {
                tb_username.BackColor = Color.Red;
                return;
            }

            if (string.IsNullOrWhiteSpace(tb_password.Text))
            {
                tb_password.BackColor = Color.Red;
                return;
            }

            tb_username.BackColor = Color.White;
            tb_password.BackColor = Color.White;

            try
            {
                var disco = await _client.GetDiscoveryDocumentAsync(tb_OnCallurl.Text + "oncall/identity");

                if (disco.IsError)
                {
                    //MessageBox.Show(disco.Error);
                    disco = await _client.GetDiscoveryDocumentAsync(tb_OnCallurl.Text + "oncall.identity");

                    if (disco.IsError)
                    {
                        MessageBox.Show(disco.Error);
                        return;
                    }
                }

                var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    ClientId = tb_username.Text, // define your own Client ID
                    ClientSecret = tb_password.Text, // use the secret configured for your application (not hard coded)
                    Scope = "api" // this scope is defined as the scope used for accessing the OnCall Web API
                });

                if (tokenResponse.IsError)
                {
                    MessageBox.Show($"Error while contacting identity provider \n {tokenResponse.Error}");
                    return;
                }

                tb_accessToken.Text = tokenResponse.AccessToken;

                var restapiResponse = await _client.PostAsync(tb_restUrl.Text + $"?Token={tokenResponse.AccessToken}&language={tb_language.Text}", null);

                if (!restapiResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Error talking to RestAPI \n {restapiResponse.StatusCode} - {restapiResponse.ReasonPhrase}");
                    return;
                }

                var restcontent = await restapiResponse.Content.ReadAsStringAsync();
                var content = JsonConvert.DeserializeObject<JObject>(restcontent);
                tb_restToken.Text = content["accessToken"].Value<string>();

                //pull appart the restAPI token and display the expiration time.
                //Other fields here might be useful like issuer.
                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.ReadJwtToken(tb_restToken.Text);
                lb_expireDate.Text = token.ValidTo.ToString();
                lb_issuerVal.Text = token.Issuer;
                lb_sessionIdVal.Text = token.Claims.FirstOrDefault(c => c.Type == "sessionId")?.Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void tb_password_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((Keys)e.KeyChar == Keys.Enter)
                btn_login.PerformClick();
        }

        private void tb_username_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((Keys)e.KeyChar == Keys.Enter)
                btn_login.PerformClick();
        }

        private void copyRestToken_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(tb_restToken.Text);
        }
    }

    public class ConfigValues
    {
        public string Name { get; }
        public Uri OnCallUrl { get; }
        public Uri RestApiUrl { get; }

        public ConfigValues(string name, string oncallUrl)
        {
            Name = name;
            var test = new Uri(oncallUrl);

            OnCallUrl = test;
            RestApiUrl = new Uri("https://" + test.Host + ":8080/api/StartSession");
            //RestApiUrl = oncallUrl.Replace("./Oncall", "/") + "/api/StartSession";
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
