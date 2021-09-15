namespace RedDuck.Candleswap.Candles

open System
open System.Timers
open System.Numerics
open Nethereum.Web3
open Nethereum.Hex.HexTypes
open Nethereum.RPC.Eth.DTOs
open Nethereum.Util
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open Domain.Types
open Domain
open Indexer.Dater

module Logic =
    let maxUInt256StringRepresentation =
        "115792089237316195423570985008687907853269984665640564039457584007913129639935"
 
    let calculateCandlesByTransactions connection (swapTransactions:DbTransaction seq) =
        async{
            let calculateCandle amounts =
                let currentPrice (amountIn, amountOut, _) = BigDecimal(amountIn, 0) / BigDecimal(amountOut, 0)//tokenOut per tokenIn
                //let currentPrice (_, _, amountIn, amountOut) = BigDecimal(amountOut, 0) / BigDecimal(amountIn, 0)//tokenIn per tokenOut
                let openPrice = (Seq.last >> currentPrice) amounts
                let closePrice = (Seq.head >> currentPrice) amounts

                let mutable initHigh = BigDecimal(0I, 0)
                let mutable initLow = BigDecimal.Parse maxUInt256StringRepresentation
                let mutable initVolume = 0u

                let high, low, volume = 
                    let folder (high, low, volume) price =
                        (if high > price then high else price),
                        (if low < price then low else price),
                        (volume + 1u)
                    amounts
                    |> Seq.map currentPrice
                    |> Seq.fold folder (initHigh, initLow, initVolume)

                { _open = openPrice
                  low = low
                  high = high
                  close = closePrice
                  volume = volume }

            let amountsPairArray =
                swapTransactions 
                |> Seq.map (fun swapTransaction -> 
                                 let pair  = 
                                     {id = 0L; 
                                      token0Id = swapTransaction.token0Id; 
                                      token1Id = swapTransaction.token1Id;}
                                 ((HexBigInteger swapTransaction.amountIn).Value, 
                                  (HexBigInteger swapTransaction.amountOut).Value, pair))

            let pairsAmounts = amountsPairArray
                               |> Seq.groupBy (fun (_, _, pair) -> pair)

            
            (*do! pairsAmounts
                |> Seq.map (fun (pair, _) -> pair)
                |> Db.addPairsIfNotExistAsync connection   *)     

            let pairsCandles = pairsAmounts
                               |> Seq.map (fun (pair, list) -> (pair, calculateCandle list))
            return pairsCandles
        }

    let buildCandleAsync
        connection
        startBlockNumber
        endBlockNumber
        resolution
        =

        let rec getBlockFromDbOrDelayWhileNotIndexedAsync connection blockNumber = 
            async{
                let! blocks = Db.fetchBlockByNumber connection blockNumber
                match Seq.tryLast blocks with 
                | Some block -> return block
                | None -> printfn "Wait block"
                          do! Task.Delay(10000) |> Async.AwaitTask
                          //Thread.Sleep(10000)
                          return! getBlockFromDbOrDelayWhileNotIndexedAsync connection blockNumber      
            }

        async {
            printfn "New pass"

            let! startBlock = getBlockFromDbOrDelayWhileNotIndexedAsync connection startBlockNumber
                              
            let! endBlock = getBlockFromDbOrDelayWhileNotIndexedAsync connection endBlockNumber 

            let! transactions = Db.fetchTransactionsBetweenBlocks connection startBlockNumber endBlockNumber
                                
            let! pairsWithCandles = calculateCandlesByTransactions connection transactions

            let! pairsWithDbCandles = 
                pairsWithCandles
                |> Seq.map (fun pairWithCandle -> 
                           async{
                               let (pair, candle) = pairWithCandle
                               let! pairFromDb = Db.fetchPairAsync connection pair.token0Id pair.token1Id
                               let dbCandle = { datetime = (int64)(HexBigInteger startBlock.timestamp).Value
                                                resolutionSeconds = resolution
                                                pairId = pairFromDb.Value.id 
                                                _open = candle._open.ToString()
                                                high = candle.high.ToString()
                                                low = candle.low.ToString()
                                                close = candle.close.ToString()
                                                volume = (int)candle.volume}
                               return struct (pair, dbCandle)
                           })
                |> Async.Parallel
            
            return pairsWithDbCandles
        }

    let sendCallbackAndWriteToDBAsync
        connection 
        (pairsWithDbCandles: struct (Pair*DbCandle) [])
        callback =
        async{
            for pairWithDbCandle in pairsWithDbCandles do
                let struct (_, dbCandle) = pairWithDbCandle
                callback((pairWithDbCandle))
                do! Db.addCandle connection dbCandle
        }

    let buildCandleSendCallbackAndWriteToDBAsync
        connection
        callback
        startBlockNumber
        endBlockNumber
        resolution
        =
        async {
            let! pairsWithDbCandles = buildCandleAsync connection startBlockNumber endBlockNumber resolution
            return! sendCallbackAndWriteToDBAsync connection pairsWithDbCandles callback
        }

    let getCandle connection (web3:IWeb3) callback (resolutionTime: TimeSpan) (cancelToken:CancellationToken) =
       
        
        async{
            try
                while cancelToken.IsCancellationRequested <> true do
                    let timer = new System.Diagnostics.Stopwatch()
                    timer.Start()
                    
                    let! lastRecordedBlock = Indexer.Logic.getLastRecordedBlockOrWaitWhileNotIndexed connection
                    let lastRecordedBlocNumber = HexBigInteger lastRecordedBlock.number
                    let! lastBlockNumberInBlockchain = web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()
                                                       |> Async.AwaitTask

                    do! (int)resolutionTime.TotalSeconds
                        |> buildCandleSendCallbackAndWriteToDBAsync connection callback
                                                                    lastBlockNumberInBlockchain
                                                                    lastRecordedBlocNumber
                                                                 
                    //do! Task.Delay(resolutionTime - timer.Elapsed) |> Async.AwaitTask
                    //do! Task.Delay(resolutionTime - timer.) |> Async.AwaitTask
                    //Thread.Sleep(resolutionTime)
                    do! Task.Delay(resolutionTime) |> Async.AwaitTask
                printfn "Operation was canceled!"
            with
            | ex -> ex.ToString() |> printfn "%s"
        }

    let firstUniswapExchangeTimestamp =
        DateTime(2018, 11, 2, 10, 33, 56)

    let getTimeSamples (period: DateTime * DateTime) (rate:TimeSpan) =
        let rec inner (start: DateTime, _end) samples =
            if start <= _end then 
                samples
            else
                let newStart = start.Subtract rate

                inner (newStart, _end) ((start, newStart) :: samples)
        inner period []
        |> List.rev

    let getCandles connection callback (web3: IWeb3) (cancelToken:CancellationToken) period resolution =

        let dateTimeToHex (dateTime:DateTime) = 
            (DateTimeOffset dateTime).ToUnixTimeSeconds()
            |> BigInteger
            |> HexBigInteger
        
        async{
            try

                let timeSamples = getTimeSamples period resolution

                for (start, _end) in timeSamples do
                    try
                        let! startBlockNumber =  getBlockNumberByDateTimeAsync false web3 start 
                        let! endBlockNumber = getBlockNumberByDateTimeAsync false web3 _end
                        do! buildCandleSendCallbackAndWriteToDBAsync connection callback 
                                                                     startBlockNumber
                                                                     endBlockNumber
                                                                     ((int)resolution.TotalSeconds)
                    with
                    | ex -> ex.ToString() |> printfn "%s"

                if cancelToken.IsCancellationRequested = true
                then printfn "Operation was canceled!"
                else printfn "Processing completed successfully"
            with
            | ex -> ex.ToString() |> printfn "%s"
        }

