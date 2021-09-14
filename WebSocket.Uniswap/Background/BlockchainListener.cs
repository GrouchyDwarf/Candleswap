using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using RedDuck.Candleswap.Candles;
using RedDuck.Candleswap.Candles.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using WebSocket.Uniswap.Infrastructure;
using static Domain.Types;

namespace WebSocket.Uniswap.Background
{
    public class BlockchainListener: BackgroundService
    {
        private readonly ILogger<BlockchainListener> _logger;
        private readonly IWeb3 _web3;


        public BlockchainListener(ILogger<BlockchainListener> logger, IWeb3 web3)
        {
            _logger = logger;
            _web3 = web3;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Indexer running.");
            await DoWork(cancellationToken);
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            //var lastBlockNumberInBlockchain = await _logicService.GetBlockNumberByDateTimeAsync(false, startFrom);
            var lastBlockNumberInBlockchain = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();


            foreach(var candle in Logic2.newCandles(_web3, _logger, lastBlockNumberInBlockchain))
            {
                DbCandle dbCandle = new((long)candle.datetime, candle.resolution,
                        0, candle._open.ToString(), candle.high.ToString(), candle.low.ToString(), candle.close.ToString(), (int)candle.volume);
                WebSocketConnection.OnCandleUpdateReceived((candle.pair, dbCandle));
            }
            
            //var pancakeLauchDateTimestamp = new DateTime(2020, 9, 20, 0, 0, 0);

            //_indexerService.IndexInRangeParallel(lastBlockNumberInBlockchain.Value,
            //                                     0,
            //                                     FSharpOption<BigInteger>.None);

            //_indexerService.IndexNewBlockAsync(5);

            //foreach (var period in _defaultPeriods)
            //    _logicService.GetCandle(WebSocketConnection.OnCandleUpdateReceived, TimeSpan.FromSeconds(period),
            //        cancellationToken);

            //foreach(var period in _defaultPeriods)
            //{
            //    _logicService.GetCandles(_ => { }, cancellationToken, 
            //                             (startFrom, pancakeLauchDateTimestamp), TimeSpan.FromSeconds(period));
            //}


        }

       /* private async Task OnCandlesUpdateGet(List<Logic2.Candle> candles)
        {
            foreach(var candle in candles)
            {
                var pair = await _candleStorageService.FetchPairAsync(candle.pair.token0Id, candle.pair.token1Id);
                DbCandle dbCandle = new((long)candle.datetime, candle.resolution,
                        pair.Value.id, candle._open.ToString(), candle.high.ToString(), candle.low.ToString(), candle.close.ToString(), (int)candle.volume);
                WebSocketConnection.OnCandleUpdateReceived((pair.Value, dbCandle));
            }
        }*/
        /*
         private async Task OnCalculatedCandlesGet(IEnumerable<Logic2.Candle> candles)
         {
            foreach (var candle in candles)
            {
                var pair = await _candleStorageService.FetchPairAsync(candle.pair.token0Id, candle.pair.token1Id);
                DbCandle dbCandle = new((long)candle.datetime, candle.resolution,
                        pair.Value.id, candle._open.ToString(), candle.high.ToString(), candle.low.ToString(), candle.close.ToString(), (int)candle.volume);
                await _candleStorageService.AddCandleAsync(dbCandle);
                WebSocketConnection.OnCandleUpdateReceived((pair.Value, dbCandle));
            }
        }*/

    }
}
