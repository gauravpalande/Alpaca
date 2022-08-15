using Alpaca.Markets;
using Newtonsoft.Json;

namespace AlpacaExample
{
    internal static class Program
    {
        private const string KEY_ID = "";
        private const string SECRET_KEY = "";
        private const int percentStep = 7;

        public static async Task Main()
        {
            var symbols = new string[] { "XLY", "XLP", "XLE", "XLF", "XLV", "XLI", "GDX", "VNQ", "QQQ", "VOX", "XLU" };

            // Client calls
            var alpacaDataClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(KEY_ID, SECRET_KEY));
            var alpacaTradingClient = Environments.Paper.GetAlpacaTradingClient(new SecretKey(KEY_ID, SECRET_KEY));

            while (true)
            {
                Pause(10);

                foreach (var symbol in symbols)
                {
                    decimal symbolTenYearHigh = await CalculateSymbolTenYearhigh(alpacaDataClient, symbol);

                    var percentageLookup = GetPercentageLookup(symbolTenYearHigh, symbol);

                    Savelookup(percentageLookup);

                    await TradeETF(symbol, percentageLookup, alpacaDataClient, alpacaTradingClient, symbolTenYearHigh);

                    Savelookup(percentageLookup);
                }
            }
        }

        private static async Task TradeETF(string symbol, Dictionary<string, Dictionary<decimal, bool>> percentageLookup, 
            IAlpacaDataClient alpacaDataClient, IAlpacaTradingClient alpacaTradingClient, decimal symbolTenYearHigh)
        {
            decimal currentTradePrice = GetCurrentPrice(symbol, alpacaDataClient);

            // On Buy
            var buySteps = percentageLookup[symbol].Where(x => !x.Value && x.Key > currentTradePrice);

            foreach (var buy in buySteps)
            {
                Pause(1);
                Console.WriteLine($"Buying {symbol} Position: {buy.Key}, Price: {currentTradePrice}; Time: {DateTime.Now:F}");

                await alpacaTradingClient.PostOrderAsync(OrderSide.Buy.Market(symbol, OrderQuantity.FromInt64(1)));
                await alpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, OrderQuantity.FromInt64(1), OrderSide.Sell, OrderType.Limit,
                    TimeInForce.Gtc)
                {
                    LimitPrice = Math.Floor(buy.Key + (2 * symbolTenYearHigh / percentStep))
                });

                percentageLookup[symbol][buy.Key] = true;
            }

            // On Sell
            var sellSteps = percentageLookup[symbol].Where(x => x.Value && x.Key < currentTradePrice - (2 * symbolTenYearHigh / percentStep));

            foreach (var sell in sellSteps)
            {
                Console.WriteLine($"Selling {symbol} Position: {sell.Key}; Price: {currentTradePrice}; Time: {DateTime.Now:F}");

                percentageLookup[symbol][sell.Key] = false;

                Savelookup(percentageLookup);
            }
        }

        private static void Pause(int seconds)
        {
            Thread.Sleep(1000 * seconds);
        }

        private static decimal GetCurrentPrice( string symbol, IAlpacaDataClient alpacaDataClient)
        {
            var currentTradePrice = 0m;
            try
            {
                currentTradePrice = alpacaDataClient.GetLatestTradeAsync(new LatestMarketDataRequest(symbol)).Result.Price;
                //if (DateTime.Now.Minute % 5 == 0 && (DateTime.Now.Second < 10))
                //{
                    Console.WriteLine($"Current {symbol} Price: {currentTradePrice}; Time: {DateTime.Now:F}");
                //}
            }
            catch
            {
                Console.WriteLine($"Log: Error in getting cuurent {symbol} price occurred");
            }
            return currentTradePrice;
        }

        private static void Savelookup(Dictionary<string,Dictionary<decimal, bool>> symbolsPercentageLookup)
        {
            var jsonString = JsonConvert.SerializeObject(symbolsPercentageLookup);
            File.WriteAllText($"{Directory.GetCurrentDirectory()}/PersistentSymbolsDictionary.txt", jsonString);
        }

        private static Dictionary<string, Dictionary<decimal, bool>> GetPercentageLookup(decimal symbolTenYearHigh, string symbol)
        {
            var percentageLookup = new Dictionary<string, Dictionary<decimal, bool>>();

            if(!Directory.Exists($"{Directory.GetCurrentDirectory()}/PersistentSymbolsDictionary.txt"))
            {
                var fileStream = File.Create($"{Directory.GetCurrentDirectory()}/PersistentSymbolsDictionary.txt");
                fileStream.Close();
            }

            var persistentLookupJsonString = File.ReadAllText($"{Directory.GetCurrentDirectory()}/PersistentSymbolsDictionary.txt");
            var persistentLookup = JsonConvert.DeserializeObject<Dictionary<string,Dictionary<decimal, bool>>>(persistentLookupJsonString);
            if (persistentLookup != null)
            {
                percentageLookup = persistentLookup;
            }

            if (!percentageLookup.ContainsKey(symbol))
            {
                percentageLookup.Add(symbol, new Dictionary<decimal, bool>());
            }

            if(percentageLookup[symbol].Count == 0 || percentageLookup[symbol].First().Key != symbolTenYearHigh)
            {
                percentageLookup[symbol] = new Dictionary<decimal, bool>();
                for (int i = 0; i < percentStep; i++)
                {
                    percentageLookup[symbol].Add(symbolTenYearHigh - (symbolTenYearHigh * i / percentStep), false);
                }
            }

            return percentageLookup;
        }

        private static async Task<decimal> CalculateSymbolTenYearhigh(IAlpacaDataClient alpacaDataClient, string symbol)
        {
            var historicalBars = await alpacaDataClient.GetHistoricalBarsAsync(new HistoricalBarsRequest(symbol, DateTime.Today.AddYears(-10),
                DateTime.Today, BarTimeFrame.Year));

            decimal tenYearHigh = 0;

            foreach (var bar in historicalBars.Items.First().Value)
            {
                tenYearHigh = (tenYearHigh > bar.High) ? tenYearHigh : bar.High;
            }

            Console.WriteLine($"{symbol}: 10 year high: {tenYearHigh}");
            return tenYearHigh;
        }
    }
}