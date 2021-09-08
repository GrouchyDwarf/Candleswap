open System
open Nethereum.Web3
open RedDuck.Candleswap.Candles
open System.Threading
open Indexer.Logic
open Microsoft.Extensions.Logging
open Domain.Types
open Microsoft.Data.SqlClient

[<EntryPoint>]
let main args =
    let connectionString = args.[0]
    //let connection = new SqlConnection(connectionString)
    let connection = new SqlConnection("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=candleswap;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False;MultipleActiveResultSets=True")
    connection.Open()
    let period = (int)args.[1]
    let web3 = Web3(args.[2])
    let startFrom = DateTime.UtcNow
    let endTime = startFrom - TimeSpan(0, 0, period)
    let logger = new Logger<DbCandle>(new LoggerFactory())

    //indexInTimeRangeAsync connection web3 logger startFrom endTime
    //|> Async.RunSynchronously


    (*Logic.getCandles connection (fun candle -> printfn "candle received" ) (TimeSpan.FromSeconds((float)period)) web3 CancellationToken.None startFrom
    |> Async.RunSynchronously*)
    0
