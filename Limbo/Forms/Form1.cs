using FastColoredTextBoxNS;
using Keno;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.WinForms;
using Newtonsoft.Json;
using RestSharp;
using SharpLua;
using SuperSocket.ClientEngine;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocket4Net;

namespace Limbo
{
    public partial class Form1 : Form
    {

        CookieContainer cc = new CookieContainer();
        private WebSocket chat_socket { get; set; }
        private FastColoredTextBox richTextBox1;

        LuaInterface lua = LuaRuntime.GetLua();
        delegate void LogConsole(string text);
        delegate void dtip(string username, decimal amount);
        delegate void dvault(decimal sentamount);
        delegate void dStop();
        delegate void dResetSeed();
        delegate void dResetStat();
        delegate void dSeedEmpty();
        delegate void dVaultEmpty(decimal sentamount);
        delegate void dTipEmpty(string username, decimal amount);

        public List<TabControl> listLua = new List<TabControl>();
        public List<ListView> listLogs = new List<ListView>();
        public List<Panel> listGraph = new List<Panel>();

        List<double> xList = new List<double>();
        List<double> yList = new List<double>();

        CartesianChart ch = new CartesianChart();
        ChartValues<ObservablePoint> data = new ChartValues<ObservablePoint>();

        public string StakeSite = "stake.com";
        public string token = "";

        public string clientSeed = "";
        public string serverSeed = "";
        public int nonce = 1;
        public decimal balanceSim = 0;
        public int stopNonce = 0;

        public bool running = false;
        public bool ready = true;
        public bool sim = false;
        public int counter = 0;

        public string currencySelected = "btc";
        public double target = 0;
        public decimal BaseBet = 0;
        public decimal amount = 0;
        public decimal currentBal = 0;
        public decimal currentProfit = 0;
        public decimal currentWager = 0;
        public bool isWin = false;
        public int wins = 0;
        public int losses = 0;
        public int winstreak = 0;
        public int losestreak = 0;
        public decimal Lastbet = 0;
        long beginMs = 0;

        List<decimal> highestProfit = new List<decimal> { 0 };
        List<decimal> lowestProfit = new List<decimal> { 0 };
        List<decimal> highestBet = new List<decimal> { 0 };

        List<int> highestStreak = new List<int> { 0 };
        List<int> lowestStreak = new List<int> { 0 };

        public lastbet last = new lastbet();

        public string[] currenciesAvailable = {
            "BTC","ETH","LTC","DOGE","BCH","XRP","TRX","EOS","BNB",
            "USDT","APE","BUSD","CRO","DAI","LINK","SAND","SHIB",
            "UNI","USDC","VND","TRY","TRUMP","SWEEPS","POL","BRL"
        };

        private bool is_connected = false;
        private string UserAgent = "";
        private string ClearanceCookie = "";

        // true when "Use Extension" (index 1) is selected
        private bool UseExtensionMode => cmbFetchMode.SelectedIndex == 1;

        private void fillCurrencies()
        {
            foreach (var item in currenciesAvailable)
            {
                //currencyComboBox.Items.Add(item);
            }
        }

        public Form1()
        {
            InitializeComponent();

            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);

            fillCurrencies();

            this.listView1.ItemChecked += this.listView1_ItemChecked;
            this.CommandBox2.KeyDown += this.CmdBox_KeyDown;
            this.listBox3.KeyDown += this.listBox3_KeyDown;

            richTextBox2.ReadOnly = true;
            richTextBox2.BackColor = Color.FromArgb(249, 249, 249);
            listView1.BackColor = Color.FromArgb(249, 249, 249);
            listBox3.BackColor = Color.FromArgb(249, 249, 249);
            listView1.SetDoubleBuffered(true);

            Text += " - " + Application.ProductVersion;
            Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);

            xList.Add(0);
            yList.Add(0);

            data.Add(new ObservablePoint { X = xList[counter], Y = yList[counter] });

            ch.Series = new SeriesCollection
            {
                new LiveCharts.Wpf.LineSeries
                {
                    Title = "Profit",
                    Values = data,
                    PointGeometrySize = 0,
                    AreaLimit = 0
                }
            };

