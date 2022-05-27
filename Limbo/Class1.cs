using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Limbo
{
    public class BetQuery
    {
        public string operationName { get; set; }
        public string query { get; set; }
        public BetClass variables { get; set; }
    }
    public class BetClass
    {
        public string identifier { get; set; }
        public decimal amount { get; set; }
        public decimal target { get; set; }
        public string currency { get; set; }
        public string game { get; set; }
        public string guess { get; set; }
        public int minesCount { get; set; }
        public List<int> fields { get; set; }
        public string seed { get; set; }
        public string risk { get; set; }
        public List<int> numbers { get; set; }
        public double multiplierTarget { get; set; }
    }
    public class Card
    {
        public string rank { get; set; }
        public string suit { get; set; }
    }
    public class Data
    {
        public availableBalances availableBalances { get; set; }
        public chatMessages chatMessages { get; set; }
        public crash crash { get; set; }
        public Betdata data { get; set; }
        public List<Errors> errors { get; set; }
    }
    public class Errors
    {
        public List<string> path { get; set; }
        public string message { get; set; }
        public string errorType { get; set; }
        public string data { get; set; }
    }

    public class ActiveData
    {
        public User data { get; set; }
        public List<Errors> errors { get; set; }
    }
    public class User
    {
        public Active user { get; set; }
    }
    public class Active
    {
        public string id { get; set; }
        public string name { get; set; }
        public limboBet activeCasinoBet { get; set; }
        public List<Balances> balances { get; set; }
    }
    public class Balances
    {
        public Available available { get; set; }
    }
    public class Available
    {
        public decimal amount { get; set; }
        public string currency { get; set; }
    }
    public class Betdata
    {
        public limboBet limboBet { get; set; }
        public limboBet minesNext { get; set; }
        public limboBet minesCashout { get; set; }
        public object rotateSeedPair { get; set; }
        public object createVaultDeposit { get; set; }
    }
    public class limboBet
    {
        public string id { get; set; }
        public string iid { get; set; }
        public double payoutMultiplier { get; set; }
        public decimal amount { get; set; }
        public decimal payout { get; set; }
        public string updatedAt { get; set; }
        public string currency { get; set; }
        public string game { get; set; }
        public Active user { get; set; }
        public State state { get; set; }
    }
    public class State
    {
        public List<int> drawnNumbers { get; set; }
        public List<int> selectedNumbers { get; set; }
        public double result { get; set; }
        public double multiplierTarget { get; set; }

    }
    public class Rounds
    {
        public int field { get; set; }
        public double payoutMultiplier { get; set; }

    }
    public class BalancesData
    {
        public User data { get; set; }
        public List<Errors> errors { get; set; }
    }
    public class messageData
    {
        public messageData() => this.payload = new messagePayload();

        public string id { get; set; }

        public string type { get; set; }

        public messagePayload payload { get; set; }
    }
    public class messagePayload
    {
        public messagePayload()
        {
            ///this.variables = new object();
            //this.extensions = new object();
            this.data = new Data();
            this.errors = new List<messageErrors>();
        }

        public string accessToken { get; set; }

        public string operationName { get; set; }

        public string key { get; set; }
        public string query { get; set; }

        public string language { get; set; }

        public string lockdownToken { get; set; }
        public BetClass variables { get; set; }

        public string requestPolicy { get; set; }
        public bool preferGetMethod { get; set; }
        public bool suspense { get; set; }
        public context context { get; set; }

        public Data data { get; set; }

        public List<messageErrors> errors { get; set; }
    }
    public class messageErrors
    {
        public messageErrors() => this.message = (string)null;

        public string[] path { get; set; }

        public string message { get; set; }
    }

    public class ChatInputs
    {
        public ChatInputs()
        {

        }
    }



    public class context
    {
        public string url { get; set; }
    }

 
    public class chatMessages
    {
        public string id { get; set; }
        public ChatData data { get; set; }
        public ChatUser user { get; set; }
    }

    public class ChatData
    {
        public string message { get; set; }

    }
    public class ChatUser
    {
        public string name { get; set; }

    }
    public class availableBalances
    {
        public decimal amount { get; set; }
        public Balance balance { get; set; }
    }

    public class Balance
    {
        public decimal amount { get; set; }
        public string currency { get; set; }
    }

    public class crash
    {
        public Event @event { get; set; }
    }

    public class Event
    {
        public string id { get; set; }
        public string status { get; set; }
        public object multiplier { get; set; }
        public string startTime { get; set; }
        public object nextRoundIn { get; set; }
        public int elapsed { get; set; }
        public string timestamp { get; set; }
    }
}
