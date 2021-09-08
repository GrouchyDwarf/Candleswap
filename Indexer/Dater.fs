namespace Indexer

open System
open System.Collections.Generic
open System.Numerics
open Nethereum.Web3
open Nethereum.Hex.HexTypes

module Dater =
    type BlockNumberTimestamp =
        { number: HexBigInteger
          timestamp: HexBigInteger }

    let getBlockNumberAndTimestampAsync
        (web3: IWeb3)
        timestamp
        (blockNumber: HexBigInteger)
        =
        async {
            let! block = web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockNumber)
                         |> Async.AwaitTask
            let t = timestamp
            return {number = blockNumber
                    timestamp = block.Timestamp}
        }

    let isBestBlock date predictedBlockTime previousBlockTime nextBlockTime ifBlockAfterDate =
        match ifBlockAfterDate with
        | true ->
            if predictedBlockTime < date then
                false
            else if predictedBlockTime >= date
                    && previousBlockTime < date then
                true
            else
                false
        | false ->
            if predictedBlockTime > date then
                false
            else if predictedBlockTime <= date && nextBlockTime > date then
            //else if predictedBlockTime < date && nextBlockTime >= date then
                true
            else
                false

    let getNextPredictedBlockNumber currentPredictedBlockNumber skip =
        let nextPredictedBlockNumber = currentPredictedBlockNumber + skip

        if nextPredictedBlockNumber <= 1I then
            1I
        else
            nextPredictedBlockNumber

    let rec findBestBlock date predictedBlock ifBlockAfterDate blockTime web3 =
        async {
            let! previousPredictedBlock =
                predictedBlock.number.Value - 1I
                |> HexBigInteger
                |> getBlockNumberAndTimestampAsync web3 date

            let! nextPredictedBlock =
                predictedBlock.number.Value + 1I
                |> HexBigInteger
                |> getBlockNumberAndTimestampAsync  web3 date

            if isBestBlock
                date
                predictedBlock.timestamp.Value
                previousPredictedBlock.timestamp.Value
                nextPredictedBlock.timestamp.Value
                ifBlockAfterDate then
                return predictedBlock
            else
                let difference = date - predictedBlock.timestamp.Value

                let mutable skip =
                    (float difference) / blockTime |> Math.Ceiling

                if skip = 0.0 then
                    skip <- if difference < 0I then -1.0 else 1.0

                let! nextPredictedBlock =
                    getNextPredictedBlockNumber predictedBlock.number.Value (BigInteger skip)
                    |> HexBigInteger
                    |> getBlockNumberAndTimestampAsync web3 date

                let newBlockTime =
                    (predictedBlock.timestamp.Value
                     - nextPredictedBlock.timestamp.Value)
                    / (predictedBlock.number.Value
                       - nextPredictedBlock.number.Value)
                    |> float
                    |> Math.Abs
                    

                return!
                    findBestBlock date nextPredictedBlock ifBlockAfterDate newBlockTime web3
        }

    let getBlockByDateAsync ifBlockAfterDate (web3: IWeb3) date =
        async {
            let! latestBlockNumber =
                web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()
                |> Async.AwaitTask

            let! latestBlock = getBlockNumberAndTimestampAsync web3 date latestBlockNumber
            let firstBlockNumber = HexBigInteger 0I
            let! firstBlock = getBlockNumberAndTimestampAsync web3 date firstBlockNumber

            let blockTime =
                (float (
                    latestBlock.timestamp.Value
                    - firstBlock.timestamp.Value
                ))
                / (float (latestBlock.number.Value - 1I))

            if date <= firstBlock.timestamp.Value then
                return firstBlock
            else if date >= latestBlock.timestamp.Value then
                return latestBlock
            else
                let! predictedBlock =
                    (float (date - firstBlock.timestamp.Value))
                    / blockTime
                    |> Math.Ceiling
                    |> BigInteger
                    |> HexBigInteger
                    |> getBlockNumberAndTimestampAsync web3 date

                return! findBestBlock date predictedBlock ifBlockAfterDate blockTime web3
        }

    let dateTimeToEpoch date = 
        let t = date - DateTime(1970, 1, 1)
        t.TotalSeconds |> BigInteger

    let getBlockNumberByDateTimeAsync ifBlockAfterDate (web3: IWeb3) (date:DateTime) = 
        async{
            let! blockNumberTimestamp = dateTimeToEpoch date
                                        |> getBlockByDateAsync ifBlockAfterDate web3

            return blockNumberTimestamp.number
        }



