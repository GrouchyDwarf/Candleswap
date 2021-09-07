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
        private readonly ILogicService _logicService;
        private readonly IIndexerService _indexerService;
        private readonly ICandleStorageService _candleStorageService;
        private readonly IWeb3 _web3;
        private readonly ISqlConnectionProvider _sqlConnectionProvider;

        private readonly int[] _defaultPeriods = { 15, 60, 600 };

        public BlockchainListener(ILogger<BlockchainListener> logger, ILogicService logicService,
                                  IIndexerService indexerService, IWeb3 web3, ICandleStorageService candleStorageService,
                                  ISqlConnectionProvider sqlConnectionProvider)
        {
            _logger = logger;
            _logicService = logicService;
            _indexerService = indexerService;
            _web3 = web3;
            _candleStorageService = candleStorageService;
            _sqlConnectionProvider = sqlConnectionProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Indexer running.");
            await DoWork(cancellationToken);
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            //var lastBlockInBlockchain = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var startFrom = DateTime.UtcNow;

            //var lastBlockNumberInBlockchain = await _logicService.GetBlockNumberByDateTimeAsync(false, startFrom);
            var lastBlockNumberInBlockchain = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            var connection = _sqlConnectionProvider.GetConnection();

            Task.Run(async () =>
            {

                foreach(var candle in Logic2.newCandles(_web3, _logger, connection,
                    lastBlockNumberInBlockchain/*, (FSharpFunc<List<Logic2.Candle>, Task>)OnCandlesUpdateGet*/))
                {
                    var pair = await _candleStorageService.FetchPairAsync(candle.pair.token0Id, candle.pair.token1Id);
                    DbCandle dbCandle = new((long)candle.datetime, candle.resolution,
                            pair.Value.id, candle._open.ToString(), candle.high.ToString(), candle.low.ToString(), candle.close.ToString(), (int)candle.volume);
                    await _candleStorageService.AddCandleAsync(dbCandle);
                    WebSocketConnection.OnCandleUpdateReceived((pair.Value, dbCandle));
                }
            });/*
            Task.Run(async () =>
            {
                foreach (var c in RedDuck.Candleswap.Candles.Logic2.oldCandles(_web3, _logger, connection, lastBlockNumberInBlockchain))
                {
                    var pair = await _candleStorageService.FetchPairAsync(c.pair.token0Id, c.pair.token1Id);
                    DbCandle dbCandle = new((long)c.datetime, c.resolution,
                        pair.Value.id, c._open.ToString(), c.high.ToString(), c.low.ToString(), c.close.ToString(), (int)c.volume);
                    await _candleStorageService.AddCandleAsync(dbCandle);
                }
            });*/


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
