namespace Indexer

open System.Collections.Concurrent
open Nethereum.Web3
open Nethereum.Hex.HexTypes
open System.Numerics
open Nethereum.RPC.Eth.DTOs
open Domain
open Domain.Types
open Microsoft.Extensions.Logging
open System
open Dater
open System.Threading.Tasks
open System.Threading
open Microsoft.Data.SqlClient

module Logic = 

    let indexBlockAsync (web3:IWeb3) connection (logger:ILogger) (blockNumber:BigInteger) =
        let filterSwapTransactions transactions =
            transactions
            |> Array.filter
                (fun (transaction:Transaction) ->
                    (Array.tryFind(fun address -> address = transaction.To) SwapRouterV2.addresses) <> None
                    && transaction.Input <> "0x"
                    && SwapRouterV2.swapFunctionIds
                       |> Array.exists (fun func -> 
                       transaction.Input.Contains(func)))

        let rec map (transaction: Transaction) =
            async {
                let! receipt = web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction.TransactionHash)
                              |> Async.AwaitTask
                if(receipt <> null)
                then return receipt
                else printfn "Wait receipt"
                     do! Task.Delay(1000) |> Async.AwaitTask
                     //Thread.Sleep(1000)
                     return! map transaction
            }

        let filterSuccessfulTranscations transactionsWithReceipts =
            transactionsWithReceipts
            |> Array.filter
                (fun tr ->
                    let (_, (r:TransactionReceipt)) = tr
                    r.Status.Value <> 0I)

        let tryGetTransactionInfo addresses infoFunc (transaction: Transaction) =
            if Array.contains transaction.To addresses
            then Some infoFunc
            else None

        /// Returns Some function to obtain transaction information if transaction recipient 
        /// matches any known.
        let getTransactionData transaction receipt = 
            [tryGetTransactionInfo SwapRouterV2.addresses SwapRouterV2.getInfoFromRouter] 
            |> List.map (fun f -> f transaction)
            |> List.tryFind Option.isSome 
            |> Option.get
            |> Option.map (fun f -> (f transaction receipt, transaction))

        let rec getBlockOrWaitAsync (web3:IWeb3) (blockNumber:BigInteger) = 
            async{
                let! block = HexBigInteger blockNumber
                             |> web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync
                             |> Async.AwaitTask
                if block <> null
                then return block
                else printfn "Wait block"
                     do! Task.Delay(2000) |> Async.AwaitTask
                     //Thread.Sleep(2000)
                     return! getBlockOrWaitAsync web3 blockNumber     
            }

        async{
            //logger.LogInformation($"Start indexing {blockNumber} block")
            printfn $"Start indexing {blockNumber} block"
            let! block = getBlockOrWaitAsync web3 blockNumber

            do! Db.addBlockAsync connection {number = (HexBigInteger blockNumber).HexValue 
                                             timestamp = block.Timestamp.HexValue}

            let transactions = block.Transactions
                               |> filterSwapTransactions
                               
            let! receipts = transactions
                            |> Array.map map 
                            |> Async.Parallel

            let transactionsWithReceipts = 
                Array.map2 (fun transaction receipt -> (transaction, receipt)) transactions receipts
                |> filterSuccessfulTranscations

            /// Extended information about transactions to Uniswap contracts.
            let actualTransactionsData = 
                transactionsWithReceipts
                |> Array.map (fun (t, r) -> getTransactionData t r)
                |> Array.choose id

            for ((token0Id, token1Id, amountIn, amountOut), transaction) in actualTransactionsData do
                let dbTransaction = 
                    { hash = transaction.TransactionHash
                      token0Id = token0Id
                      token1Id = token1Id
                      amountIn = (HexBigInteger amountIn).HexValue
                      amountOut = (HexBigInteger amountOut).HexValue
                      blockNumber = (HexBigInteger blockNumber).HexValue
                      nonce = transaction.Nonce.HexValue}
                do! Db.addTransactionAsync connection dbTransaction
                do! Db.addPairAsync connection { id = 0L;
                                                token0Id = token0Id; 
                                                token1Id = token1Id}
        }                                  

    let getTransactionsAsync (web3:IWeb3) (logger:ILogger) (connection:SqlConnection) (blockNumber:BigInteger) =
        let filterSwapTransactions transactions =
            transactions
            |> Array.filter
                (fun (transaction:Transaction) ->
                    (Array.tryFind(fun address -> address = transaction.To) SwapRouterV2.addresses) <> None
                    && transaction.Input <> "0x"
                    && SwapRouterV2.swapFunctionIds
                       |> Array.exists (fun func -> 
                       transaction.Input.Contains(func)))

        let rec map (transaction: Transaction) =
            async {
                let! receipt = web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction.TransactionHash)
                              |> Async.AwaitTask
                if(receipt <> null)
                then return receipt
                else printfn "Wait receipt"
                     do! Task.Delay(1000) |> Async.AwaitTask
                     //Thread.Sleep(1000)
                     return! map transaction
            }

        let filterSuccessfulTranscations transactionsWithReceipts =
            transactionsWithReceipts
            |> Array.filter
                (fun tr ->
                    let (_, (r:TransactionReceipt)) = tr
                    r.Status.Value <> 0I)

        let tryGetTransactionInfo addresses infoFunc (transaction: Transaction) =
            if Array.contains transaction.To addresses
            then Some infoFunc
            else None

        /// Returns Some function to obtain transaction information if transaction recipient 
        /// matches any known.
        let getTransactionData transaction receipt = 
            [tryGetTransactionInfo SwapRouterV2.addresses SwapRouterV2.getInfoFromRouter] 
            |> List.map (fun f -> f transaction)
            |> List.tryFind Option.isSome 
            |> Option.get
            |> Option.map (fun f -> (f transaction receipt, transaction))

        let rec getBlockOrWaitAsync (web3:IWeb3) (blockNumber:BigInteger) = 
            async{
                let! block = HexBigInteger blockNumber
                             |> web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync
                             |> Async.AwaitTask
                
                if block <> null
                then return block
                else printfn "Wait block"
                     do! Task.Delay(2000) |> Async.AwaitTask
                     //Thread.Sleep(2000)
                     return! getBlockOrWaitAsync web3 blockNumber     
            }

        async{
            //logger.LogInformation($"Start indexing {blockNumber} block")
            printfn $"Start indexing {blockNumber} block"
            let! block = getBlockOrWaitAsync web3 blockNumber

            //do! Db.addBlockAsync connection {number = (HexBigInteger blockNumber).HexValue 
            //                                 timestamp = block.Timestamp.HexValue}

            let transactions = block.Transactions
                               |> filterSwapTransactions
                               
            let! receipts = transactions
                            |> Array.map map 
                            |> Async.Parallel

            let transactionsWithReceipts = 
                Array.map2 (fun transaction receipt -> (transaction, receipt)) transactions receipts
                |> filterSuccessfulTranscations

            /// Extended information about transactions to Uniswap contracts.
            let actualTransactionsData = 
                transactionsWithReceipts
                |> Array.map (fun (t, r) -> getTransactionData t r)
                |> Array.choose id

            
            let transactions = 
                actualTransactionsData |> Seq.map(fun ((token0Id, token1Id, amountIn, amountOut), transaction) -> 
                    { hash = transaction.TransactionHash
                      token0Id = token0Id
                      token1Id = token1Id
                      amountIn = (HexBigInteger amountIn).HexValue
                      amountOut = (HexBigInteger amountOut).HexValue
                      blockNumber = (HexBigInteger blockNumber).HexValue
                      nonce = transaction.Nonce.HexValue}
                )

            for transaction in transactions do  
                do! Db.addPairAsync connection {id = 0L
                                                token0Id = transaction.token0Id
                                                token1Id = transaction.token1Id}

            return block.Timestamp.Value, transactions

        }            

    let indexInRangeAsync web3 connection (logger:ILogger) startBlock endBlock = 
        async{
            let loop () =
                async{
                    let rec inner i = 
                        async{
                            if i > endBlock
                            then do! indexBlockAsync web3 connection logger i
                                 do! inner (i - 1I)
                        }
                    do! inner startBlock
                }
            do! loop()
        }

    ///Indexing from the end of blockchain to the beginning
    ///start block-inclusive endBlock-noninclusive
    let indexInRangeParallel connection web3 (logger:ILogger) startBlock endBlock stepOption =
        async{
            if startBlock < endBlock 
            then logger.LogError $"startBlock({startBlock}) must be grater than endBlock({endBlock})"
            else
                let step = if Option.isNone stepOption then 20I else stepOption.Value

                let steps = (startBlock - endBlock) / step
                let startOfBlocksNotIndexedYet = startBlock - step * steps

                let loop () =
                    async{
                        let rec inner i j =
                            async{
                                if j > 0I
                                then do! indexInRangeAsync web3 connection logger 
                                                           i (i - step)
                                     //|> Async.StartAsTask
                                     do! inner (i - step) (j - 1I)
                            }
                        do! inner startBlock steps
                    }

                do! loop()

                do! indexInRangeAsync web3 connection logger startOfBlocksNotIndexedYet endBlock
        }

    (*let indexInTimeRangeAsync connection web3 logger startTime endTime =
        async{
            let! startBlock = getBlockNumberByDateTimeOffsetAsync false web3 startTime
            let! endBlock =  getBlockNumberByDateTimeOffsetAsync false web3 endTime

            do! indexInRangeAsync web3 connection logger startBlock.Value endBlock.Value
        }*) 

    let rec getLastRecordedBlockOrWaitWhileNotIndexed connection = 
        async{
            let! blocks = Db.fetchLastRecordedBlockAsync connection
            match Seq.tryLast blocks with 
            | Some block -> return block
            | None -> printfn "Wait while last recorded block not indexed"
                      do! Task.Delay(5000) |> Async.AwaitTask
                      //Thread.Sleep(5000)
                      return! getLastRecordedBlockOrWaitWhileNotIndexed connection      
        }

    let indexNewBlocksAsync connection (web3:IWeb3) logger (checkingForNewBlocksPeriod:int) =
        
        async{
            while true do
                let! lastRecordedBlock = getLastRecordedBlockOrWaitWhileNotIndexed connection
                let! lastBlockInBlockchain = web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()
                                             |> Async.AwaitTask
                do! indexInRangeParallel connection web3 logger lastBlockInBlockchain.Value 
                                         (HexBigInteger lastRecordedBlock.number).Value None
                do! Task.Delay(checkingForNewBlocksPeriod) |> Async.AwaitTask
                //Thread.Sleep(checkingForNewBlocksPeriod)
        }
