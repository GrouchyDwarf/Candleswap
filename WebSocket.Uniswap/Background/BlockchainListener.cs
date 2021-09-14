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
            var lastBlockNumberInBlockchain = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            var connection = _sqlConnectionProvider.GetConnection();

            foreach (var candle in Logic2.newCandles(_web3, _logger, connection,
                                                     lastBlockNumberInBlockchain))
            {
                var pair = await _candleStorageService.FetchPairAsync(candle.pair.token0Id, candle.pair.token1Id);
                DbCandle dbCandle = new((long)candle.datetime, candle.resolution,
                        pair.Value.id, candle._open.ToString(), candle.high.ToString(), candle.low.ToString(), candle.close.ToString(), (int)candle.volume);
                WebSocketConnection.OnCandleUpdateReceived((pair.Value, dbCandle));
            }
        }

    }
}