            ch.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                /*Separator = new LiveCharts.Wpf.Separator { Step = 15 }*/
            });

            Func<double, string> formatFunc = (x) => string.Format("{0:0.000000}", x);
            ch.AxisY.Add(new LiveCharts.Wpf.Axis { LabelFormatter = formatFunc });

            ch.Width = 400;
            panel1.Controls.Add(ch);

            richTextBox1 = new FastColoredTextBox();
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Language = Language.Lua;
            richTextBox1.BorderStyle = BorderStyle.None;
            richTextBox1.BackColor = Color.FromArgb(249, 249, 249);
            tabPageLua.Controls.Add(richTextBox1);

            richTextBox1.TextChanged += this.richTextBox1_TextChanged;

            richTextBox1.Text = Properties.Settings.Default.textCode;
            textBox3.Text = Properties.Settings.Default.cookie;
            textBox4.Text = Properties.Settings.Default.agent;
            UserAgent = textBox4.Text;
            ClearanceCookie = textBox3.Text;

            // Subscribe to BrowserFetch socket events.
            // These fire on the Fleck thread, so always Invoke back to the UI thread.
            BrowserFetch.Connected += (s, e) => this.Invoke((MethodInvoker)delegate ()
            {
                lblCookieStatus.Text = "⬤ Extension ON";
                lblCookieStatus.ForeColor = Color.Green;
                lblCookieStatus.Visible = true;
               
            });
            BrowserFetch.Disconnected += (s, e) => this.Invoke((MethodInvoker)delegate ()
            {
                if (UseExtensionMode)
                {
                    lblCookieStatus.Text = "⬤ Extension OFF";
                    lblCookieStatus.ForeColor = Color.Orange;
                    lblCookieStatus.Visible = true;
                }
            });
            


            int savedIndex = Properties.Settings.Default.fetchmode;
            cmbFetchMode.SelectedIndex = savedIndex;
            
            // Set initial status labels based on saved cookie (no button click needed)
            UpdateStatusLabels();
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  FetchMode combo  ──  "Use Cookie" (0)  |  "Use Extension" (1)
        // ═════════════════════════════════════════════════════════════════════════
        private void cmbFetchMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            currencyComboBox.Items.Clear();

            Properties.Settings.Default.fetchmode = cmbFetchMode.SelectedIndex;
            Properties.Settings.Default.Save();
            bool extMode = UseExtensionMode;

            btnGetCookie.Enabled = !extMode;
            textBox3.Enabled     = !extMode;
            textBox4.Enabled     = !extMode;

            if (extMode)
            {
                // Show OFF immediately; Connected event will flip it to ON once socket opens
                lblCookieStatus.Text      = "⬤ Extension OFF";
                lblCookieStatus.ForeColor = Color.Orange;
                lblCookieStatus.Visible   = true;
                BrowserFetch.StartServer();

                // If the extension was already connected from a previous session, show ON now
                if (BrowserFetch.IsConnected)
                {
                    lblCookieStatus.Text      = "⬤ Extension ON";
                    lblCookieStatus.ForeColor = Color.Green;
                }
            }
            else
            {
                // Switched back to Cookie mode — stop the server and restore cookie status
                //BrowserFetch.StopServer();
                bool hasCookie = !string.IsNullOrEmpty(ClearanceCookie);
                lblCookieStatus.Text      = hasCookie ? "⬤ Cookie OK" : "⬤ Cookie OFF";
                lblCookieStatus.ForeColor = hasCookie ? Color.Green : Color.Orange;
                lblCookieStatus.Visible   = true;
               
            }
            
        }

        // ── Get-Cookie button ─────────────────────────────────────────────────────
        private void btnGetCookie_Click(object sender, EventArgs e)
        {
            using (var loginForm = new WebViewLogin(StakeSite))
            {
                var result = loginForm.ShowDialog(this);

                if (result == DialogResult.OK)
                {
                    ClearanceCookie = loginForm.CapturedClearance;
                    UserAgent       = loginForm.CapturedUserAgent;

                    textBox3.Text = ClearanceCookie;
                    textBox4.Text = UserAgent;

                    Properties.Settings.Default.cookie = ClearanceCookie;
                    Properties.Settings.Default.agent  = UserAgent;
                    Properties.Settings.Default.Save();

                    cc = new CookieContainer();   // rebuild cookie jar
                }
                // Status label always reflects actual cookie state after dialog closes
                UpdateStatusLabels();
            }
        }

        // ── Status helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Master status updater — call whenever mode, cookie value, or WS state changes.
        /// ⬤ is embedded in label text; separate dot control is always hidden.
        /// </summary>
        private void UpdateStatusLabels(bool wsConnected = false)
        {
            

            // Separate dot control no longer needed — hide it permanently


            if (!UseExtensionMode)
            {
                // ── Cookie mode ────────────────────────────────────────────────
                bool hasCookie = !string.IsNullOrEmpty(ClearanceCookie);
                lblCookieStatus.Text = hasCookie ? "⬤ Cookie OK" : "⬤ Cookie OFF";
                lblCookieStatus.ForeColor = hasCookie ? Color.Green : Color.Orange;
                lblCookieStatus.Visible = true;  // extension status not relevant
               
            }
            else
            {
                // ── Extension mode ─────────────────────────────────────────────
                lblCookieStatus.Visible = true;     // cookie info not relevant

                if (wsConnected)
                {
                    lblCookieStatus.Text = "⬤ Extension OK";
                    lblCookieStatus.ForeColor = Color.Green;
                    lblCookieStatus.Visible = true;
                }
                else
                {
                    lblCookieStatus.Text = "⬤ Extension OFF";
                    lblCookieStatus.ForeColor = Color.Orange;
                    lblCookieStatus.Visible = true;
                }

            }
        }

        /// <summary>Thin wrapper called from WS open/close events.</summary>
        private void UpdateWsStatus(bool connected)
        {
            UpdateStatusLabels(wsConnected: connected);
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  Central GraphQL dispatcher
        //    index 0  "Use Cookie"    → RestSharp + cf_clearance cookie
        //    index 1  "Use Extension" → BrowserFetch.FetchAsync
        // ═════════════════════════════════════════════════════════════════════════
        private async Task<string> GraphQL(string operationName, string query,
                                            BetClass variables = null)
        {
            var url = "https://" + StakeSite + "/_api/graphql";

            if (UseExtensionMode)
            {
                // ── Extension path ──────────────────────────────────────────────
                var body = new BetSend
                {
                    operationName = operationName,
                    query         = query,
                    variables     = variables
                };
                var options = new
                {
                    method  = "POST",
                    headers = new Dictionary<string, string>
                    {
                        { "Content-Type",   "application/json" },
                        { "x-access-token", token }
                    },
                    body = body
                };
                return await BrowserFetch.FetchAsync(url, options);
            }
            else
            {
                // ── RestSharp (cookie) path ─────────────────────────────────────
                var client  = new RestClient(url);
                var request = new RestRequest(Method.POST);

                client.CookieContainer = cc;
                client.UserAgent       = UserAgent;
                client.CookieContainer.Add(
                    new Cookie("cf_clearance", ClearanceCookie, "/", StakeSite));

                var payload = new BetQuery
                {
                    token         = token,
                    operationName = operationName,
                    query         = query,
                    variables     = variables
                };

                request.AddHeader("Content-Type",   "application/json");
                request.AddHeader("x-access-token", token);
                request.AddParameter("application/json",
                    JsonConvert.SerializeObject(payload),
                    ParameterType.RequestBody);

                var resp = await client.ExecuteAsync(request);
                return resp.Content;
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  Misc / Lua helpers
        // ═════════════════════════════════════════════════════════════════════════
        private void listBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                string tmpStr = "";
                foreach (var item in listBox3.SelectedItems)
                    tmpStr += listBox3.GetItemText(item) + "\n";
                Clipboard.SetData(DataFormats.StringFormat, tmpStr);
            }
            if (e.Control && e.KeyCode == Keys.A)
            {
                for (int i = 0; i < listBox3.Items.Count; i++)
                    listBox3.SetSelected(i, true);
            }
        }

        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void RegisterLua()
        {
            lua.RegisterFunction("vault",      this, new dvault(luaVault).Method);
            lua.RegisterFunction("tip",        this, new dtip(luatip).Method);
            lua.RegisterFunction("print",      this, new LogConsole(luaPrint).Method);
            lua.RegisterFunction("stop",       this, new dStop(luaStop).Method);
            lua.RegisterFunction("resetseed",  this, new dResetSeed(luaResetSeed).Method);
            lua.RegisterFunction("resetstats", this, new dResetStat(luaResetStat).Method);
        }

        private void SetLuaVariables(decimal profitCurr)
        {
            lua["balance"]       = currentBal;
            lua["profit"]        = currentProfit;
            lua["currentstreak"] = (winstreak > 0) ? winstreak : -losestreak;
            lua["previousbet"]   = Lastbet;
            lua["bets"]          = wins + losses;
            lua["wins"]          = wins;
            lua["losses"]        = losses;
            lua["currency"]      = currencySelected;
            lua["wagered"]       = currentWager;
            lua["win"]           = isWin;
            lua["lastBet"]       = last;
            lua["currentprofit"] = profitCurr;
        }

        private void UnSetVariables()
        {
            lua["balance"] = null;
            lua["nextbet"] = null;
            lua["target"]  = null;
        }

        private void GetLuaVariables()
        {
            try
            {
                Lastbet          = (decimal)(double)lua["nextbet"];
                amount           = Lastbet;
                currencySelected = (string)lua["currency"];
                target           = (double)lua["target"];
                TargetLabeL.Text = target.ToString("0.00") + "x";
            }
            catch (Exception e)
            {
                ready = false;
                bSta();
                luaPrint("Please set 'nextbet = x' and 'target = x' variable on top of script.");
            }
        }

        public void bSta()
        {
            running = false;
            button1.Enabled = true;
            currencyComboBox.Enabled = true;
            button1.Text = "Start";
        }

        public void Log(Data response)
        {
            string[] row = {
                response.data.limboBet.id,
                String.Format("{0}x|{1}",
                    response.data.limboBet.payoutMultiplier.ToString("0.00"),
                    response.data.limboBet.state.result.ToString("0.0000")),
                response.data.limboBet.amount.ToString("0.00000000") + " " + currencySelected,
                (response.data.limboBet.payout - response.data.limboBet.amount).ToString("0.00000000"),
                response.data.limboBet.game
            };
            var log = new ListViewItem(row);
            listView1.Items.Insert(0, log);
            if (listView1.Items.Count > 15)
                listView1.Items[listView1.Items.Count - 1].Remove();
            log.BackColor = response.data.limboBet.payoutMultiplier > 0
                ? Color.FromArgb(170, 250, 190)
                : Color.FromArgb(250, 185, 170);
        }

        private void SetStatistics()
        {
            balanceLabel.Text       = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);
            profitLabel.Text        = currentProfit.ToString("0.00000000");
            wagerLabel.Text         = currentWager.ToString("0.00000000");
            wltLabel.Text           = String.Format("{0} / {1} / {2}", wins, losses, wins + losses);
            currentStreakLabel.Text  = String.Format("{0} / {1} / {2}",
                (winstreak > 0) ? winstreak.ToString() : (-losestreak).ToString(),
                highestStreak.Max(), lowestStreak.Min());
            lowestProfitLabel.Text  = lowestProfit.Min().ToString("0.00000000");
            highestProfitLabel.Text = highestProfit.Max().ToString("0.00000000");
            highestBetLabel.Text    = highestBet.Max().ToString("0.00000000");
        }

        void luaStop()
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                luaPrint("Called stop.");
                running = false; sim = false; bSta();
            });
        }
        void luaVault(decimal sentamount) { this.Invoke((MethodInvoker)delegate () { VaultSend(sentamount); }); }
        void luatip(string user, decimal amount) { this.Invoke((MethodInvoker)delegate () { luaPrint("Tipping not available."); }); }
        void luaPrint(string text) { this.Invoke((MethodInvoker)delegate () { richTextBox2.AppendText(text + "\r\n"); }); }
        void luaResetSeed() { this.Invoke((MethodInvoker)delegate () { ResetSeeds(); }); }

        void luaResetStat()
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                currentProfit = 0; currentWager = 0;
                wins = 0; losses = 0; winstreak = 0; losestreak = 0;
                lowestStreak  = new List<int>     { 0 };
                highestStreak = new List<int>     { 0 };
                highestProfit = new List<decimal> { 0 };
                lowestProfit  = new List<decimal> { 0 };
                highestBet    = new List<decimal> { 0 };
                wltLabel.Text           = "0 / 0 / 0";
                currentStreakLabel.Text  = "0 / 0 / 0";
                profitLabel.Text        = currentProfit.ToString("0.00000000");
                wagerLabel.Text         = currentWager.ToString("0.00000000");
                wltLabel.Text           = String.Format("{0} / {1} / {2}", wins, losses, wins + losses);
                currentStreakLabel.Text  = String.Format("{0} / {1} / {2}",
                    (winstreak > 0) ? winstreak.ToString() : (-losestreak).ToString(),
                    highestStreak.Max(), lowestStreak.Min());
                lowestProfitLabel.Text  = lowestProfit.Min().ToString("0.00000000");
                highestProfitLabel.Text = highestProfit.Max().ToString("0.00000000");
                highestBetLabel.Text    = highestBet.Max().ToString("0.00000000");
            });
        }

        private void LogButton_Click(object sender, EventArgs e)
        {
            if (LogButton.Text.Contains(">"))
            {
                LogButton.Text = "Log <";
                listLogs.Add(listView1);
                listView1.Show();
            }
            else
            {
                listLogs.Clear();
                LogButton.Text = "Log >";
            }
        }

        private async void textBox1_TextChanged(object sender, EventArgs e)
        {
            token = textBox1.Text;
            Properties.Settings.Default.token = token;
        }

        private async void currencyComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            currencySelected = currencyComboBox.Text.ToLower();
            //await CheckBalance();
            //await Authorize();
            balanceLabel.Text = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);
        }

        private void SiteComboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            StakeSite = textBox2.Text.ToLower();
            Properties.Settings.Default.indexSite = textBox2.Text;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (!running)
            {
                ready = true;
                RegisterLua();
                await CheckBalance();

                try
                {
                    UnSetVariables();
                    SetLuaVariables(0);
                    LuaRuntime.SetLua(lua);
                    LuaRuntime.Run(richTextBox1.Text);
                }
                catch (Exception ex)
                {
                    luaPrint("Lua ERROR!!"); luaPrint(ex.Message);
                    running = false; bSta();
                }
                GetLuaVariables();

                if (ready)
                {
                    button1.Enabled = false;
                    running = true;
                    button1.Text = "Stop";
                    beginMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    StartBet();
                }
                else bSta();
            }
            else
            {
                running = false;
                bSta();
            }
        }

        async Task StartBet()
        {
            while (running)
            {
                if (beginMs == 0)
                    beginMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                await LimboBet();
            }
        }

        private async Task Balances()
        {
            messagePayload mp = new messagePayload
            {
                accessToken = token,
                query = "subscription AvailableBalances {\n  availableBalances {\n    amount\n    identifier\n    balance {\n      amount\n      currency\n    }\n  }\n}\n"
            };
            messageData md = new messageData { id = "6cc429c1-a18a-4a6a-819e-1c78c724b5f8", type = "subscribe", payload = mp };
            this.chat_socket.Send(JsonConvert.SerializeObject(md));
        }

        public void Connect()
        {
            try
            {
                Debug.WriteLine(StakeSite);
                this.chat_socket = new WebSocket(
                    "wss://api." + StakeSite + "/websockets",
                    "graphql-transport-ws",
                    new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("jwt", token) },
                    userAgent: "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_14_6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.163 Safari/537.36",
                    origin: "https://" + StakeSite,
                    version: WebSocketVersion.Rfc6455,
                    sslProtocols: SslProtocols.Tls12);

                this.chat_socket.EnableAutoSendPing   = true;
                this.chat_socket.AutoSendPingInterval = 1000;
                this.chat_socket.MessageReceived += new EventHandler<MessageReceivedEventArgs>(this.chat_socket_MessageReceived);
                this.chat_socket.Opened          += new EventHandler(this.chat_socket_Opened);
                this.chat_socket.Error           += new EventHandler<ErrorEventArgs>(this.chat_socket_Error);
                this.chat_socket.Closed          += new EventHandler(this.chat_socket_Closed);
                this.chat_socket.Open();
            }
            catch (Exception ex) { Debug.WriteLine(ex.ToString()); }
        }

        private async void chat_socket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            messageData _msg = JsonConvert.DeserializeObject<messageData>(e.Message);
            string type = _msg.type;

            if (type == "connection_ack")
            {
                is_connected = true;
                this.Invoke((MethodInvoker)delegate () { UpdateWsStatus(true); });
            }
            else if (type == "next")
            {
                if (_msg.payload.errors.Count > 0)
                {
                    if (_msg.payload.errors[0].message.Contains("invalid") ||
                        _msg.payload.errors[0].message.Contains("expired"))
                        this.Invoke((MethodInvoker)delegate () { this.chat_socket.Close(); });
                }
                else if (_msg.payload.data.availableBalances != null)
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        if (_msg.payload.data.availableBalances.balance.currency == currencySelected.ToLower())
                        {
                            currentBal = _msg.payload.data.availableBalances.balance.amount;
                            try { lua["balance"] = currentBal; LuaRuntime.SetLua(lua); }
                            catch (Exception ex) { luaPrint("Lua ERROR!!"); luaPrint(ex.Message); running = false; bSta(); }
                            balanceLabel.Text = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);
                        }
                    });
                }
            }
        }

        private void chat_socket_Opened(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel1.Text = "Connected";
                this.Invoke((MethodInvoker)delegate () { UpdateWsStatus(true); });
                this.chat_socket.Send(JsonConvert.SerializeObject(new messageData()
                {
                    type    = "connection_init",
                    payload = new messagePayload() { accessToken = token, language = "en" }
                }));
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void chat_socket_Error(object sender, ErrorEventArgs e)
        {
            try { Debug.WriteLine(e.Exception); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private async void chat_socket_Closed(object sender, EventArgs e)
        {
            try
            {
                if (!this.is_connected) return;
                await Task.Delay(400);
                this.Invoke((MethodInvoker)delegate ()
                {
                    toolStripStatusLabel1.Text = "Re-connecting...";
                    UpdateWsStatus(false);
                });
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  LimboBet
        // ═════════════════════════════════════════════════════════════════════════
        async Task LimboBet()
        {
            try
            {
                if (!running) return;

                var json = await GraphQL(
                    "LimboBet",
                    "mutation LimboBet($amount: Float!, $multiplierTarget: Float!, $currency: CurrencyEnum!, $identifier: String!) {\n limboBet(\n amount: $amount\n currency: $currency\n multiplierTarget: $multiplierTarget\n identifier: $identifier\n  ) {\n...CasinoBet\n state {\n...CasinoGameLimbo\n    }\n  }\n}\n\nfragment CasinoBet on CasinoBet {\n id\n active\n payoutMultiplier\n amountMultiplier\n amount\n payout\n updatedAt\n currency\n game\n user {\n id\n name\n  }\n}\n\nfragment CasinoGameLimbo on CasinoGameLimbo {\n result\n multiplierTarget\n}\n",
                    new BetClass
                    {
                        currency         = currencySelected,
                        amount           = amount,
                        multiplierTarget = target,
                        identifier       = RandomString(21)
                    }
                );

                button1.Enabled = true;
                Data response = JsonConvert.DeserializeObject<Data>(json);

                if (response.errors != null)
                {
                    luaPrint(String.Format("{0}:{1}", response.errors[0].errorType, response.errors[0].message));
                    if (running) await Task.Delay(2000);
                    else { running = false; bSta(); }
                }
                else
                {
                    TimerFunc(beginMs);
                    currentWager += response.data.limboBet.amount;

                    if (response.data.limboBet.payoutMultiplier > 0)
                    { losestreak = 0; winstreak++; isWin = true; wins++; ResultLabeL.ForeColor = Color.LimeGreen; }
                    else
                    { losestreak++; winstreak = 0; isWin = false; losses++; ResultLabeL.ForeColor = Color.Red; }

                    Log(response);
                    await CheckBalance();

                    decimal profitCurr = response.data.limboBet.payout - response.data.limboBet.amount;
                    currentProfit += profitCurr;
                    TargetLabeL.Text = response.data.limboBet.state.multiplierTarget.ToString("0.00") + "x";
                    ResultLabeL.Text = response.data.limboBet.state.result.ToString("0.00") + "x";
                    last.target = response.data.limboBet.state.multiplierTarget;
                    last.result = response.data.limboBet.state.result;

                    highestStreak.Add(winstreak);  highestStreak = new List<int>     { highestStreak.Max() };
                    lowestStreak.Add(-losestreak); lowestStreak  = new List<int>     { lowestStreak.Min() };
                    if (currentProfit < 0) { lowestProfit.Add(currentProfit);  lowestProfit  = new List<decimal> { lowestProfit.Min()  }; }
                    else                   { highestProfit.Add(currentProfit); highestProfit = new List<decimal> { highestProfit.Max() }; }
                    highestBet.Add(amount); highestBet = new List<decimal> { highestBet.Max() };
                    SetStatistics();

                    counter++; xList.Add(counter); yList.Add((double)currentProfit);
                    data.Add(new ObservablePoint { X = xList[xList.Count - 1], Y = yList[yList.Count - 1] });
                    if (data.Count > 20) { data.RemoveAt(0); xList.RemoveAt(0); yList.RemoveAt(0); }

                    try
                    {
                        SetLuaVariables(profitCurr);
                        LuaRuntime.SetLua(lua);
                        LuaRuntime.Run("dobet()");
                    }
                    catch (Exception ex) { luaPrint("Lua ERROR!!"); luaPrint(ex.Message); running = false; bSta(); }
                    GetLuaVariables();
                }
            }
            catch { }
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  CheckBalance
        // ═════════════════════════════════════════════════════════════════════════
        public async Task CheckBalance()
        {
           // currencyComboBox.Items.Clear();
            try
            {
                var json = await GraphQL(
                    "UserBalances",
                    "query UserBalances {\n  user {\n    id\n    balances {\n      available {\n        amount\n        currency\n        __typename\n      }\n      vault {\n        amount\n        currency\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n}\n"
                );
                BalancesData response = JsonConvert.DeserializeObject<BalancesData>(json);
                if (response?.data != null)
                {
                    for (var i = 0; i < response.data.user.balances.Count; i++)
                    {
                        if (response.data.user.balances[i].available.currency == currencySelected.ToLower())
                        {
                            currentBal = response.data.user.balances[i].available.amount;
                            balanceLabel.Text = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);
                        }
                    }
                }
            }
            catch { }
        }

        public void TimerFunc(long begin)
        {
            decimal diff    = (decimal)((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - begin);
            decimal seconds = Math.Floor((diff / 1000) % 60);
            decimal minutes = Math.Floor((diff / (1000 * 60)) % 60);
            decimal hours   = Math.Floor(diff / (1000 * 60 * 60));
            Time.Text = String.Format("{0} : {1} : {2}", hours, minutes, seconds);
        }

        private void clearLinkbtn_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            textBox1.Clear();
            textBox1.Enabled = true;
            token = "";
            toolStripStatusLabel1.Text = "Disconnected";
        }

        private async void CheckBtn_Click(object sender, EventArgs e)
        {
            CheckBtn.Enabled = false;
            await CheckBalance();
            await Authorize();
            CheckBtn.Enabled = true;
        }

        public string RandomString(int length)
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.textCode = richTextBox1.Text;
        }

        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {
            if (richTextBox2.Lines.Length > 100)
            {
                List<string> lines = richTextBox2.Lines.ToList();
                lines.RemoveAt(0);
                richTextBox2.Lines = lines.ToArray();
            }
            richTextBox2.SelectionStart = richTextBox2.Text.Length;
            richTextBox2.ScrollToCaret();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            currentProfit = 0; currentWager = 0;
            wins = 0; losses = 0; winstreak = 0; losestreak = 0;
            lowestStreak  = new List<int>     { 0 };
            highestStreak = new List<int>     { 0 };
            highestProfit = new List<decimal> { 0 };
            lowestProfit  = new List<decimal> { 0 };
            highestBet    = new List<decimal> { 0 };
            beginMs = 0; Time.Text = "0 : 0 : 0";
            wltLabel.Text = "0 / 0 / 0"; currentStreakLabel.Text = "0 / 0 / 0";
            counter = 0; yList.Clear(); xList.Clear(); xList.Add(0); yList.Add(0); data.Clear();
            profitLabel.Text  = currentProfit.ToString("0.00000000");
            wagerLabel.Text   = currentWager.ToString("0.00000000");
            wltLabel.Text     = String.Format("{0} / {1} / {2}", wins, losses, wins + losses);
            currentStreakLabel.Text = String.Format("{0} / {1} / {2}",
                (winstreak > 0) ? winstreak.ToString() : (-losestreak).ToString(),
                highestStreak.Max(), lowestStreak.Min());
            lowestProfitLabel.Text  = lowestProfit.Min().ToString("0.00000000");
            highestProfitLabel.Text = highestProfit.Max().ToString("0.00000000");
            highestBetLabel.Text    = highestBet.Max().ToString("0.00000000");
        }

        private void CommandButton2_Click(object sender, EventArgs e)
        {
            try { if (CommandBox2.Text.Length > 0) LuaRuntime.Run(CommandBox2.Text); }
            catch (Exception ex) { luaPrint("Lua ERROR!!"); luaPrint(ex.Message); running = false; bSta(); }
        }

        private void listView1_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (e.Item.Checked)
                Process.Start(new ProcessStartInfo(
                    string.Format("https://{1}/casino/home?betId={0}&modal=bet", e.Item.Text, StakeSite))
                    { UseShellExecute = true });
        }

        private void CmdBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) CommandButton2_Click(this, new EventArgs());
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  VaultSend / ResetSeeds / SendTip / Authorize
        // ═════════════════════════════════════════════════════════════════════════
        private async Task VaultSend(decimal sentamount)
        {
            try
            {
                var json = await GraphQL(
                    "CreateVaultDeposit",
                    "mutation CreateVaultDeposit($currency: CurrencyEnum!, $amount: Float!) {\n  createVaultDeposit(currency: $currency, amount: $amount) {\n    id\n    amount\n    currency\n    user {\n      id\n      balances {\n        available {\n          amount\n          currency\n          __typename\n        }\n        vault {\n          amount\n          currency\n          __typename\n        }\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n}\n",
                    new BetClass { currency = currencySelected.ToLower(), amount = sentamount }
                );
                Data response = JsonConvert.DeserializeObject<Data>(json);
                if (response.errors != null) luaPrint(response.errors[0].errorType + ":" + response.errors[0].message);
                else if (response.data != null)
                    luaPrint(string.Format("Deposited to vault: {0} {1}", sentamount.ToString("0.00000000"), currencySelected));
            }
            catch { }
        }

        private async Task ResetSeeds()
        {
            try
            {
                var json = await GraphQL(
                    "RotateSeedPair",
                    "mutation RotateSeedPair($seed: String!) {\n  rotateSeedPair(seed: $seed) {\n    clientSeed {\n      user {\n        id\n        activeClientSeed {\n          id\n          seed\n          __typename\n        }\n        activeServerSeed {\n          id\n          nonce\n          seedHash\n          nextSeedHash\n          __typename\n        }\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n}\n",
                    new BetClass { seed = RandomString(10) }
                );
                Data response = JsonConvert.DeserializeObject<Data>(json);
                if (response.errors != null) luaPrint(response.errors[0].errorType + ":" + response.errors[0].message);
                else if (response.data != null) luaPrint("Seed was reset.");
            }
            catch { }
        }

        private async Task SendTip()
        {
            // SendTip uses its own separate API base URL so keeps direct RestSharp
            try
            {
                var mainurl = "https://api." + StakeSite + "/graphql";
                var client  = new RestClient(mainurl);
                var request = new RestRequest(Method.POST);
                client.CookieContainer = cc;
                client.UserAgent       = UserAgent;
                client.CookieContainer.Add(new Cookie("cf_clearance", ClearanceCookie, "/", StakeSite));
                BetQuery payload = new BetQuery
                {
                    operationName = "RotateSeedPair",
                    variables     = new BetClass { seed = RandomString(10) },
                    query = "mutation RotateSeedPair($seed: String!) {\n  rotateSeedPair(seed: $seed) {\n    clientSeed {\n      user { id activeClientSeed { id seed __typename } activeServerSeed { id nonce seedHash nextSeedHash __typename } __typename }\n      __typename\n    }\n    __typename\n  }\n}\n"
                };
                request.AddHeader("Content-Type",   "application/json");
                request.AddHeader("x-access-token", token);
                request.AddParameter("application/json", JsonConvert.SerializeObject(payload), ParameterType.RequestBody);
                var restResponse = await client.ExecuteAsync(request);
                Data response    = JsonConvert.DeserializeObject<Data>(restResponse.Content);
                if (response.errors != null) luaPrint(response.errors[0].errorType + ":" + response.errors[0].message);
                else if (response.data != null) luaPrint("Not functional.");
            }
            catch { }
        }

        private async Task Authorize()
        {
           // currencyComboBox.Items.Clear();
            try
            {
                var json = await GraphQL(
                    "UserBalances",
                    "query UserBalances {\n  user {\n    id\n    balances {\n      available {\n        amount\n        currency\n        __typename\n      }\n      vault {\n        amount\n        currency\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n}\n"
                );
                BalancesData response = JsonConvert.DeserializeObject<BalancesData>(json);
                if (response == null || response.errors != null)
                {
                    toolStripStatusLabel1.Text = "Disconnected";
                }
                else if (response.data != null)
                {
                    toolStripStatusLabel1.Text = "Connected.";
                    for (var i = 0; i < response.data.user.balances.Count; i++)
                    {
                        if (response.data.user.balances[i].available.currency == currencySelected.ToLower())
                        {
                            currentBal = response.data.user.balances[i].available.amount;
                            balanceLabel.Text = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);
                        }
                    }
                    currencyComboBox.Items.Clear();
                    for (var k = 0; k < response.data.user.balances.Count; k++)
                        currencyComboBox.Items.Add(response.data.user.balances[k].available.currency);
                }
            }
            catch { }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Text      = Properties.Settings.Default.token;
            textBox2.Text      = Properties.Settings.Default.indexSite;
            ServerSeedBox.Text = Properties.Settings.Default.serverSeed;
            ClientSeedBox.Text = Properties.Settings.Default.clientSeed;
            NonceBox.Text      = Properties.Settings.Default.nonce.ToString();
            NonceStopBox.Text  = Properties.Settings.Default.nonceStop.ToString();
        }

        private void SimulateRun()
        {
            while (sim)
            {
                if (nonce > stopNonce || balanceSim < amount || target <= 1)
                {
                    if (target <= 1) { luaPrint("Lua ERROR!!"); luaPrint("Lua: Target must be above 1."); }
                    SimulateButton_Click_1(this, new EventArgs()); sim = false; break;
                }
                decimal result = LimboResult(serverSeed, clientSeed, nonce);
                nonce++;

                decimal payout = 0, payoutMultiplier = 0;
                currentWager += amount;
                string winStatus = "lose";

                if (result > (decimal)target)
                {
                    losestreak = 0; winstreak++; isWin = true; wins++;
                    payout = (decimal)target * amount; payoutMultiplier = (decimal)target;
                    winStatus = "win"; ResultLabeL.ForeColor = Color.LimeGreen;
                }
                else
                { losestreak++; winstreak = 0; isWin = false; losses++; ResultLabeL.ForeColor = Color.Red; }

                decimal profitCurr = payout - amount;
                currentProfit += profitCurr; balanceSim += profitCurr;
                TargetLabeL.Text = target.ToString("0.00") + "x";
                ResultLabeL.Text = result.ToString("0.00") + "x";
                last.target = target; last.result = (double)result;

                highestStreak.Add(winstreak);  highestStreak = new List<int>     { highestStreak.Max() };
                lowestStreak.Add(-losestreak); lowestStreak  = new List<int>     { lowestStreak.Min() };
                if (currentProfit < 0) { lowestProfit.Add(currentProfit);  lowestProfit  = new List<decimal> { lowestProfit.Min()  }; }
                else                   { highestProfit.Add(currentProfit); highestProfit = new List<decimal> { highestProfit.Max() }; }
                highestBet.Add(amount); highestBet = new List<decimal> { highestBet.Max() };

                this.Invoke((MethodInvoker)delegate ()
                {
                    balanceLabel.Text       = balanceSim.ToString("0.00000000");
                    profitLabel.Text        = currentProfit.ToString("0.00000000");
                    wagerLabel.Text         = currentWager.ToString("0.00000000");
                    wltLabel.Text           = String.Format("{0} / {1} / {2}", wins, losses, wins + losses);
                    currentStreakLabel.Text  = String.Format("{0} / {1} / {2}",
                        (winstreak > 0) ? winstreak.ToString() : (-losestreak).ToString(),
                        highestStreak.Max(), lowestStreak.Min());
                    lowestProfitLabel.Text  = lowestProfit.Min().ToString("0.00000000");
                    highestProfitLabel.Text = highestProfit.Max().ToString("0.00000000");
                    highestBetLabel.Text    = highestBet.Max().ToString("0.00000000");
                    Application.DoEvents();
                });

                string box = String.Format("[{0}] {4}x  |  {1}   |  bet: {5}  |  profit:  {2}   [{3}]",
                    nonce - 1, result.ToString("0.0000"), currentProfit.ToString("0.00000000"),
                    winStatus, target.ToString("0.00"), amount.ToString("0.00000000"));
                listBox3.Items.Insert(0, box);
                if (listBox3.Items.Count > 200) listBox3.Items.RemoveAt(listBox3.Items.Count - 1);

                try
                {
                    lua["balance"] = balanceSim; lua["profit"] = currentProfit;
                    lua["currentstreak"] = (winstreak > 0) ? winstreak : -losestreak;
                    lua["previousbet"] = Lastbet; lua["nextbet"] = Lastbet;
                    lua["bets"] = wins + losses; lua["wins"] = wins; lua["losses"] = losses;
                    lua["currency"] = currencySelected; lua["wagered"] = currentWager;
                    lua["win"] = isWin; lua["lastBet"] = last; lua["currentprofit"] = profitCurr;
                    LuaRuntime.SetLua(lua); LuaRuntime.Run("dobet()");
                }
                catch (Exception ex) { luaPrint("Lua ERROR!!"); luaPrint(ex.Message); sim = false; }

                Lastbet = (decimal)(double)lua["nextbet"];
                amount  = Lastbet;
                currencySelected = (string)lua["currency"];
                target  = (double)lua["target"];
            }
        }

        static decimal LimboResult(string serverSeed, string clientSeed, int nonce)
        {
            string hex = HmacSha256Digest(string.Format("{0}:{1}:{2}", clientSeed, nonce, 0), serverSeed);
            decimal end = 0;
            for (int i = 0; i < 4; i++)
                end += (decimal)(Convert.ToInt32(hex.Substring(i * 2, 2), 16) / Math.Pow(256, i + 1));
            end *= 16777216;
            end  = 16777216 / (Math.Floor(end) + 1) * (decimal)(1 - 0.01);
            return end;
        }

        public static string HmacSha256Digest(string message, string secret)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] keyBytes     = encoding.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            var crypto = new System.Security.Cryptography.HMACSHA256(keyBytes);
            return BitConverter.ToString(crypto.ComputeHash(messageBytes)).Replace("-", "").ToLower();
        }

        private void ServerSeedBox_TextChanged(object sender, EventArgs e)
        { serverSeed = ServerSeedBox.Text; Properties.Settings.Default.serverSeed = serverSeed; }
        private void ClientSeedBox_TextChanged(object sender, EventArgs e)
        { clientSeed = ClientSeedBox.Text; Properties.Settings.Default.clientSeed = clientSeed; }
        private void NonceBox_TextChanged(object sender, EventArgs e)
        { nonce = Int32.Parse(NonceBox.Text); Properties.Settings.Default.nonce = nonce; }

        private void SimulateButton_Click_1(object sender, EventArgs e)
        {
            if (SimulateButton.Text.Contains("Off"))
            { SimulateButton.Text = "Simulate"; sim = false; }
            else
            {
                nonce = Int32.Parse(NonceBox.Text);
                serverSeed = ServerSeedBox.Text; clientSeed = ClientSeedBox.Text;
                linkLabel1_LinkClicked(this, new LinkLabelLinkClickedEventArgs(new LinkLabel.Link()));
                SimulateButton.Text = "Off"; sim = true; RegisterSim();
                lua["balance"] = null; lua["nextbet"] = null; lua["target"] = null;
                try
                {
                    lua["profit"] = currentProfit;
                    lua["currentstreak"] = (winstreak > 0) ? winstreak : -losestreak;
                    lua["previousbet"] = Lastbet; lua["bets"] = wins + losses;
                    lua["wins"] = wins; lua["losses"] = losses;
                    lua["currency"] = currencySelected; lua["wagered"] = currentWager;
                    lua["win"] = isWin; lua["lastBet"] = last;
                    LuaRuntime.SetLua(lua); LuaRuntime.Run(richTextBox1.Text);
                }
                catch (Exception ex) { luaPrint("Lua ERROR!!"); luaPrint(ex.Message); sim = false; }

                try
                {
                    Lastbet = (decimal)(double)lua["nextbet"]; amount = Lastbet;
                    currencySelected = (string)lua["currency"]; target = (double)lua["target"];
                    balanceSim = (decimal)(double)lua["balance"];
                }
                catch
                {
                    luaPrint("Please set 'balance = x' and 'target = x' and 'nextbet = x' variable on top of script.");
                    SimulateButton_Click_1(this, new EventArgs()); return;
                }
                balanceLabel.Text = balanceSim.ToString("0.00000000");
                Task.Run(() => SimulateRun());
            }
        }

        private void wltLabel_Click(object sender, EventArgs e) { }
        private void NonceStopBox_TextChanged(object sender, EventArgs e)
        { stopNonce = Int32.Parse(NonceStopBox.Text); Properties.Settings.Default.nonceStop = stopNonce; }
        private void listBox3_Click(object sender, EventArgs e) { }
        private void listBox3_SelectedIndexChanged(object sender, EventArgs e) { }
        private void ResetBoxSeed_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) { listBox3.Items.Clear(); }

        private void RegisterSim()
        {
            lua.RegisterFunction("vault",      this, new dVaultEmpty(EmptyVaultFunc).Method);
            lua.RegisterFunction("tip",        this, new dTipEmpty(EmptyTipFunc).Method);
            lua.RegisterFunction("print",      this, new LogConsole(luaPrint).Method);
            lua.RegisterFunction("stop",       this, new dStop(luaStop).Method);
            lua.RegisterFunction("resetseed",  this, new dSeedEmpty(EmptySeedFunc).Method);
            lua.RegisterFunction("resetstats", this, new dResetStat(luaResetStat).Method);
        }

        public void EmptySeedFunc()  { this.Invoke((MethodInvoker)delegate () { luaPrint("Function not available in simulation. (resetseed)"); }); }
        public void EmptyVaultFunc(decimal amount) { this.Invoke((MethodInvoker)delegate () { luaPrint("Function not available in simulation. (vault)"); }); }
        public void EmptyTipFunc(string user, decimal amount) { this.Invoke((MethodInvoker)delegate () { luaPrint("Function not available in simulation. (tip)"); }); }

        private void textBox2_TextChanged(object sender, EventArgs e)
        { ClearanceCookie = textBox2.Text; Properties.Settings.Default.cookie = ClearanceCookie; }
        private void textBox3_TextChanged(object sender, EventArgs e) { }
        private void textBox2_TextChanged_1(object sender, EventArgs e)
        { StakeSite = textBox2.Text.ToLower(); Properties.Settings.Default.indexSite = textBox2.Text; }
        private void textBox3_TextChanged_1(object sender, EventArgs e)
        {
            ClearanceCookie = textBox3.Text;
            Properties.Settings.Default.cookie = ClearanceCookie;
            UpdateStatusLabels();   // live-update cookie label as user types
        }
        private void textBox4_TextChanged(object sender, EventArgs e)
        { UserAgent = textBox4.Text; Properties.Settings.Default.agent = UserAgent; }
    }

    public static class ListViewExtensions
    {
        public static void SetDoubleBuffered(this System.Windows.Forms.ListView listView, bool doubleBuffered = true)
        {
            listView
                .GetType()
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(listView, doubleBuffered, null);
        }
    }
    public class lastbet
    {
        public double result { get; set; }
        public double target { get; set; }
    }
}
