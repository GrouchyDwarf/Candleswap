USE candleswap;

CREATE TABLE blocks(
    number VARBINARY(32) PRIMARY KEY,
    timestamp VARBINARY(32) NOT NULL
);

CREATE TABLE transactions(
    hash VARBINARY(32) PRIMARY KEY,
    token0Id VARBINARY(20) NOT NULL,
    token1Id VARBINARY(20) NOT NULL,
    amountIn VARBINARY(32) NOT NULL,
    amountOut VARBINARY(32) NOT NULL,
    blockNumber VARBINARY(32) NOT NULL FOREIGN KEY REFERENCES blocks(number),
    nonce VARBINARY(32) NOT NULL
);