using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RedDuck.Candleswap.Candles.CSharp;
using static Domain.Types;

namespace WebSocket.Uniswap.Helpers
{
    public class CandleStorageHelper
    {
        public static async Task<Pair> GetPairAsync(ICandleStorageService candleStorage, string token0Id, string token1Id)
        {
            var pairs = await candleStorage.FetchPairsAsync();
            return pairs.FirstOrDefault(pair => String.Compare(token0Id, pair.token0Id, true) == 0 &&
                                                String.Compare(token1Id, pair.token1Id, true) == 0);
        }

        public static async Task<Pair> GetPairOrCreateNewIfNotExists(ICandleStorageService candleStorage, string token0Id, string token1Id)
        {
            var pair = await GetPairAsync(candleStorage, token0Id, token1Id);

            if (pair == null)
            {
                await candleStorage.AddPairAsync(new Pair(0, token0Id, token1Id));
                var newPair = (await candleStorage.FetchPairsAsync()).FirstOrDefault();
                return newPair;
            }
            else
                return pair;
        }
    }
}
