using FastColoredTextBoxNS;
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
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocket4Net;

namespace Limbo
{
    public partial class Form1 : Form
    {
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
            "BTC",
            "ETH",
            "LTC",
            "DOGE",
            "BCH",
            "XRP",
            "TRX",
            "EOS",
            "BNB",
            "USDT",
            "APE",
            "BUSD",
            "CRO",
            "DAI",
            "LINK",
            "SAND",
            "SHIB",
            "UNI",
            "USDC"
       };


        private bool is_connected = false;

        private void fillCurrencies()
        {
            // fill currency combobox

            currencySelector.Items.Clear();

            foreach (var item in currenciesAvailable)
            {
                currencySelector.Items.Add(item);
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
            //this.listBox3.Click += this.listBox3_Click;

            richTextBox2.ReadOnly = true;
            richTextBox2.BackColor = Color.FromArgb(249, 249, 249);
            listView1.BackColor = Color.FromArgb(249, 249, 249);
            listBox3.BackColor = Color.FromArgb(249, 249, 249);
            //listView1.Hide();

            Text += " - " + Application.ProductVersion;
            Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);

            xList.Add(0);
            yList.Add(0);

            data.Add(new ObservablePoint
            {
                X = xList[counter],
                Y = yList[counter]
            });

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
                /*Separator = new LiveCharts.Wpf.Separator
                {
                    Step = 15
                }*/
            });

            Func<double, string> formatFunc = (x) => string.Format("{0:0.000000}", x);

            ch.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                LabelFormatter = formatFunc
            });

            //ch.Series[0].ScalesYAt = 0;
            ch.Width = 400;
            panel1.Controls.Add(ch);

            //RegisterLua();

            richTextBox1 = new FastColoredTextBox();
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Language = Language.Lua;
            richTextBox1.BorderStyle = BorderStyle.None;
            richTextBox1.BackColor = Color.FromArgb(249, 249, 249);
            tabPageLua.Controls.Add(richTextBox1);

            richTextBox1.TextChanged += this.richTextBox1_TextChanged;

            richTextBox1.Text = Properties.Settings.Default.textCode;

        }
        private void listBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control == true && e.KeyCode == Keys.C)
            {
                string tmpStr = "";
                foreach (var item in listBox3.SelectedItems)
                {
                    tmpStr += listBox3.GetItemText(item) + "\n";
                }
                Clipboard.SetData(DataFormats.StringFormat, tmpStr);
            }
            if (e.Control == true && e.KeyCode == Keys.A)
            {
                for (int i = 0; i < listBox3.Items.Count; i++)
                {
                    listBox3.SetSelected(i, true);
                }
            }
        }

        private void Application_ApplicationExit(object sender, EventArgs e)
        {

            Properties.Settings.Default.Save();

        }
        private void RegisterLua()
        {

            lua.RegisterFunction("vault", this, new dvault(luaVault).Method);
            lua.RegisterFunction("tip", this, new dtip(luatip).Method);
            lua.RegisterFunction("print", this, new LogConsole(luaPrint).Method);
            lua.RegisterFunction("stop", this, new dStop(luaStop).Method);
            lua.RegisterFunction("resetseed", this, new dResetSeed(luaResetSeed).Method);
            lua.RegisterFunction("resetstats", this, new dResetStat(luaResetStat).Method);
        }

        private void SetLuaVariables(decimal profitCurr)
        {
            lua["balance"] = currentBal;
            lua["profit"] = currentProfit;
            lua["currentstreak"] = (winstreak > 0) ? winstreak : -losestreak;
            lua["previousbet"] = Lastbet;
            lua["bets"] = wins + losses;
            lua["wins"] = wins;
            lua["losses"] = losses;
            lua["currency"] = currencySelected;
            lua["wagered"] = currentWager;
            lua["win"] = isWin;

            lua["lastBet"] = last;
            lua["currentprofit"] = profitCurr;
        }

        private void UnSetVariables()
        {
            lua["balance"] = null;
            lua["nextbet"] = null;
            lua["target"] = null;
        }

        private void GetLuaVariables()
        {
            try
            {
                Lastbet = (decimal)(double)lua["nextbet"];
                amount = Lastbet;
                currencySelected = (string)lua["currency"];
                target = (double)lua["target"];
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
            currencySelector.Enabled = true;
            button1.Text = "Start";
        }

        public void Log(Data response)
        {
            string[] row = { response.data.limboBet.id, String.Format("{0}x|{1}", response.data.limboBet.payoutMultiplier.ToString("0.00"), response.data.limboBet.state.result.ToString("0.0000")), response.data.limboBet.amount.ToString("0.00000000") + " " + currencySelected, (response.data.limboBet.payout - response.data.limboBet.amount).ToString("0.00000000"), response.data.limboBet.game };
            var log = new ListViewItem(row);
            listView1.Items.Insert(0, log);
            if (listView1.Items.Count > 15)
            {
                listView1.Items[listView1.Items.Count - 1].Remove();
            }
            if (response.data.limboBet.payoutMultiplier > 0)
            {
                log.BackColor = Color.FromArgb(170, 250, 190);
            }
            else
            {
                log.BackColor = Color.FromArgb(250, 185, 170);
            }
        }

        private void SetStatistics()
        {
            balanceLabel.Text = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);
            profitLabel.Text = currentProfit.ToString("0.00000000");
            wagerLabel.Text = currentWager.ToString("0.00000000");
            wltLabel.Text = String.Format("{0} / {1} / {2}", wins.ToString(), losses.ToString(), (wins + losses).ToString());
            currentStreakLabel.Text = String.Format("{0} / {1} / {2}", (winstreak > 0) ? winstreak.ToString() : (-losestreak).ToString(), highestStreak.Max().ToString(), lowestStreak.Min().ToString());
            lowestProfitLabel.Text = lowestProfit.Min().ToString("0.00000000");
            highestProfitLabel.Text = highestProfit.Max().ToString("0.00000000");
            highestBetLabel.Text = highestBet.Max().ToString("0.00000000");
        }
        void luaStop()
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                luaPrint("Called stop.");
                running = false;
                sim = false;
                bSta();
            });

        }
        void luaVault(decimal sentamount)
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                VaultSend(sentamount);
            });
        }
        void luatip(string user, decimal amount)
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                luaPrint("Tipping not available.");

            });
        }
        void luaPrint(string text)
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                richTextBox2.AppendText(text + "\r\n");
            });

        }
        void luaResetSeed()
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                ResetSeeds();
            });
        }

        void luaResetStat()
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                currentProfit = 0;
                currentWager = 0;
                wins = 0;
                losses = 0;
                winstreak = 0;
                losestreak = 0;
                lowestStreak = new List<int> { 0 };
                highestStreak = new List<int> { 0 };
                highestProfit = new List<decimal> { 0 };
                lowestProfit = new List<decimal> { 0 };
                highestBet = new List<decimal> { 0 };
                wltLabel.Text = "0 / 0 / 0";
                currentStreakLabel.Text = "0 / 0 / 0";
                profitLabel.Text = currentProfit.ToString("0.00000000");
                wagerLabel.Text = currentWager.ToString("0.00000000");
                wltLabel.Text = String.Format("{0} / {1} / {2}", wins.ToString(), losses.ToString(), (wins + losses).ToString());
                currentStreakLabel.Text = String.Format("{0} / {1} / {2}", (winstreak > 0) ? winstreak.ToString() : (-losestreak).ToString(), highestStreak.Max().ToString(), lowestStreak.Min().ToString());
                lowestProfitLabel.Text = lowestProfit.Min().ToString("0.00000000");
                highestProfitLabel.Text = highestProfit.Max().ToString("0.00000000");
                highestBetLabel.Text = highestBet.Max().ToString("0.00000000");
            });
        }





        private void LogButton_Click(object sender, EventArgs e)
        {
            if (LogButton.Text.Contains(">"))
            {

                LogButton.Text = "Log <";
                listLogs.Add(listView1);
                //tabControl2.Hide();
                listView1.Show();
            }
            else
            {
                //listView1.Hide();
                //tabControl2.Show();
                listLogs.Clear();
                LogButton.Text = "Log >";
            }
        }

        private async void textBox1_TextChanged(object sender, EventArgs e)
        {
            token = apiKeyInput.Text;
            Properties.Settings.Default.token = token;
        
        }

        private void currencyComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            currencySelected = currenciesAvailable[currencySelector.SelectedIndex].ToLower();
            Properties.Settings.Default.indexCurrency = currencySelector.SelectedIndex;
            string[] current = currencySelector.Text.Split(' ');
            if (current.Length > 1)
            {
                balanceLabel.Text = current[1] + " " + currencySelected;
            }
        }

        private void SiteComboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            StakeSite = mirrorSiteSelector.Text.ToLower();
            Properties.Settings.Default.indexSite = mirrorSiteSelector.SelectedIndex;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (running == false)
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
                    luaPrint("Lua ERROR!!");
                    luaPrint(ex.Message);
                    running = false;
                    bSta();
                }
                GetLuaVariables();

                currencySelector.SelectedIndex = Array.FindIndex(currenciesAvailable, row => row == currencySelected.ToUpper());
                if (ready == true)
                {
                    button1.Enabled = false;
                    running = true;
                    button1.Text = "Stop";
                    currencySelector.Enabled = false;
                    beginMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    StartBet();
                }
                else
                {
                    bSta();
                }


            }
            else
            {
                running = false;
                bSta();
            }
        }
        async Task StartBet()
        {
            while (running == true)
            {
                if (beginMs == 0)
                {
                    beginMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                }
                await LimboBet();
            }
        }
        private async Task Balances()
        {

            messagePayload messagePayload2 = new messagePayload();
            messagePayload2.accessToken = token;
            messagePayload2.query = "subscription AvailableBalances {\n  availableBalances {\n    amount\n    identifier\n    balance {\n      amount\n      currency\n    }\n  }\n}\n";
            messageData messageData2 = new messageData();
            messageData2.id = "6cc429c1-a18a-4a6a-819e-1c78c724b5f8";
            messageData2.type = "subscribe";
            messageData2.payload = messagePayload2;
            this.chat_socket.Send(JsonConvert.SerializeObject(messageData2));

        }
        public void Connect()
        {
            try
            {
                Debug.WriteLine(StakeSite);
                this.chat_socket = new WebSocket("wss://api." + StakeSite + "/websockets", "graphql-transport-ws", new List<KeyValuePair<string, string>>()
                {
                     new KeyValuePair<string, string>("jwt", token)
                }, userAgent: "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_14_6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.163 Safari/537.36", origin: "https://" + StakeSite, version: WebSocketVersion.Rfc6455, sslProtocols: SslProtocols.Tls12);
                this.chat_socket.EnableAutoSendPing = true;
                this.chat_socket.AutoSendPingInterval = 1000;
                //this.lastmessage = DateTime.UtcNow;
                this.chat_socket.MessageReceived += new EventHandler<MessageReceivedEventArgs>(this.chat_socket_MessageReceived);
                this.chat_socket.Opened += new EventHandler(this.chat_socket_Opened);
                this.chat_socket.Error += new EventHandler<ErrorEventArgs>(this.chat_socket_Error);
                this.chat_socket.Closed += new EventHandler(this.chat_socket_Closed);
                this.chat_socket.Open();
            }
            catch (Exception ex)
            {
                //Bsta();
                Debug.WriteLine(ex.ToString());

            }


        }

        private async void chat_socket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            messageData _msg = JsonConvert.DeserializeObject<messageData>(e.Message);
            string type = _msg.type;

            if (type == "connection_ack")
            {

                is_connected = true;

                await Balances();
            }
            else
            {

                if (type == "next")
                {
                    if (_msg.payload.errors.Count > 0)
                    {
                        if (_msg.payload.errors[0].message.Contains("invalid") || _msg.payload.errors[0].message.Contains("expired"))
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                this.chat_socket.Close();
                            });
                        }
                    }
                    else
                    {
                        if (_msg.payload.data.availableBalances != null)
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                if (_msg.payload.data.availableBalances.balance.currency == currencySelected.ToLower())
                                {
                                    currentBal = _msg.payload.data.availableBalances.balance.amount;

                                    try
                                    {
                                        lua["balance"] = currentBal;
                                        LuaRuntime.SetLua(lua);
                                    }
                                    catch (Exception ex)
                                    {
                                        luaPrint("Lua ERROR!!");
                                        luaPrint(ex.Message);
                                        running = false;
                                        bSta();
                                    }
                                    balanceLabel.Text = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);

                                }

                                var index = Array.FindIndex(currenciesAvailable, row => row.Contains(_msg.payload.data.availableBalances.balance.currency.ToUpper()));
                                currencySelector.Items[index] = string.Format("{0} {1}", currenciesAvailable[index], _msg.payload.data.availableBalances.balance.amount.ToString("0.00000000"));





                            });

                        }
                    }


                }
                else
                {

                }

            }
        }

        private void chat_socket_Opened(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel1.Text = string.Format("{0}", "Connected");
                this.chat_socket.Send(JsonConvert.SerializeObject(new messageData()
                {
                    type = "connection_init",
                    payload = new messagePayload()
                    {
                        accessToken = token,
                        language = "en"
                    }
                }));

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void chat_socket_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                //Bsta();
                Debug.WriteLine(e.Exception);
            }
            catch (Exception ex)
            {
                //Bsta();
                Debug.WriteLine(ex.Message);
            }
        }

        private async void chat_socket_Closed(object sender, EventArgs e)
        {
            try
            {


                if (!this.is_connected)
                    return;
                //this._from_chat_close = true;
                await Task.Delay(400);
                this.Invoke((MethodInvoker)delegate ()
                {
                    toolStripStatusLabel1.Text = string.Format("{0}", "Re-connecting...");
                });
                await Task.Delay(1000);
                this.chat_socket.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }


        async Task LimboBet()
        {
            try
            {
                if (running)
                {
                    var mainurl = "https://api." + StakeSite + "/graphql";
                    var request = new RestRequest(Method.POST);
                    var client = new RestClient(mainurl);
                    BetQuery payload = new BetQuery();
                    payload.variables = new BetClass()
                    {
                        currency = currencySelected,
                        amount = amount,
                        multiplierTarget = target,
                        identifier = RandomString(21)

                    };

                    payload.query = "mutation LimboBet($amount: Float!, $multiplierTarget: Float!, $currency: CurrencyEnum!, $identifier: String!) {\n limboBet(\n amount: $amount\n currency: $currency\n multiplierTarget: $multiplierTarget\n identifier: $identifier\n  ) {\n...CasinoBet\n state {\n...CasinoGameLimbo\n    }\n  }\n}\n\nfragment CasinoBet on CasinoBet {\n id\n active\n payoutMultiplier\n amountMultiplier\n amount\n payout\n updatedAt\n currency\n game\n user {\n id\n name\n  }\n}\n\nfragment CasinoGameLimbo on CasinoGameLimbo {\n result\n multiplierTarget\n}\n";

                    request.AddHeader("Content-Type", "application/json");
                    request.AddHeader("x-access-token", token);

                    request.AddParameter("application/json", JsonConvert.SerializeObject(payload), ParameterType.RequestBody);


                    var restResponse =
                        await client.ExecuteAsync(request);


                    button1.Enabled = true;
                    Data response = JsonConvert.DeserializeObject<Data>(restResponse.Content);

                    if (response.errors != null)
                    {
                        luaPrint(String.Format("{0}:{1}", response.errors[0].errorType, response.errors[0].message));

                        //if(response.errors[0].errorType == "graphQL")

                        if (running == true)
                        {
                            await Task.Delay(2000);

                        }
                        else
                        {
                            running = false;
                            bSta();
                        }
                    }
                    else
                    {
                        TimerFunc(beginMs);

                        currentWager += response.data.limboBet.amount;
                        if (response.data.limboBet.payoutMultiplier > 0)
                        {
                            losestreak = 0;
                            winstreak++;
                            isWin = true;
                            wins++;
                            ResultLabeL.ForeColor = Color.LimeGreen;
                        }
                        else
                        {
                            losestreak++;
                            winstreak = 0;
                            isWin = false;
                            losses++;
                            ResultLabeL.ForeColor = Color.Red;

                        }

                        Log(response);
                        //CheckBalance();

                        decimal profitCurr = response.data.limboBet.payout - response.data.limboBet.amount;
                        currentProfit += response.data.limboBet.payout - response.data.limboBet.amount;
                        //profitLabel.Text = currentProfit.ToString("0.00000000");
                        TargetLabeL.Text = response.data.limboBet.state.multiplierTarget.ToString("0.00") + "x";
                        ResultLabeL.Text = response.data.limboBet.state.result.ToString("0.00") + "x";

                        last.target = response.data.limboBet.state.multiplierTarget;
                        last.result = response.data.limboBet.state.result;


                        highestStreak.Add(winstreak);
                        highestStreak = new List<int> { highestStreak.Max() };
                        lowestStreak.Add(-losestreak);
                        lowestStreak = new List<int> { lowestStreak.Min() };

                        if (currentProfit < 0)
                        {
                            lowestProfit.Add(currentProfit);
                            lowestProfit = new List<decimal> { lowestProfit.Min() };
                        }
                        else
                        {
                            highestProfit.Add(currentProfit);
                            highestProfit = new List<decimal> { highestProfit.Max() };
                        }

                        highestBet.Add(amount);
                        highestBet = new List<decimal> { highestBet.Max() };

                        SetStatistics();


                        counter++;
                        xList.Add(counter);
                        yList.Add((double)currentProfit);



                        data.Add(new ObservablePoint
                        {
                            X = xList[xList.Count - 1],
                            Y = yList[yList.Count - 1]
                        });


                        if (data.Count > 50)
                        {
                            data.RemoveAt(0);
                            xList.RemoveAt(0);
                            yList.RemoveAt(0);

                        }

                        try
                        {
                            SetLuaVariables(profitCurr);
                            LuaRuntime.SetLua(lua);


                            LuaRuntime.Run("dobet()");

                        }
                        catch (Exception ex)
                        {
                            luaPrint("Lua ERROR!!");
                            luaPrint(ex.Message);
                            running = false;
                            bSta();
                        }
                        GetLuaVariables();
                    }
                }
            }
            catch (Exception ex)
            {
                //luaPrint(ex.Message);
            }
        }


        public async Task CheckBalance()
        {
            try
            {
                var mainurl = "https://api." + StakeSite + "/graphql";
                var request = new RestRequest(Method.POST);
                var client = new RestClient(mainurl);
                BetQuery payload = new BetQuery();
                payload.operationName = "UserBalances";
                payload.query = "query UserBalances {\n  user {\n    id\n    balances {\n      available {\n        amount\n        currency\n        __typename\n      }\n      vault {\n        amount\n        currency\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n}\n";

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("x-access-token", token);

                request.AddParameter("application/json", JsonConvert.SerializeObject(payload), ParameterType.RequestBody);



                var restResponse =
                    await client.ExecuteAsync(request);


                //Debug.WriteLine(restResponse.Content);
                BalancesData response = JsonConvert.DeserializeObject<BalancesData>(restResponse.Content);


                if (response.errors != null)
                {

                }
                else
                {
                    if (response.data != null)
                    {

                        for (var i = 0; i < response.data.user.balances.Count; i++)
                        {
                            if (response.data.user.balances[i].available.currency == currencySelected.ToLower())
                            {
                                currentBal = response.data.user.balances[i].available.amount;
                                balanceLabel.Text = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);
                            }
                            if (true)
                            {
                                for (int s = 0; s < currenciesAvailable.Length; s++)
                                {
                                    if (response.data.user.balances[i].available.currency == currenciesAvailable[s].ToLower())
                                    {
                                        currencySelector.Items[s] = string.Format("{0} {1}", currenciesAvailable[s], response.data.user.balances[i].available.amount.ToString("0.00000000"));

                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //luaPrint(ex.Message);
            }

        }
        public void TimerFunc(long begin)
        {
            decimal diff = (decimal)((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - begin);
            decimal seconds = Math.Floor((diff / 1000) % 60);
            decimal minutes = Math.Floor((diff / (1000 * 60)) % 60);
            decimal hours = Math.Floor((diff / (1000 * 60 * 60)));

            Time.Text = String.Format("{0} : {1} : {2}", hours, minutes, seconds);
        }
        private void clearLinkbtn_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            apiKeyInput.Clear();
            apiKeyInput.Enabled = true;
            token = "";
            toolStripStatusLabel1.Text = "Unauthorized";
        }

        private async void CheckBtn_Click(object sender, EventArgs e)
        {
            btnCheckBalance.Enabled = false;
            await CheckBalance();
            btnCheckBalance.Enabled = true;
        }

        public string RandomString(int length)
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.textCode = richTextBox1.Text;
        }
        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {
            if (richTextBox2.Lines.Length > 200)
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
            currentProfit = 0;
            currentWager = 0;
            wins = 0;
            losses = 0;
            winstreak = 0;
            losestreak = 0;
            lowestStreak = new List<int> { 0 };
            highestStreak = new List<int> { 0 };
            highestProfit = new List<decimal> { 0 };
            lowestProfit = new List<decimal> { 0 };
            highestBet = new List<decimal> { 0 };
            beginMs = 0;
            Time.Text = "0 : 0 : 0";
            wltLabel.Text = "0 / 0 / 0";
            currentStreakLabel.Text = "0 / 0 / 0";
            counter = 0;
            yList.Clear();
            xList.Clear();
            xList.Add(0);
            yList.Add(0);
            data.Clear();
            profitLabel.Text = currentProfit.ToString("0.00000000");
            wagerLabel.Text = currentWager.ToString("0.00000000");
            wltLabel.Text = String.Format("{0} / {1} / {2}", wins.ToString(), losses.ToString(), (wins + losses).ToString());
            currentStreakLabel.Text = String.Format("{0} / {1} / {2}", (winstreak > 0) ? winstreak.ToString() : (-losestreak).ToString(), highestStreak.Max().ToString(), lowestStreak.Min().ToString());
            lowestProfitLabel.Text = lowestProfit.Min().ToString("0.00000000");
            highestProfitLabel.Text = highestProfit.Max().ToString("0.00000000");
            highestBetLabel.Text = highestBet.Max().ToString("0.00000000");
        }

        private void CommandButton2_Click(object sender, EventArgs e)
        {
            try
            {
                if (CommandBox2.Text.Length > 0)
                {
                    LuaRuntime.Run(CommandBox2.Text);
                }
            }
            catch (Exception ex)
            {
                luaPrint("Lua ERROR!!");
                luaPrint(ex.Message);
                running = false;
                bSta();
            }
        }



        private void listView1_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            //
            ListViewItem item = e.Item as ListViewItem;
            if (e.Item.Checked == true)
            {
                Process.Start(new ProcessStartInfo(string.Format("https://{1}/casino/home?betId={0}&modal=bet", e.Item.Text, StakeSite)) { UseShellExecute = true });
            }
        }

        private void CmdBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommandButton2_Click(this, new EventArgs());
            }
        }
        private async Task VaultSend(decimal sentamount)
        {
            try
            {
                var mainurl = "https://api." + StakeSite + "/graphql";
                var request = new RestRequest(Method.POST);
                var client = new RestClient(mainurl);
                BetQuery payload = new BetQuery();
                payload.operationName = "CreateVaultDeposit";
                payload.variables = new BetClass()
                {
                    currency = currencySelected.ToLower(),
                    amount = sentamount
                };
                payload.query = "mutation CreateVaultDeposit($currency: CurrencyEnum!, $amount: Float!) {\n  createVaultDeposit(currency: $currency, amount: $amount) {\n    id\n    amount\n    currency\n    user {\n      id\n      balances {\n        available {\n          amount\n          currency\n          __typename\n        }\n        vault {\n          amount\n          currency\n          __typename\n        }\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n}\n";
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("x-access-token", token);

                request.AddParameter("application/json", JsonConvert.SerializeObject(payload), ParameterType.RequestBody);
                //request.AddJsonBody(payload);
                //IRestResponse response = client.Execute(request);

                var restResponse =
                    await client.ExecuteAsync(request);

                // Will output the HTML contents of the requested page
                //Debug.WriteLine(restResponse.Content);
                Data response = JsonConvert.DeserializeObject<Data>(restResponse.Content);
                //System.Diagnostics.Debug.WriteLine(restResponse.Content);
                if (response.errors != null)
                {
                    luaPrint(response.errors[0].errorType + ":" + response.errors[0].message);
                }
                else
                {
                    if (response.data != null)
                    {
                        luaPrint(string.Format("Deposited to vault: {0} {1}", sentamount.ToString("0.00000000"), currencySelected));
                    }

                }
            }
            catch (Exception ex)
            {
                //luaPrint(ex.Message);
            }
        }

        private async Task ResetSeeds()
        {
            try
            {
                var mainurl = "https://api." + StakeSite + "/graphql";
                var request = new RestRequest(Method.POST);
                var client = new RestClient(mainurl);
                BetQuery payload = new BetQuery();
                payload.operationName = "RotateSeedPair";
                payload.variables = new BetClass()
                {
                    seed = RandomString(10)
                };
                payload.query = "mutation RotateSeedPair($seed: String!) {\n  rotateSeedPair(seed: $seed) {\n    clientSeed {\n      user {\n        id\n        activeClientSeed {\n          id\n          seed\n          __typename\n        }\n        activeServerSeed {\n          id\n          nonce\n          seedHash\n          nextSeedHash\n          __typename\n        }\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n}\n";
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("x-access-token", token);

                request.AddParameter("application/json", JsonConvert.SerializeObject(payload), ParameterType.RequestBody);
                //request.AddJsonBody(payload);
                //IRestResponse response = client.Execute(request);

                var restResponse =
                    await client.ExecuteAsync(request);

                // Will output the HTML contents of the requested page
                //Debug.WriteLine(restResponse.Content);
                Data response = JsonConvert.DeserializeObject<Data>(restResponse.Content);
                //System.Diagnostics.Debug.WriteLine(restResponse.Content);
                if (response.errors != null)
                {
                    luaPrint(response.errors[0].errorType + ":" + response.errors[0].message);
                }
                else
                {
                    if (response.data != null)
                    {
                        luaPrint("Seed was reset.");

                    }

                }
            }
            catch (Exception ex)
            {
                //luaPrint(ex.Message);
            }
        }

        private async Task SendTip()
        {
            try
            {
                var mainurl = "https://api." + StakeSite + "/graphql";
                var request = new RestRequest(Method.POST);
                var client = new RestClient(mainurl);
                BetQuery payload = new BetQuery();
                payload.operationName = "RotateSeedPair";
                payload.variables = new BetClass()
                {
                    seed = RandomString(10)
                };
                payload.query = "mutation RotateSeedPair($seed: String!) {\n  rotateSeedPair(seed: $seed) {\n    clientSeed {\n      user {\n        id\n        activeClientSeed {\n          id\n          seed\n          __typename\n        }\n        activeServerSeed {\n          id\n          nonce\n          seedHash\n          nextSeedHash\n          __typename\n        }\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n}\n";
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("x-access-token", token);

                request.AddParameter("application/json", JsonConvert.SerializeObject(payload), ParameterType.RequestBody);
                //request.AddJsonBody(payload);
                //IRestResponse response = client.Execute(request);

                var restResponse =
                    await client.ExecuteAsync(request);

                // Will output the HTML contents of the requested page
                //Debug.WriteLine(restResponse.Content);
                Data response = JsonConvert.DeserializeObject<Data>(restResponse.Content);
                //System.Diagnostics.Debug.WriteLine(restResponse.Content);
                if (response.errors != null)
                {
                    luaPrint(response.errors[0].errorType + ":" + response.errors[0].message);
                }
                else
                {
                    if (response.data != null)
                    {
                        luaPrint("Not functional.");

                    }

                }
            }
            catch (Exception ex)
            {
                //luaPrint(ex.Message);
            }
        }

        private async Task Authorize()
        {
            try
            {
                var mainurl = "https://api." + StakeSite + "/graphql";
                var request = new RestRequest(Method.POST);
                var client = new RestClient(mainurl);
                BetQuery payload = new BetQuery();
                payload.operationName = "initialUserRequest";
                payload.variables = new BetClass() { };
                payload.query = "query initialUserRequest {\n  user {\n    ...UserAuth\n    __typename\n  }\n}\n\nfragment UserAuth on User {\n  id\n  name\n  email\n  hasPhoneNumberVerified\n  hasEmailVerified\n  hasPassword\n  intercomHash\n  createdAt\n  hasTfaEnabled\n  mixpanelId\n  hasOauth\n  isKycBasicRequired\n  isKycExtendedRequired\n  isKycFullRequired\n  kycBasic {\n    id\n    status\n    __typename\n  }\n  kycExtended {\n    id\n    status\n    __typename\n  }\n  kycFull {\n    id\n    status\n    __typename\n  }\n  flags {\n    flag\n    __typename\n  }\n  roles {\n    name\n    __typename\n  }\n  balances {\n    ...UserBalanceFragment\n    __typename\n  }\n  activeClientSeed {\n    id\n    seed\n    __typename\n  }\n  previousServerSeed {\n    id\n    seed\n    __typename\n  }\n  activeServerSeed {\n    id\n    seedHash\n    nextSeedHash\n    nonce\n    blocked\n    __typename\n  }\n  __typename\n}\n\nfragment UserBalanceFragment on UserBalance {\n  available {\n    amount\n    currency\n    __typename\n  }\n  vault {\n    amount\n    currency\n    __typename\n  }\n  __typename\n}\n";
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("x-access-token", token);

                request.AddParameter("application/json", JsonConvert.SerializeObject(payload), ParameterType.RequestBody);
                //request.AddJsonBody(payload);
                //IRestResponse response = client.Execute(request);

                var restResponse =
                    await client.ExecuteAsync(request);

                // Will output the HTML contents of the requested page
                //Debug.WriteLine(restResponse.Content);
                ActiveData response = JsonConvert.DeserializeObject<ActiveData>(restResponse.Content);
                //System.Diagnostics.Debug.WriteLine(restResponse.Content);
                if (response == null || response.errors != null)
                {
                    toolStripStatusLabel1.Text = "Unauthorized";
                }
                else
                {
                    if (response.data != null)
                    {
                        toolStripStatusLabel1.Text = String.Format("Authorized.");
                        apiKeyInput.Enabled = false;
                        for (var i = 0; i < response.data.user.balances.Count; i++)
                        {
                            if (response.data.user.balances[i].available.currency == currencySelected.ToLower())
                            {
                                currentBal = response.data.user.balances[i].available.amount;
                                balanceLabel.Text = String.Format("{0} {1}", currentBal.ToString("0.00000000"), currencySelected);

                            }
                            //currencySelect.Items.Clear();
                            if (true)
                            {
                                for (int s = 0; s < currenciesAvailable.Length; s++)
                                {
                                    if (response.data.user.balances[i].available.currency == currenciesAvailable[s].ToLower())
                                    {
                                        currencySelector.Items[s] = string.Format("{0} {1}", currenciesAvailable[s], response.data.user.balances[i].available.amount.ToString("0.00000000"));
                                        //currencySelect.Items.Add(string.Format("{0} {1}", s, response.data.user.balances[i].available.amount.ToString("0.00000000")));
                                        break;
                                    }
                                }
                            }
                        }

                    }

                }
            }
            catch (Exception ex)
            {
                //luaPrint(ex.Message);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            apiKeyInput.Text = Properties.Settings.Default.token;
            currencySelector.SelectedIndex = Properties.Settings.Default.indexCurrency;
            mirrorSiteSelector.SelectedIndex = Properties.Settings.Default.indexSite;
            ServerSeedBox.Text = Properties.Settings.Default.serverSeed;
            ClientSeedBox.Text = Properties.Settings.Default.clientSeed;
            NonceBox.Text = Properties.Settings.Default.nonce.ToString();
            NonceStopBox.Text = Properties.Settings.Default.nonceStop.ToString();
        }



        private void SimulateRun()
        {
            while (sim == true)
            {
                if (nonce > stopNonce || balanceSim < amount || target <= 1)
                {
                    if (target <= 1)
                    {
                        luaPrint("Lua ERROR!!");
                        luaPrint("Lua: Target must be above 1.");
                    }
                    SimulateButton_Click_1(this, new EventArgs());
                    sim = false;
                    break;
                }
                decimal result = LimboResult(serverSeed, clientSeed, nonce);
                nonce += 1;

                decimal payout = 0;
                decimal payoutMultiplier = 0;
                currentWager += amount;
                string winStatus = "lose";
                if (result > (decimal)target)
                {
                    losestreak = 0;
                    winstreak++;
                    isWin = true;
                    wins++;
                    payout = (decimal)target * amount;
                    payoutMultiplier = (decimal)target;
                    winStatus = "win";
                    ResultLabeL.ForeColor = Color.LimeGreen;
                }
                else
                {
                    losestreak++;
                    winstreak = 0;
                    isWin = false;
                    losses++;
                    ResultLabeL.ForeColor = Color.Red;
                }

                decimal profitCurr = payout - amount;
                currentProfit += payout - amount;
                balanceSim += payout - amount;
                //profitLabel.Text = currentProfit.ToString("0.00000000");
                TargetLabeL.Text = target.ToString("0.00") + "x";
                ResultLabeL.Text = result.ToString("0.00") + "x";

                last.target = target;
                last.result = (double)result;


                highestStreak.Add(winstreak);
                highestStreak = new List<int> { highestStreak.Max() };
                lowestStreak.Add(-losestreak);
                lowestStreak = new List<int> { lowestStreak.Min() };

                if (currentProfit < 0)
                {
                    lowestProfit.Add(currentProfit);
                    lowestProfit = new List<decimal> { lowestProfit.Min() };
                }
                else
                {
                    highestProfit.Add(currentProfit);
                    highestProfit = new List<decimal> { highestProfit.Max() };
                }

                highestBet.Add(amount);
                highestBet = new List<decimal> { highestBet.Max() };
                this.Invoke((MethodInvoker)delegate ()
                {
                    balanceLabel.Text = String.Format("{0}", balanceSim.ToString("0.00000000"));
                    profitLabel.Text = currentProfit.ToString("0.00000000");
                    wagerLabel.Text = currentWager.ToString("0.00000000");
                    wltLabel.Text = String.Format("{0} / {1} / {2}", wins.ToString(), losses.ToString(), (wins + losses).ToString());
                    currentStreakLabel.Text = String.Format("{0} / {1} / {2}", (winstreak > 0) ? winstreak.ToString() : (-losestreak).ToString(), highestStreak.Max().ToString(), lowestStreak.Min().ToString());
                    lowestProfitLabel.Text = lowestProfit.Min().ToString("0.00000000");
                    highestProfitLabel.Text = highestProfit.Max().ToString("0.00000000");
                    highestBetLabel.Text = highestBet.Max().ToString("0.00000000");
                    Application.DoEvents();
                });
                //SetStatistics();
                string box = String.Format("[{0}] {4}x  |  {1}   |  bet: {5}  |  profit:  {2}   [{3}]", nonce - 1, result.ToString("0.0000"), currentProfit.ToString("0.00000000"), winStatus, target.ToString("0.00"), amount.ToString("0.00000000"));
                listBox3.Items.Insert(0, box);
                if (listBox3.Items.Count > 200)
                {
                    listBox3.Items.RemoveAt(listBox3.Items.Count - 1);
                }
                try
                {
                    lua["balance"] = balanceSim;
                    lua["profit"] = currentProfit;
                    lua["currentstreak"] = (winstreak > 0) ? winstreak : -losestreak;
                    lua["previousbet"] = Lastbet;
                    lua["nextbet"] = Lastbet;
                    lua["bets"] = wins + losses;
                    lua["wins"] = wins;
                    lua["losses"] = losses;
                    lua["currency"] = currencySelected;
                    lua["wagered"] = currentWager;
                    lua["win"] = isWin;

                    lua["lastBet"] = last;
                    lua["currentprofit"] = profitCurr;
                    LuaRuntime.SetLua(lua);


                    LuaRuntime.Run("dobet()");

                }
                catch (Exception ex)
                {
                    luaPrint("Lua ERROR!!");
                    luaPrint(ex.Message);
                    sim = false;
                }
                Lastbet = (decimal)(double)lua["nextbet"];
                amount = Lastbet;
                currencySelected = (string)lua["currency"];
                target = (double)lua["target"];

            }
        }
        static decimal LimboResult(string serverSeed, string clientSeed, int nonce)
        {
            string nonceSeed = string.Format("{0}:{1}:{2}", clientSeed, nonce, 0);

            string hex = HmacSha256Digest(nonceSeed, serverSeed);
            decimal end = 0;
            for (int i = 0; i < 4; i++)
            {
                end += (decimal)(Convert.ToInt32(hex.Substring(i * 2, 2), 16) / Math.Pow(256, i + 1));
            }
            end *= 16777216;
            end = 16777216 / (Math.Floor(end) + 1) * (decimal)(1 - 0.01);
            return end;
        }

        public static string HmacSha256Digest(string message, string secret)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] keyBytes = encoding.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            System.Security.Cryptography.HMACSHA256 cryptographer = new System.Security.Cryptography.HMACSHA256(keyBytes);

            byte[] bytes = cryptographer.ComputeHash(messageBytes);

            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private void ServerSeedBox_TextChanged(object sender, EventArgs e)
        {
            serverSeed = ServerSeedBox.Text;
            Properties.Settings.Default.serverSeed = serverSeed;
        }

        private void ClientSeedBox_TextChanged(object sender, EventArgs e)
        {
            clientSeed = ClientSeedBox.Text;
            Properties.Settings.Default.clientSeed = clientSeed;
        }

        private void NonceBox_TextChanged(object sender, EventArgs e)
        {
            nonce = Int32.Parse(NonceBox.Text);
            Properties.Settings.Default.nonce = nonce;
        }

        private void SimulateButton_Click_1(object sender, EventArgs e)
        {
            if (SimulateButton.Text.Contains("Off"))
            {
                SimulateButton.Text = "Simulate";
                sim = false;
            }
            else
            {
                nonce = Int32.Parse(NonceBox.Text);
                serverSeed = ServerSeedBox.Text;
                clientSeed = ClientSeedBox.Text;
                linkLabel1_LinkClicked(this, new LinkLabelLinkClickedEventArgs(new LinkLabel.Link()));
                SimulateButton.Text = "Off";
                sim = true;
                RegisterSim();
                lua["balance"] = null;
                lua["nextbet"] = null;
                lua["target"] = null;
                try
                {

                    lua["profit"] = currentProfit;
                    lua["currentstreak"] = (winstreak > 0) ? winstreak : -losestreak;
                    lua["previousbet"] = Lastbet;
                    lua["bets"] = wins + losses;
                    lua["wins"] = wins;
                    lua["losses"] = losses;
                    lua["currency"] = currencySelected;
                    lua["wagered"] = currentWager;
                    lua["win"] = isWin;
                    lua["lastBet"] = last;
                    LuaRuntime.SetLua(lua);


                    LuaRuntime.Run(richTextBox1.Text);


                }
                catch (Exception ex)
                {
                    luaPrint("Lua ERROR!!");
                    luaPrint(ex.Message);
                    sim = false;
                }


                try
                {
                    Lastbet = (decimal)(double)lua["nextbet"];
                    amount = Lastbet;
                    currencySelected = (string)lua["currency"];
                    target = (double)lua["target"];
                    balanceSim = (decimal)(double)lua["balance"];
                }
                catch (Exception ex)
                {
                    luaPrint("Please set 'balance = x' and 'target = x' and 'nextbet = x' variable on top of script.");
                    SimulateButton_Click_1(this, new EventArgs());
                    return;
                }

                balanceLabel.Text = String.Format("{0}", balanceSim.ToString("0.00000000"));
                Task.Run(() => SimulateRun());
            }
        }



        private void wltLabel_Click(object sender, EventArgs e)
        {

        }

        private void NonceStopBox_TextChanged(object sender, EventArgs e)
        {
            stopNonce = Int32.Parse(NonceStopBox.Text);
            Properties.Settings.Default.nonceStop = stopNonce;
        }
        private void listBox3_Click(object sender, EventArgs e)
        {
            //listBox3.ClearSelected();
        }
        private void listBox3_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void ResetBoxSeed_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            listBox3.Items.Clear();
        }

        private void RegisterSim()
        {

            lua.RegisterFunction("vault", this, new dVaultEmpty(EmptyVaultFunc).Method);
            lua.RegisterFunction("tip", this, new dTipEmpty(EmptyTipFunc).Method);
            lua.RegisterFunction("print", this, new LogConsole(luaPrint).Method);
            lua.RegisterFunction("stop", this, new dStop(luaStop).Method);
            lua.RegisterFunction("resetseed", this, new dSeedEmpty(EmptySeedFunc).Method);
            lua.RegisterFunction("resetstats", this, new dResetStat(luaResetStat).Method);
        }

        public void EmptySeedFunc()
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                luaPrint("Function not available in simulation. (resetseed)");

            });
        }
        public void EmptyVaultFunc(decimal amount)
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                luaPrint("Function not available in simulation. (vault)");

            });
        }
        public void EmptyTipFunc(string user, decimal amount)
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                luaPrint("Function not available in simulation. (tip)");

            });
        }

        private async void btnLoginLogout_Click(object sender, EventArgs e)
        {
            if (token.Length == 96)
            {
                Connect();
                await Authorize();
            }
            else
            {
                toolStripStatusLabel1.Text = "Unauthorized";
            }
        }


    }
    public class lastbet
    {
        public double result { get; set; }
        public double target { get; set; }
    }
}
