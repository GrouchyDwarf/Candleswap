namespace Domain

open System
open System.Collections.Generic
open Dapper
open Microsoft.Data.SqlClient
open Types
open Nethereum.Hex.HexTypes

module Db = 
    let private fetchCandlesSql = 
        "select top (@limit) datetime, resolutionSeconds, pairId, [open] as _open, high, low, [close], volume " +
        "from candles " +
        "where pairId = @pairId and resolutionSeconds = @resolutionSeconds " +
        "and datetime >= @startTime and datetime <= @endTime " +
        "order by datetime desc"

    let private insertCandleSql =
        "insert into candles(datetime, resolutionSeconds, pairId, [open], high, low, [close], volume) "
        + "values (@datetime, @resolutionSeconds, @pairId, @_open, @high, @low, @close, @volume)"

    let private updateCandleSql =
        "update candles set [open] = @_open, high = @high, low = @low, [close] = @close, volume = @volume "
        + "where pairId = @pairId and resolutionSeconds = @resolutionSeconds and datetime = @datetime"

    let private getCandleByDatetimeSql =
        "select datetime, resolutionSeconds, token0Id, token1Id, [open], high, low, [close], volume"
        + "from candles"
        + "where datetime = @datetime"

    let private fetchPairsSql = 
        "select id, convert(varchar(42), token0Id, 1) token0Id, convert(varchar(42), token1Id, 1) token1Id " +
        "from pairs"

    let private fetchPairSql = 
        "select id, convert(varchar(42), token0Id, 1) token0Id, convert(varchar(42), token1Id, 1) token1Id " +
        "from pairs " + 
        "where token0Id = convert(varbinary(20), @token0Id, 1) and token1Id = convert(varbinary(20), @token1Id, 1)"

    let private insertPairSql = 
        "insert into pairs(token0Id, token1Id) " + 
        "values(convert(varbinary(20), @token0Id, 1) , convert(varbinary(20), @token1Id, 1))"

    let private fetchBlocksSql = "select convert(varchar(66), number, 1) number, convert(varchar(66), timestamp, 1) timestamp from blocks"

    let private insertBlockSql = "insert into blocks(number, timestamp) values(convert(varbinary(32), @number, 1), convert(varbinary(32), @timestamp, 1))"

    let private fetchLastRecordedBlockSql = 
        "select top 1 convert(varchar(66), number, 1) number, convert(varchar(66), timestamp, 1) timestamp " +
        "from blocks " + 
        "order by number desc"

    let private fetchBlockByNumberSql = 
        "select convert(varchar(66), number, 1) number, convert(varchar(66), timestamp, 1) timestamp " +
        "from blocks " + 
        "where number = convert(varbinary(32), @number, 1)"

    let private fetchTransactionsSql = 
         "select convert(varchar(66), hash, 1) hash, " +
         "convert(varchar(42), token0Id, 1) token0Id, convert(varchar(42), token1Id, 1) token1Id, convert(varchar(66), amountIn, 1) amountIn, convert(varchar(66), amountOut, 1) amountOut , convert(varchar(66), blockNumber, 1) blockNumber, convert(varchar(66), nonce, 1) nonce " + 
         "from transactions " + 
         "where blockNumber = convert(varbinary(32), @blockNumber, 1) " + 
         "order by blockNumber desc, nonce desc"
    
    let private insertTransactionSql = 
        "insert into transactions(hash, nonce, token0Id, token1Id, amountIn, amountOut, blockNumber) " + 
        "values(convert(varbinary(32), @hash, 1), convert(varbinary(32), @nonce, 1), " +
        "convert(varbinary(20), @token0Id, 1), convert(varbinary(20), @token1Id, 1), convert(varbinary(32), @amountIn, 1), convert(varbinary(32), @amountOut, 1), convert(varbinary(32), @blockNumber, 1))"

    let private fetchTransactionsBetweenBlocksSql = 
        "select convert(varchar(66), hash, 1) hash, " +
        "convert(varchar(42), token0Id, 1) token0Id, convert(varchar(42), token1Id, 1) token1Id, convert(varchar(66), amountIn, 1) amountIn, convert(varchar(66), amountOut, 1) amountOut , convert(varchar(66), blockNumber, 1) blockNumber, convert(varchar(66), nonce, 1) nonce " + 
        "from transactions " + 
        "where convert(varbinary(32), @startBlockNumber, 1) >= blockNumber and blockNumber < convert(varbinary(32), @endBlockNumber, 1)" + 
        "order by blockNumber desc, nonce desc"


    let inline (=>) k v = k, box v

    let private handleHexStringBeforeConvertingToVarbinary (hex:string) = 
        if hex.Length % 2 <> 0 
        then hex.Insert(2, "0")
        else hex

    let private dbQuery<'T> (connection: SqlConnection) (sql: string) (parameters: IDictionary<string, obj> option) =
        match parameters with
        | Some (p) -> connection.QueryAsync<'T>(sql, p)
        | None -> connection.QueryAsync<'T>(sql)

    let private dbExecute (connection: SqlConnection) (sql: string) (data: _) = connection.ExecuteAsync(sql, data)

    let fetchCandles connection pairId (resolutionSeconds: int) startTime endTime limit =
        async {
            let! candles =
                Async.AwaitTask
                <| dbQuery<DbCandle>
                    connection
                    fetchCandlesSql
                    (Some(
                        dict [ "pairId" => pairId
                               "resolutionSeconds" => resolutionSeconds
                               "startTime" => startTime
                               "endTime" => endTime
                               "limit" => limit]
                    ))

            return candles
        }

    let fetchCandlesTask connection pairId (resolutionSeconds: int) startTime endTime limit  =
        fetchCandles connection pairId resolutionSeconds startTime endTime limit
        |> Async.StartAsTask

    let addCandle connection candle =
        async {
            let! rowsChanged =
                Async.AwaitTask
                <| dbExecute connection insertCandleSql candle

            printfn "records added: %i" rowsChanged
        }

    let updateCandle connection candle =
        async {
            let! rowsChanged =
                Async.AwaitTask
                <| dbExecute connection updateCandleSql candle

            printfn "records added: %i" rowsChanged
        }

    let getCandleByDatetime connection datetime =
        async {
            let queryParams = Some <| dict [ "datetime" => datetime ]

            let! candle =
                Async.AwaitTask
                <| dbQuery connection getCandleByDatetimeSql queryParams

            return candle
        }

    let fetchPairsAsync connection = 
        async{
            return! dbQuery<Pair> connection fetchPairsSql None
                    |> Async.AwaitTask
        }

    let fetchPairAsync connection (token0Id:string) (token1Id:string) = 
        async{
            
            let firstParam = handleHexStringBeforeConvertingToVarbinary token0Id
            let secondParam = handleHexStringBeforeConvertingToVarbinary token1Id

            let! pairs = dict ["token0Id" => firstParam
                               "token1Id" => secondParam ]
                         |> Some
                         |> dbQuery<Pair> connection fetchPairSql
                         |> Async.AwaitTask

            return Seq.tryLast pairs
        }

    let addPairAsync connection (pair:Pair) = 
        async{
            let! pairFromDb = fetchPairAsync connection pair.token0Id pair.token1Id

            let record = { id = 0L;
                           token0Id = handleHexStringBeforeConvertingToVarbinary pair.token0Id;
                           token1Id = handleHexStringBeforeConvertingToVarbinary pair.token1Id}

            match pairFromDb with 
            | Some _ -> printfn "such pair is already registered"
            | None -> let! rowsChanged = dbExecute connection insertPairSql record
                                         |> Async.AwaitTask
                      printfn "records added: %i" rowsChanged
        }

    let fetchPairOrCreateNewIfNotExists connection token0Id token1Id = 
        async{
            let! pair = fetchPairAsync connection token0Id token1Id
            match pair with
            | Some pair -> return pair
            | None -> let pair = {id = 0L; token0Id = token0Id; token1Id = token1Id}
                      do! addPairAsync connection pair
                      return pair
        }

    let addPairsIfNotExistAsync connection (pairs:Pair seq) = 
        async{
            //let! pairsFromDb = fetchPairsAsync connection
            for pair in pairs do
                do! addPairAsync connection pair
        }

    let fetchBlocksAsync connection = 
        async{
            return! dbQuery<Block> connection fetchBlocksSql None
                    |> Async.AwaitTask
        }

    let fetchLastRecordedBlockAsync connection =
        async{
            let! blocks = dbQuery<Block> connection fetchLastRecordedBlockSql None
                          |> Async.AwaitTask
            return blocks
        }

    let fetchBlockByNumber connection (number:HexBigInteger) =
        async{
            let param = handleHexStringBeforeConvertingToVarbinary number.HexValue

            let! blocks = dbQuery<Block> connection fetchBlockByNumberSql (Some(dict ["number" => param]))
                          |> Async.AwaitTask
            return blocks
        }

    let addBlockAsync connection (block:Block) =
        async{
            let record = {number  = handleHexStringBeforeConvertingToVarbinary block.number
                          timestamp = handleHexStringBeforeConvertingToVarbinary block.timestamp}
            let! rowsChanged = dbExecute connection insertBlockSql record
                               |> Async.AwaitTask
            printfn "records added: %i" rowsChanged
        }

    let fetchTransactionsAsync connection (blockNumber:HexBigInteger) = 
        async {
            let param = handleHexStringBeforeConvertingToVarbinary blockNumber.HexValue
            let! transactions =
                Async.AwaitTask
                <| dbQuery<DbTransaction>
                    connection
                    fetchTransactionsSql
                    (Some(dict [ "blockNumber" => param]))
            return transactions
        }

    let addTransactionAsync connection (transaction:DbTransaction) = 
        async {
            let record = {hash = handleHexStringBeforeConvertingToVarbinary transaction.hash
                          token0Id = handleHexStringBeforeConvertingToVarbinary transaction.token0Id
                          token1Id = handleHexStringBeforeConvertingToVarbinary transaction.token1Id
                          amountIn = handleHexStringBeforeConvertingToVarbinary transaction.amountIn
                          amountOut = handleHexStringBeforeConvertingToVarbinary transaction.amountOut
                          blockNumber = handleHexStringBeforeConvertingToVarbinary transaction.blockNumber
                          nonce = handleHexStringBeforeConvertingToVarbinary transaction.nonce}
            let! rowsChanged =
                Async.AwaitTask
                <| dbExecute connection insertTransactionSql record

            printfn "records added: %i" rowsChanged
        }

    ///start block-inclusive endBlock-noninclusive
    let fetchTransactionsBetweenBlocks connection (startBlockNumber:HexBigInteger) (endBlockNumber:HexBigInteger) = 
        async {
            let firstParam = handleHexStringBeforeConvertingToVarbinary startBlockNumber.HexValue
            let secondParam = handleHexStringBeforeConvertingToVarbinary endBlockNumber.HexValue
            let! transactions =
                Async.AwaitTask
                <| dbQuery<DbTransaction>
                    connection
                    fetchTransactionsBetweenBlocksSql
                    (Some(dict [ "startBlockNumber" => firstParam
                                 "endBlockNumber" => secondParam]))
            return transactions
        }
