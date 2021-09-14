using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RedDuck.Candleswap.Candles.CSharp;

namespace WebSocket.Uniswap.Controllers
{
    //[Route("api/[controller]")]
    //[ApiController]
    //public class PairsController : ControllerBase
    //{
    //    private readonly ICandleStorageService _candleStorage;
    //    public PairsController(ICandleStorageService candleStorage)
    //    {
    //        _candleStorage = candleStorage;
    //    }

    //    [HttpGet()]
    //    public async Task<object> GetPairs()
    //    {
    //        return (await _candleStorage.FetchPairsAsync()).Select(pair => new { token0Id = pair.token0Id, 
    //                                                                             token1Id = pair.token1Id});
    //    }
    //}
}
