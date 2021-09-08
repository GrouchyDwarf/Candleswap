/// Contains C#-friendly wrappers.
namespace RedDuck.Candleswap.Candles.CSharp

open System
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open Nethereum.RPC.Eth.DTOs
open Nethereum.Web3;
open RedDuck.Candleswap.Candles
open Domain.Types
open System.Threading
open System.Collections.Generic
open Microsoft.Data.SqlClient
open Domain
open Indexer.Logic
open System.Numerics
open Nethereum.Hex.HexTypes
open Indexer

type ISqlConnectionProvider =
    abstract GetConnection: unit -> SqlConnection

type SqlConnectionProvider(config: IConfiguration) =
    let connectionString = config.GetSection("ConnectionStrings").["Default"]
    let connection = new SqlConnection(connectionString)
    do connection.Open()
    
    interface ISqlConnectionProvider with
        member _.GetConnection() = connection
    
    interface IDisposable with
        member _.Dispose() =
            connection.Dispose()

type ILogicService =
    abstract CalculateCandlesByTransactions:
        swapTransactions: DbTransaction seq ->
        Task<IEnumerable<Pair*Candle>>

    abstract GetCandle:
        callback: Action<struct (Pair*DbCandle)> ->
        resolutionTime: TimeSpan ->
        cancelToken: CancellationToken ->
        Task

    abstract GetCandles:
        callback: Action<struct (Pair*DbCandle)> ->
        cancelToken: CancellationToken ->
        period: struct (DateTime*DateTime) -> 
        resolution: TimeSpan ->
        Task

    abstract GetTimeSamples:
        period: struct (DateTime*DateTime) ->
        rate: TimeSpan -> 
        List<struct (DateTime*DateTime)>

    abstract GetBlockNumberByDateTimeAsync:
        ifBlockAfterDate: bool -> 
        date: DateTime -> 
        Task<HexBigInteger>

type LogicService(web3: IWeb3, sqlite: ISqlConnectionProvider) = 
    let toRefTuple = fun struct (a, b) -> (a, b)
    let toStructTuple = fun (a, b) -> struct(a, b)
    let connection = sqlite.GetConnection()

    interface ILogicService with
        member _.CalculateCandlesByTransactions 
            swapTransactions
            = 
            swapTransactions
            |> Logic.calculateCandlesByTransactions connection  
            |> Async.StartAsTask
        
        member _.GetCandle callback resolutionTime cancelToken =
            let callback str = callback.Invoke(str)
            Logic.getCandle connection web3 callback resolutionTime cancelToken |> Async.StartAsTask :> Task

        member _.GetCandles callback cancelToken period resolution = 
            let callback str = callback.Invoke(str)
            
            let newPeriod = toRefTuple period 

            Logic.getCandles connection callback web3 cancelToken newPeriod resolution 
            |> Async.StartAsTask :> Task

        member _.GetTimeSamples period rate =
            let newPeriod = toRefTuple period
            let result = Logic.getTimeSamples newPeriod rate
                         |> List.map(fun t -> toStructTuple t)
            new List<struct(DateTime*DateTime)>(result)

        member _.GetBlockNumberByDateTimeAsync ifBlockAfterDate date = 
            Dater.getBlockNumberByDateTimeAsync ifBlockAfterDate web3 date |> Async.StartAsTask



module Logic =
    [<Literal>]
    let MaxUInt256StringRepresentation = "115792089237316195423570985008687907853269984665640564039457584007913129639935"

type ICandleStorageService =
    abstract UpdateCandleAsync: Candle -> Task
    abstract AddCandleAsync: DbCandle -> Task
    abstract FetchCandlesAsync: 
        pairId: int64 ->
        resolutionSeconds: int ->
        startTime: int64 ->
        endTime: int64 -> 
        limit: int ->
        Task<seq<DbCandle>>
    abstract FetchPairsAsync: unit -> Task<seq<Pair>>
    abstract AddPairAsync: Pair -> Task
    abstract FetchPairAsync: string -> string -> Task<Pair option>
    abstract FetchPairOrCreateNewIfNotExists: string -> string -> Task<Pair>

type CandleStorageService(sql: ISqlConnectionProvider) =
    let connection = sql.GetConnection()
    
    interface ICandleStorageService with
        member _.UpdateCandleAsync candle = 
            Db.updateCandle connection candle |> Async.StartAsTask :> Task
        
        member _.AddCandleAsync candle = 
            Db.addCandle connection candle |> Async.StartAsTask :> Task
        
        member _.FetchCandlesAsync pairId resolutionSeconds startTime endTime limit = 
            Db.fetchCandles connection pairId resolutionSeconds startTime endTime limit
            |> Async.StartAsTask

        member _.FetchPairsAsync () = 
            Db.fetchPairsAsync connection |> Async.StartAsTask

        member _.AddPairAsync pair = 
            Db.addPairAsync connection pair |> Async.StartAsTask :> Task

        member _.FetchPairAsync token0Id token1Id = 
            Db.fetchPairAsync connection token0Id token1Id |> Async.StartAsTask

        member _.FetchPairOrCreateNewIfNotExists token0Id token1Id = 
            Db.fetchPairOrCreateNewIfNotExists connection token0Id token1Id |> Async.StartAsTask


type IIndexerService =
    abstract IndexNewBlockAsync: int -> Task 
    abstract IndexInRangeParallel: 
        BigInteger -> 
        BigInteger -> 
        BigInteger option -> 
        Task

type IndexerService(web3, sql:ISqlConnectionProvider, logger) = 
    let connection = sql.GetConnection()

    interface IIndexerService with
        member _.IndexNewBlockAsync checkingForNewBlocksPeriod = 
            indexNewBlocksAsync connection web3 logger checkingForNewBlocksPeriod 
            |> Async.StartAsTask :> Task

        member _.IndexInRangeParallel startBlock endBlock step =
            indexInRangeParallel connection web3 logger startBlock endBlock step
            |> Async.StartAsTask :> Task