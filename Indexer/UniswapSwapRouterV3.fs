namespace Indexer

open System
open System.Numerics
open Nethereum.RPC.Eth.DTOs
open Nethereum.ABI.FunctionEncoding.Attributes
open Nethereum.Hex.HexConvertors.Extensions
open Nethereum.Contracts
open Nethereum.Util
open Contracts.UniswapV3Router.ContractDefinition

module UniswapSwapRouterV3 =
    let routerAddress = "0xe592427a0aece92de3edee1f18e0157c05861564"
    let addresses = [ routerAddress ]

    let exactInputSingleId = "0x414bf389"
    let exactOutputSingleId = "0xdb3e2198"
    let exactInputId = "0xc04b8d59"
    let exactOutputId = "0xf28c0498"
    let multicallId = "0xac9650d8"
    let lengthForSimpleCall = 648
    let lengthForSingleCall = 520

    [<Event("Swap")>]
    type SwapEventDTO() =
        inherit EventDTO()

        [<Parameter("address", "sender", 1, true)>]
        member val Sender = Unchecked.defaultof<string> with get, set

        [<Parameter("address", "recipient", 2, true)>]
        member val Recipient = Unchecked.defaultof<string> with get, set

        [<Parameter("int256", "amount0", 3, false)>]
        member val Amount0 = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("int256", "amount1", 4, false)>]
        member val Amount1 = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("uint160", "sqrtPriceX96", 5, false)>]
        member val SqrtPriceX96 = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("uint128", "liquidity", 6, false)>]
        member val Liquidity = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("int24", "tick", 7, false)>]
        member val Tick = Unchecked.defaultof<BigInteger> with get, set


    let getSingleInfoFromRouter (func: FunctionMessage) (event: SwapEventDTO) transactionInput =
        let amountIn =
            if event.Amount0 < 0I then
                event.Amount0 * (-1I)
            else
                event.Amount0

        let amountOut =
            if event.Amount1 < 0I then
                event.Amount1 * (-1I)
            else
                event.Amount1

        if func :? ExactOutputSingleFunction then
            let decodedInput =
                (new ExactOutputSingleFunction())
                    .DecodeInput(transactionInput)

            (decodedInput.Params.TokenIn, decodedInput.Params.TokenOut, amountIn, amountOut)
        else if func :? ExactInputSingleFunction then
            let decodedInput =
                (new ExactInputSingleFunction())
                    .DecodeInput(transactionInput)

            (decodedInput.Params.TokenIn, decodedInput.Params.TokenOut, amountIn, amountOut)
        else
            ("", "", 0I, 0I)


    let getSimpleInfoFromRouter (func: FunctionMessage) (event: SwapEventDTO) transactionInput =
        let getAmount amount = 
            if amount < 0I then amount * (-1I) else amount
          
        let amountIn = getAmount event.Amount0
        let amountOut = getAmount event.Amount1

        if func :? ExactOutputFunction then
            let decodedInput =
                (new ExactOutputFunction())
                    .DecodeInput(transactionInput)

            let tokenIn =
                "0x"
                + decodedInput.Params.Path.Slice(46, 66).ToHex()
            //let second = decoded.Params.Path.Slice(23, 43).ToHex()
            let tokenOut =
                "0x"
                + decodedInput.Params.Path.Slice(0, 20).ToHex()

            (tokenIn, tokenOut, amountIn, amountOut)
        else if func :? ExactInputFunction then
            let decodedInput =
                (new ExactInputFunction())
                    .DecodeInput(transactionInput)

            let tokenIn =
                "0x"
                + decodedInput.Params.Path.Slice(0, 20).ToHex()
            //let second = decoded.Params.Path.Slice(23, 43).ToHex()
            let tokenOut =
                "0x"
                + decodedInput.Params.Path.Slice(46, 66).ToHex()

            (tokenIn, tokenOut, amountIn, amountOut)
        else
            ("", "", 0I, 0I)

    let multicallToCall (multicall: string) length index =
        "0x" + multicall.Substring(index, length)

    let getInfoFromRouter (transaction: Transaction) (transactionReceipt: TransactionReceipt) =
        let swapEvents = transactionReceipt.Logs.DecodeAllEvents<SwapEventDTO>()
        if transaction.Input.StartsWith(multicallId) then
            if transaction.Input.Contains(exactInputId.Replace("0x", "")) then
                transaction.Input.IndexOf(exactInputId.Replace("0x", ""))
                |> multicallToCall transaction.Input lengthForSimpleCall
                |> getSimpleInfoFromRouter (new ExactInputFunction())swapEvents.[0].Event
            else if transaction.Input.Contains(exactOutputId.Replace("0x", "")) then
                transaction.Input.IndexOf(exactOutputId.Replace("0x", ""))
                |> multicallToCall transaction.Input lengthForSimpleCall
                |> getSimpleInfoFromRouter (new ExactOutputFunction()) swapEvents.[0].Event
            else if transaction.Input.Contains(exactInputSingleId) then
                transaction.Input.IndexOf(exactInputSingleId.Replace("0x", ""))
                |> multicallToCall transaction.Input lengthForSingleCall
                |> getSingleInfoFromRouter (new ExactInputSingleFunction()) swapEvents.[0].Event
            else if transaction.Input.Contains(exactOutputSingleId.Replace("0x", "")) then
                transaction.Input.IndexOf(exactOutputSingleId.Replace("0x", ""))
                |> multicallToCall transaction.Input lengthForSingleCall
                |> getSingleInfoFromRouter (new ExactOutputSingleFunction()) swapEvents.[0].Event
            else
                ("", "", 0I, 0I)
        else if transaction.Input.Contains(exactInputId) then
            getSimpleInfoFromRouter (new ExactInputFunction()) swapEvents.[0].Event transaction.Input
        else if transaction.Input.Contains(exactOutputId) then
            getSimpleInfoFromRouter (new ExactOutputFunction()) swapEvents.[0].Event transaction.Input
        else if transaction.Input.Contains(exactInputSingleId) then
            getSingleInfoFromRouter (new ExactInputSingleFunction()) swapEvents.[0].Event transaction.Input
        else if transaction.Input.Contains(exactOutputSingleId) then
            getSingleInfoFromRouter (new ExactOutputSingleFunction()) swapEvents.[0].Event transaction.Input
        else
            ("", "", 0I, 0I)
