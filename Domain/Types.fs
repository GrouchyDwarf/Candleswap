namespace Domain

open System.Numerics
open Nethereum.Util
open Nethereum.ABI.FunctionEncoding.Attributes
open Nethereum.Hex.HexTypes

module Types =
    type Candle =
        { _open: BigDecimal
          high: BigDecimal
          low: BigDecimal
          close: BigDecimal
          volume: uint }
    
    [<CLIMutable>]
    type DbCandle =
        { datetime: int64
          resolutionSeconds: int
          pairId:int64
          _open: string
          high: string
          low: string
          close: string
          volume: int }

    [<CLIMutable>]
    type Pair =
        { id: int64
          token0Id: string
          token1Id: string }

    type Block = {
        number: string
        timestamp: string
    }

    type DbTransaction = {
        hash: string
        token0Id: string
        token1Id: string
        amountIn:string
        amountOut:string
        blockNumber: string
        nonce:string
    }

    [<Event("Swap")>]
    type SwapEvent() =        
        inherit EventDTO()

        [<Parameter("address", "sender", 1, true)>]
        member val Sender = Unchecked.defaultof<string> with get, set

        [<Parameter("uint", "amount0In", 2, false)>]
        member val Amount0In = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("uint", "amount1In", 3, false)>]
        member val Amount1In = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("uint", "amount0Out", 4, false)>]
        member val Amount0Out = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("uint", "amount1Out", 5, false)>]
        member val Amount1Out = Unchecked.defaultof<BigInteger> with get, set

        [<Parameter("address", "to", 6, true)>]
        member val To = Unchecked.defaultof<string> with get, set