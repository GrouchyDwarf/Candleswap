namespace Indexer

open System
open System.Numerics
open Nethereum.RPC.Eth.DTOs
open Nethereum.ABI.FunctionEncoding.Attributes
open Nethereum.Contracts
open Contracts.UniswapV1Exchange.ContractDefinition

module UniswapExchangeV1 =
    let exchangeAddress = "0x09cabec1ead1c0ba254b09efb3ee13841712be14"
    let addresses = [ exchangeAddress ]
    let wethAddress = "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2"

    let ethToTokenSwapInputId = "0xf39b5b9b"
    let ethToTokenTransferInputId = "0xad65d76d"
    let ethToTokenSwapOutputId = "0x6b1d4db7"
    let ethToTokenTransferOutputId = "0x0b573638"

    let tokenToEthSwapInputId = "0x95e3c50b"
    let tokenToEthTransferInputId = "0x7237e031"
    let tokenToEthSwapOutputId = "0x013efd8b"
    let tokenToEthTransferOutputId = "0xd4e4841d"

    let tokenToTokenSwapInputId = "0xddf7e1a7"
    let tokenToTokenTransferInputId = "0xf552d91b"
    let tokenToTokenSwapOutputId = "0xb040d545"
    let tokenToTokenTransferOutputId = "0xf3c0efe9"

    let tokenToExchangeSwapInputId = "0xb1cb43bf"
    let tokenToExchangeTransferInputId = "0xec384a3e"
    let tokenToExchangeSwapOutputId = "0xea650c7d"
    let tokenToExchangeTransferOutputId = "0x981a1327"


    [<Event("TokenPurchase")>]
    type TokenPurchaseEventDTO() =
        inherit EventDTO()

        [<Parameter("address", "buyer", 1, true)>]
        member val Buyer = Unchecked.defaultof<string> with get, set

        [<Parameter("uint256", "eth_sold", 2, true)>]
        member val EthSold = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("uint256", "tokens_bought", 3, true)>]
        member val TokensBought = Unchecked.defaultof<BigInteger> with get, set


    [<Event("EthPurchase")>]
    type EthPurchaseEventDTO() =
        inherit EventDTO()

        [<Parameter("address", "buyer", 1, true)>]
        member val Buyer = Unchecked.defaultof<string> with get, set

        [<Parameter("uint256", "tokens_sold", 2, true)>]
        member val TokensSold = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("uint256", "eth_bought", 3, true)>]
        member val EthBought = Unchecked.defaultof<BigInteger> with get, set


    let getInfoFromEthToToken (tokenPurchaseEvent: TokenPurchaseEventDTO) = 
        let tokenIn = wethAddress
        let tokenOut = tokenPurchaseEvent.Buyer 
        let amountIn = tokenPurchaseEvent.EthSold
        let amountOut = tokenPurchaseEvent.TokensBought
        (tokenIn, tokenOut, amountIn, amountOut)

    let getInfoFromTokenToEth (ethPurchaseEvent: EthPurchaseEventDTO) = 
        let tokenIn = ethPurchaseEvent.Buyer
        let tokenOut = wethAddress
        let amountIn = ethPurchaseEvent.TokensSold
        let amountOut = ethPurchaseEvent.EthBought
        (tokenIn, tokenOut, amountIn, amountOut)

    let getInfoFromTokenToToken (firstTransferEvent:TransferEventDTO) (secondTransferEvent:TransferEventDTO)=
        let tokenIn = firstTransferEvent.From
        let tokenOut = secondTransferEvent.From
        let amountIn = firstTransferEvent.Value
        let amountOut = secondTransferEvent.Value
        (tokenIn, tokenOut, amountIn, amountOut)
    

    let getInfoFromExchange (transaction: Transaction) (transactionReceipt: TransactionReceipt) =
        let transferEvents = transactionReceipt.Logs.DecodeAllEvents<TransferEventDTO>()
        let tokenPurchaseEvents = transactionReceipt.Logs.DecodeAllEvents<TokenPurchaseEventDTO>()
        let ethPurchaseEvents = transactionReceipt.Logs.DecodeAllEvents<EthPurchaseEventDTO>()
        if transaction.Input.Contains(ethToTokenSwapInputId) || transaction.Input.Contains(ethToTokenTransferInputId)
           || transaction.Input.Contains(ethToTokenSwapOutputId) || transaction.Input.Contains(ethToTokenTransferOutputId)
        then getInfoFromEthToToken tokenPurchaseEvents.[0].Event
        else if transaction.Input.Contains(tokenToEthSwapInputId) || transaction.Input.Contains(tokenToEthTransferInputId)
                || transaction.Input.Contains(tokenToEthSwapOutputId) || transaction.Input.Contains(tokenToEthTransferOutputId)
        then getInfoFromTokenToEth ethPurchaseEvents.[0].Event
        else if transaction.Input.Contains(tokenToTokenSwapInputId) || transaction.Input.Contains(tokenToTokenTransferInputId)
                || transaction.Input.Contains(tokenToTokenSwapOutputId) || transaction.Input.Contains(tokenToTokenTransferOutputId)
                || transaction.Input.Contains(tokenToExchangeSwapInputId) || transaction.Input.Contains(tokenToExchangeTransferInputId)
                || transaction.Input.Contains(tokenToExchangeSwapOutputId) || transaction.Input.Contains(tokenToExchangeTransferOutputId)
        then getInfoFromTokenToToken transferEvents.[0].Event transferEvents.[1].Event
        else ("", "", 0I, 0I)
