open System
open Nethereum.Web3
open Nethereum.Hex.HexTypes
open Nethereum.Contracts
open Domain.Types
open Domain.Db
open Microsoft.Data.SqlClient
open Indexer.Logic
open Microsoft.Extensions.Logging
open Indexer.Dater

// Define a function to construct a message to print
let from whom =
    sprintf "from %s" whom

[<EntryPoint>]
let main argv =
    let web3 = Web3 "https://bsc-dataseed.binance.org/"

    let connection = new SqlConnection("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=candleswap;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False;MultipleActiveResultSets=True")
    connection.Open()

    let logger = new Logger<DbCandle>(new LoggerFactory())

    let date = DateTime (2021, 9, 2, 0, 8, 25)
    async{
        let! t = getBlockByDateAsync false web3 1630916116I
        0
    } |> Async.RunSynchronously

    let lastBlockInBlockchain = web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()
                                |> Async.AwaitTask
                                |> Async.RunSynchronously
    async{
        do! indexInRangeParallel connection web3 logger lastBlockInBlockchain.Value 0I None
    } |> Async.RunSynchronously

    0