module Logic2 =
    let maxUInt256StringRepresentation =
        "115792089237316195423570985008687907853269984665640564039457584007913129639935"
    type Candle =
        {
          datetime: BigInteger
          pair: Pair
          resolution: int
          _open: BigDecimal
          high: BigDecimal
          low: BigDecimal
          close: BigDecimal
          volume: uint }

    type FullCandle(timeStamp: HexBigInteger, resolution: int) =
        let mutable candle = {_open = BigDecimal(); high = BigDecimal(); low = BigDecimal(); close = BigDecimal(); volume = 0u}

    let resList = [10; 15; 30; 60; 180; 300; 900; 3600; 86400; 604800]

    let initHigh = BigDecimal(0I, 0)
    let initLow = BigDecimal.Parse maxUInt256StringRepresentation
    let initVolume = 0u

    let updateCandle pair amounts isHistoricalCandle (candle:Candle) = 
        let currentPrice (amountIn, amountOut) = BigDecimal(amountIn, 0) / BigDecimal(amountOut, 0)//tokenOut per tokenIn
        let closePrice = if isHistoricalCandle 
                         then candle.close 
                         else (Seq.last >> currentPrice) amounts
        let openPrice = if isHistoricalCandle
                        then (Seq.head >> currentPrice) amounts
                        else candle._open

        let high, low, volume = 
            let folder (high, low, volume) price =
                (if high > price then high else price),
                (if low < price then low else price),
                (volume + 1u)
            amounts
            |> Seq.map currentPrice
            |> Seq.fold folder (candle.high, candle.low, candle.volume)
        {datetime = candle.datetime; pair = pair; resolution = candle.resolution; _open = openPrice; high = high; low = low; volume = volume; close = closePrice}

    let createCandle timeStamp pair amounts isHistoricalCandle (r:int) = 
        let currentPrice (amountIn, amountOut) = BigDecimal(amountIn, 0) / BigDecimal(amountOut, 0)//tokenOut per tokenIn
        let openPrice = (Seq.head >> currentPrice) amounts
        let closePrice = (Seq.last >> currentPrice) amounts

        let high, low, volume = 
            let folder (high, low, volume) price =
                (if high > price then high else price),
                (if low < price then low else price),
                (volume + 1u)
            amounts
            |> Seq.map currentPrice
            |> Seq.fold folder (initHigh, initLow, initVolume)
        if isHistoricalCandle
        then {datetime = timeStamp - BigInteger(r); pair = pair; resolution = r; _open = openPrice; high = high; low = low; volume = volume; close = closePrice}
        else {datetime = timeStamp; pair = pair; resolution = r; _open = openPrice; high = high; low = low; volume = volume; close = closePrice}

    
    let expect list itemsToExclude = 
        list
        |> List.filter (fun first -> 
            itemsToExclude 
            |> List.forall (fun second -> first.datetime <> second.datetime ||  
                                          first.pair.token0Id <> second.pair.token0Id ||
                                          first.pair.token1Id <> second.pair.token1Id ||
                                          first.resolution <> second.resolution))

    let newCandles web logger  blockStart (*(callback: List<Candle> -> Task)*) = 
        let rec loop (i:int) (candles:Candle list) = seq {

            let timeStamp, transactions = Indexer.Logic.getTransactionsAsync web logger (blockStart + BigInteger(i)) |> Async.RunSynchronously

            let closedCandles = candles |> List.filter(fun candles -> BigInteger(candles.resolution) + candles.datetime <= timeStamp)
            yield! closedCandles

            let nextCandles = candles |> List.filter(fun candles -> BigInteger(candles.resolution) + candles.datetime > timeStamp)

            let pairsFromTransactions = 
                transactions
                |> Seq.groupBy(fun tr -> {id = 0L; token0Id = tr.token0Id; token1Id = tr.token1Id})

            let transactionsForExistCandles =
                pairsFromTransactions
                |> Seq.filter(fun (pair,_) -> 
                        nextCandles 
                        |> List.exists(fun c -> 
                            c.pair.token0Id = pair.token0Id && c.pair.token1Id = pair.token1Id 
                        )
                )

            // pairs that are both transactions and candles
            let pairesForExistCandles = transactionsForExistCandles |> Seq.map(fun (p, _) -> p) |> Seq.toList
            let notChangeCandles =
                nextCandles
                |> Seq.filter(fun c -> pairesForExistCandles |> List.exists(fun p -> c.pair = p) |> not)
                |> Seq.toList 


            let newTransactions =
                pairsFromTransactions
                |> Seq.filter(fun (pair,_) -> nextCandles |> List.exists(fun c -> c.pair = pair ) |> not )

            let updatedCandles = 
                transactionsForExistCandles
                |> Seq.collect(fun (pair,tr) -> 
                    let existCandles = 
                        nextCandles |> List.filter(fun c -> c.pair.token0Id = pair.token0Id && 
                                                            c.pair.token1Id = pair.token1Id)
                    let existResl = existCandles |> List.map(fun c -> c.resolution) |> List.distinct
                    let notExistResolutions = resList |> List.except existResl

                    let amounts = 
                        tr |> Seq.map(fun tr -> 
                            let ain = HexBigInteger tr.amountIn 
                            let aout = HexBigInteger tr.amountOut
                            ain.Value, aout.Value
                        ) |> Seq.toList
                    let newCandles = notExistResolutions |> List.map(createCandle timeStamp pair amounts false)
                    let updatedCandles = existCandles |> List.map(updateCandle pair amounts false)
                    newCandles @ updatedCandles
                )
                |> Seq.toList

            let newCandles = 
                newTransactions
                |> Seq.collect(fun (pair,tr) -> 
                    let amounts = 
                        tr |> Seq.map(fun tr -> 
                            let ain = HexBigInteger tr.amountIn 
                            let aout = HexBigInteger tr.amountOut
                            ain.Value, aout.Value
                        ) |> Seq.toList
                    let newCandles = resList |> List.map(createCandle timeStamp pair amounts false)
                    newCandles
                )
                |> Seq.toList

            let all = notChangeCandles @ updatedCandles @ newCandles

            yield! loop (i + 1)  all

        }

        loop 0 []

    //let oldCandles web logger connection blockStart = 
    //    let pancakeDeployBlockNumber = 629407
        
    //    let rec loop (i:BigInteger) (candles:Candle list) = seq {      
    //        if i = (pancakeDeployBlockNumber |> BigInteger)
    //        then yield! []
    //        else
    //        let timeStamp, transactions = Indexer.Logic.getTransactionsAsync web logger connection (i - 1I) |> Async.RunSynchronously

    //        let closedCandles = candles |> List.filter(fun candles -> candles.datetime > timeStamp)
    //        yield! closedCandles

    //        let nextCandles = candles |> List.filter(fun candles -> candles.datetime <= timeStamp)

    //        let pairsFromTransactions = 
    //            transactions
    //            |> Seq.groupBy(fun tr -> {id = 0L; token0Id = tr.token0Id; token1Id = tr.token1Id})

    //        let transactionsForExistCandles =
    //            pairsFromTransactions
    //            |> Seq.filter(fun (pair,_) -> 
    //                    nextCandles 
    //                    |> List.exists(fun c -> 
    //                        c.pair.token0Id = pair.token0Id && c.pair.token1Id = pair.token1Id 
    //                    )
    //            )

    //        // pairs that are both transactions and candles
    //        let pairesForExistCandles = transactionsForExistCandles |> Seq.map(fun (p, _) -> p) |> Seq.toList
    //        let notChangeCandles =
    //            nextCandles
    //            |> Seq.filter(fun c -> pairesForExistCandles |> List.exists(fun p -> c.pair = p) |> not)
    //            |> Seq.toList 


    //        let newTransactions =
    //            pairsFromTransactions
    //            |> Seq.filter(fun (pair,_) -> nextCandles |> List.exists(fun c -> c.pair = pair ) |> not )

    //        let updatedCandles = 
    //            transactionsForExistCandles
    //            |> Seq.collect(fun (pair,tr) -> 
    //                let existCandles = 
    //                    nextCandles |> List.filter(fun c -> c.pair.token0Id = pair.token0Id && 
    //                                                        c.pair.token1Id = pair.token1Id)
    //                let existResl = existCandles |> List.map(fun c -> c.resolution) |> List.distinct
    //                let notExistResolutions = resList |> List.except existResl

    //                let amounts = 
    //                    tr |> Seq.map(fun tr -> 
    //                        let ain = HexBigInteger tr.amountIn 
    //                        let aout = HexBigInteger tr.amountOut
    //                        ain.Value, aout.Value
    //                    ) |> Seq.toList
    //                let newCandles = notExistResolutions |> List.map(createCandle timeStamp pair amounts true)
    //                let updatedCandles = existCandles |> List.map(updateCandle pair amounts true)
    //                newCandles @ updatedCandles
    //            )
    //            |> Seq.toList

    //        let newCandles = 
    //            newTransactions
    //            |> Seq.collect(fun (pair,tr) -> 
    //                let amounts = 
    //                    tr |> Seq.map(fun tr -> 
    //                        let ain = HexBigInteger tr.amountIn 
    //                        let aout = HexBigInteger tr.amountOut
    //                        ain.Value, aout.Value
    //                    ) |> Seq.toList
    //                let newCandles = resList |> List.map(createCandle timeStamp pair amounts true)
    //                newCandles
    //            )
    //            |> Seq.toList

    //        let all = notChangeCandles @ updatedCandles @ newCandles

    //        yield! loop (i - 1I)  all
             
    //    }
    //    loop blockStart []


        