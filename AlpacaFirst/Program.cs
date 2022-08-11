﻿using Alpaca.Markets;
using Newtonsoft.Json;

namespace AlpacaExample
{
    internal static class Program
    {
        private const string KEY_ID = "";

        private const string SECRET_KEY = "";

        public static async Task Main()
        {
            decimal tenYearHigh = await CalculateTenYearhigh();

            Dictionary<decimal, bool> percentageLookup = GetPercentageLookup(tenYearHigh);

            while (true)
            {
                Pause(1);
                decimal currentTradePrice = GetCurrentPrice();

                var buySteps = percentageLookup.Where(x => !x.Value && x.Key > currentTradePrice);

                foreach (var buy in buySteps)
                {
                    Pause(1);
                    Console.WriteLine($"Buying for price: {buy.Key}");

                    var tradeClient = Environments.Paper.GetAlpacaTradingClient(new SecretKey(KEY_ID, SECRET_KEY));
                    await tradeClient.PostOrderAsync(OrderSide.Buy.Market("BTCUSD", OrderQuantity.FromInt64(1)));
                    await tradeClient.PostOrderAsync(new NewOrderRequest("BTCUSD", OrderQuantity.Fractional(1), OrderSide.Sell, OrderType.Limit,
                        TimeInForce.Gtc)
                    {
                        LimitPrice = Math.Floor(buy.Key + (2 * tenYearHigh / 1000))
                    });

                    percentageLookup[buy.Key] = true;

                    Savelookup(percentageLookup);
                }

                var sellSteps = percentageLookup.Where(x => x.Value && x.Key < currentTradePrice - (2 * tenYearHigh / 1000));

                foreach (var sell in sellSteps)
                {
                    Console.WriteLine($"Buying for price: {currentTradePrice}");

                    percentageLookup[sell.Key] = false;

                    Savelookup(percentageLookup);
                }
            }
        }

        private static void Pause(int seconds)
        {
            Thread.Sleep(1000 * seconds);
        }

        private static decimal GetCurrentPrice()
        {
            var currentTradeClient = Environments.Paper.GetAlpacaCryptoDataClient(new SecretKey(KEY_ID, SECRET_KEY));
            var currentTradePrice = currentTradeClient.GetLatestTradeAsync(new LatestDataRequest("BTCUSD", CryptoExchange.Cbse)).Result.Price;
            //Console.WriteLine($"Current Price: {currentTradePrice}");
            return currentTradePrice;
        }

        private static void Savelookup(Dictionary<decimal, bool> percentageLookup)
        {
            var jsonString = JsonConvert.SerializeObject(percentageLookup);
            File.WriteAllText($"{Directory.GetCurrentDirectory()}/PersistentDictionary.txt", jsonString);
        }

        private static Dictionary<decimal, bool> GetPercentageLookup(decimal tenYearHigh)
        {
            var percentageLookup = new Dictionary<decimal, bool>();

            var persistentLookupJsonString = File.ReadAllText($"{Directory.GetCurrentDirectory()}/PersistentDictionary.txt");
            var persistentLookup = JsonConvert.DeserializeObject<Dictionary<decimal, bool>>(persistentLookupJsonString);
            if (persistentLookup != null && persistentLookup.First().Key == tenYearHigh)
            {
                percentageLookup = persistentLookup;
            }
            else
            {
                for (int i = 0; i < 1000; i++)
                {
                    percentageLookup.Add(tenYearHigh - (tenYearHigh * i / 1000), false);
                }

                decimal portfolioSum = 0;

                foreach (var item in percentageLookup)
                {
                    portfolioSum += item.Key;
                    Console.WriteLine(item.Key);
                }

                Console.WriteLine($"PortfolioSum: {portfolioSum}");
            }

            return percentageLookup;
        }

        private static async Task<decimal> CalculateTenYearhigh()
        {
            var tenYearHighClient = Environments.Paper.GetAlpacaCryptoDataClient(new SecretKey(KEY_ID, SECRET_KEY));

            var historicalBars = await tenYearHighClient.GetHistoricalBarsAsync(new HistoricalCryptoBarsRequest("BTCUSD", DateTime.Today.AddYears(-10),
                DateTime.Today, BarTimeFrame.Year));

            decimal tenYearHigh = 0;

            foreach (var bar in historicalBars.Items.First().Value)
            {
                tenYearHigh = (tenYearHigh > bar.High) ? tenYearHigh : bar.High;
            }

            Console.WriteLine($"10 year high: {tenYearHigh}");
            return tenYearHigh;
        }
    }
}